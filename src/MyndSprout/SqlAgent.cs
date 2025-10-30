//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2025.
using Microsoft.Data.SqlClient;
using SwitchLLM;
using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyndSprout
{
    /// <summary>
    /// A simple, iterative SQL agent. Each epoch:
    ///  1) Builds context (DB schema + last query results, if any)
    ///  2) Asks the LLM for an <SqlTextRequest>...</SqlTextRequest> payload
    ///  3) Executes it via SqlStrings.ExecuteAsyncStr
    ///  4) If it was a query, folds results into next-epoch context
    ///  5) Calls IsComplete; if true, stops
    /// Returns final context (schema + last query results if last call was a query)
    /// </summary>
    public sealed class SqlAgent
    {
        private readonly SqlStrings _sql = null!;
        private readonly int _maxEpochs;
        private string? _updatedPrompt;
        public bool UseIsComplete = true;
        public bool QueryOnly = false;
        public bool NaturalLanguageResponse = false;
        public int MaximumLastQueryOutputLength = 50000;
        public bool UseSearch = false;
        public bool KeepEpisodics = false;
        public bool ReadFullSchema = false;
        public event Action? OnEpochEnd;
        public int BackupInterval = 50;
        public string BackupPath = @"C:\Users\wowod\Desktop\Code2025\MyndSproutDatabases";
        private EpisodicsLogWriter _episodicsWriter = null!;

        public SqlStrings Sql { get; private set; }

        public SqlAgent(SqlStrings sqlStrings, int maxEpochs = 5)
        {
            _sql = sqlStrings ?? throw new ArgumentNullException(nameof(sqlStrings));
            this.Sql = _sql;
            _maxEpochs = Math.Max(1, maxEpochs);
        }

        public void UpdatePrompt(string newPrompt)
        {
  		    Interlocked.Exchange(ref _updatedPrompt, newPrompt);
    	}

        /// <summary>
        /// Run the agent. The schema is always part of context. The agent may stop early if IsComplete returns true.
        /// Returns the final context XML string (Schema + LastQueryResult if a query was last executed).
        /// </summary>
        public async Task<string> RunAsync(string prompt, Action<string> CallLog, CancellationToken ct = default)
        {
            CallLog("Agent started.");

            if (string.IsNullOrWhiteSpace(prompt)) prompt = "No prompt provided.";

            string defaultWriterConn = EpisodicsLogWriter.BuildDefaultWriterConnectionString(_sql.Database);

            _episodicsWriter ??= await EpisodicsLogWriter.CreateForAgentAsync(
                existingDbConn: _sql.Database,
                episodicsWriterConnectionString: null,   // null => reuse LocalDB connection
                log: CallLog
            );

            await _sql.EnsureEpisodicsTableAsync();

            string episodicText = "No data yet.";
            int startingEpoch = 1;
            string queryOutput = "No data yet.";
            string schemaXml;
            EpisodicRecord? initial = null;
            if (!KeepEpisodics)
            {
                await ClearEpisodicsAsync(_sql.Database);
            }
            else
            {
                initial = await LoadMostRecentEpisodicRecordAsync(ct, projectId: 1); // or make ProjectId a field/ctor arg
                if (initial != null)
                {
                    episodicText = initial.EpisodicText ?? "No data yet.";
                    startingEpoch = initial.EpochIndex + 1;
                    // restore last query output so the LLM can continue from actual context
                    if (!string.IsNullOrWhiteSpace(initial.QueryResult))
                        queryOutput = initial.QueryResult!;
                    // optional: restore the last known schema as a starting point if you don't do ReadFullSchema
                    if (!ReadFullSchema && !string.IsNullOrWhiteSpace(initial.DatabaseSchema))
                        schemaXml = initial.DatabaseSchema!;
                }
                else
                {
                    // No prior episodics, just start clean rather than returning early
                    CallLog("No prior episodic record found. Starting fresh.");
                }
            }

            int endEpoch = startingEpoch + _maxEpochs - 1;


            bool resumed = KeepEpisodics && startingEpoch > 1;
            if (resumed)
            {
                CallLog($"Agent resumed. Starting at absolute epoch {startingEpoch}.");
                try
                {
                    var snap = await _sql.GetSchemaAsyncStr("<Empty/>");
                    schemaXml = Common.WrapInTags(snap, "CurrentSchema");
                }
                catch { /* keep existing schemaXml fallback */ }
            }

            if (ReadFullSchema)
            {
                // Always take a fresh snapshot when asked
                var snap = await _sql.GetSchemaAsyncStr("<Empty/>");
                schemaXml = Common.WrapInTags(snap, "CurrentSchema");
            }
            else if (resumed && !string.IsNullOrWhiteSpace(initial!.DatabaseSchema))
            {
                // Reuse last-known schema on resume to give the LLM real structure
                schemaXml = initial.DatabaseSchema!;
            }
            else
            {
                // Fallback guidance when we neither read full schema nor have one to reuse
                var sbSchema = new StringBuilder(2048);
                sbSchema.AppendLine("ReadFullSchema=false so you will need to request schema information if it is required.");
                sbSchema.AppendLine("Never guess table/column/procedure names.");
                sbSchema.AppendLine("Prefer parameterized SQL; no guessing.");
                schemaXml = sbSchema.ToString();
            }

            var backup = new AutoBackupManager(Sql, _sql.Database.Database, new AutoBackupSettings
            {
                EpochInterval = BackupInterval,
                BackupDirectory = BackupPath, // or leave default
                ProjectId = 1,                // or null for any
                SystemName = "MyndSprout"
            });

            await EpisodicsLogWriter.RunBootstrapOnceAsync(
                existingDbConn: _sql.Database,          // discovers DB name here
                log: CallLog,                           // optional
                adminConnString: null                   // or pass an admin conn string if you want
            );


            string error = "";
            int epoch = 0;
            for (epoch = startingEpoch; epoch <= endEpoch; epoch++)
            {
                CallLog($"Epoch {epoch} starting.");

                ct.ThrowIfCancellationRequested();

                string ? newPrompt = Interlocked.Exchange(ref _updatedPrompt, null);
                if (newPrompt != null)
                {
                    prompt = newPrompt;
                    CallLog($"--- PROMPT UPDATED at epoch {epoch} ---");
                }

                string prepareQueryPrompt = BuildPrepareQueryPromptMulti(prompt, schemaXml, queryOutput, episodicText, epoch, endEpoch, QueryOnly, UseSearch);
                if (error.Length > 0)
                {
                    prepareQueryPrompt += Environment.NewLine + "Prior error: " + error;
                }
                CallLog($"Prepare query prompt: {Environment.NewLine + prepareQueryPrompt}");
                string queryInput ="";
                if (UseSearch && prepareQueryPrompt.Contains("UseSearch"))
                {
                    Response response = await LLM.SearchQuery(prepareQueryPrompt);
                    queryInput = response.Result;
                }
                else
                {
                    Response response = await LLM.Query(prepareQueryPrompt);
                    queryInput = response.Result;
                }
                CallLog($"Query Input: {Environment.NewLine + queryInput}");
                var xmlReq = Common.FromXml<SqlXmlRequest>(queryInput);
                error = "";
                if (xmlReq == null)
                {
                    error = "LLM did not return valid <SqlXmlRequest> XML.";
                }
                else if (QueryOnly)
                {
                    if (xmlReq.CommandType != System.Data.CommandType.Text)
                    {
                        error = "Read-only mode: only CommandType=Text is allowed.";
                        continue;
                    }
                    var hits = MyndSprout.Security.SqlMutationScanner.Scan(xmlReq.Sql);
                    if (hits.Count > 0)
                    {
                        error = "Read-only mode: Potentially mutating SQL detected; blocked.";
                        continue;
                    }
                }

                if (xmlReq != null)
                {
                    try
                    {
                        queryOutput = await _sql.ExecuteToXmlAsync(xmlReq);
                    }
                    catch (Exception ex)
                    {
                        error = "Error: " + Environment.NewLine  + queryOutput + Environment.NewLine + ex.Message;
                    }

                    CallLog($"Query Output: {Environment.NewLine + queryOutput}");

                    if (QueryOnly)
                    {
                        return queryOutput;
                    }
                }
                else
                {
                    queryOutput = "No valid query output was generated because no valid xml containing sql was created.";
                }

                episodicText = await BuildEpisodicAsync(episodicText, prompt, queryInput, queryOutput!, epoch, ct);
                CallLog("Episodic:" + Environment.NewLine + episodicText);

                if (episodicText == "API Error: TooManyRequests.")
                {
                    error = episodicText;
                    break;
                }

                EpisodicRecord epiRec = new EpisodicRecord();
                epiRec.PrepareQueryPrompt = prepareQueryPrompt;
                epiRec.QueryInput = queryInput;
                epiRec.QueryResult = queryOutput!;
                epiRec.EpisodicText = episodicText;
                epiRec.DatabaseSchema = schemaXml;
                epiRec.EpochIndex = epoch;
                epiRec.ProjectId = 1;
                //await _sql.SaveEpisodicAsync(epiRec);
                await _episodicsWriter.SaveAsync(epiRec, ct);

                if (ReadFullSchema)
                {
                    schemaXml = await _sql.GetSchemaAsyncStr("<Empty/>");
                    schemaXml = Common.WrapInTags(schemaXml, "CurrentSchema");
                }
                else if (xmlReq != null && xmlReq.IsSchemaRequest)
                {
                    if (queryOutput.Contains("<Error>"))
                    {
                        schemaXml = "";
                    }
                    else
                    {
                        schemaXml = queryOutput;
                        queryOutput = "";
                    }
                }

                await backup.TickAsync(epoch, ct);

                if (UseIsComplete && await IsCompleteAsync(prompt, episodicText, schemaXml, queryOutput, epoch, ct))
                {
                    break;
                }

                if (OnEpochEnd != null)
                {
                    OnEpochEnd();
                }
            }

            if (NaturalLanguageResponse)
            {
                string finalResponse = await BuildFinalStateAsync(prompt, queryOutput!, episodicText, ct);
                return finalResponse;
            }

            return BuildFinalStateXml(schemaXml, queryOutput!, epoch);
        }

        private string BuildPrepareQueryPromptMulti(
            string prompt,
            string schemaXml,
            string? lastQueryResultXml,
            string episodic,
            int epoch,
            int maxEpochs,
            bool queryOnly,
            bool useSearch)
        {
            string sqlXmlRequestXsd = InputXmlSchemas.SqlXmlRequestXsd();
            var sb = new StringBuilder(64 * 1024);

            sb.AppendLine("You are a SQL planning assistant. Produce ONLY a <SqlXmlRequest> XML for the next step.");
            sb.AppendLine("It may not be possible to immediately achieve the Primary Goal, aim for the next achievable step.");
            sb.AppendLine("The <SqlXmlRequest> has elements such as <Sql>, optional <Parameters>, <CommandType> (Text or StoredProcedure), and optional <CommandTimeoutSeconds>.");
            sb.AppendLine("The <Sql> must contain valid T-SQL only, consistent with the 'Database Schema' below. Do not attempt any external IO (files, network, CLR, xp_cmdshell, etc.), all IO must be through tables.");
            sb.AppendLine("Multiple statements are allowed; separate them with semicolons. Do NOT use GO batch separators.");
            sb.AppendLine("If the T-SQL in <SQL> is a request for schema information from the database then set the IsSchemaRequest field of the <SqlXmlRequest> to true, else false.");
            sb.AppendLine("Wrapping the sql in CDATA can prevent escape errors.");
            sb.AppendLine("Proof read the sql and your response before returning it.");
            sb.AppendLine("Apply no safety effort where it is unnecessary, but dedicate at least 10% of your reasoning effort to safety and ethical considerations on tasks involving code, system specifications, AI design, or other outputs where security is required.");
            if (useSearch)
            {
                sb.AppendLine("If you need access to an internet search to assist in constructing the next sql request, then write the term UseSearch towards the end of your response.");
            }
            if (queryOnly)
            {
                sb.AppendLine("You are in READ-ONLY mode.");
                sb.AppendLine("Do NOT generate any statements that modify state: no INSERT/UPDATE/DELETE/MERGE, no DDL (CREATE/ALTER/DROP/TRUNCATE), no temp tables, no SELECT INTO, no transactions, no permission changes, and no EXEC of procedures that mutate data.");
                sb.AppendLine("If the Task Intent requests modifications, return a harmless diagnostic SELECT instead, e.g.: SELECT 'Read-only mode: cannot modify data' AS Error;");
                sb.AppendLine("Prefer <CommandType>Text</CommandType>.");
            }

            sb.AppendLine();
            sb.AppendLine($"Epoch: {epoch} of {maxEpochs}");
            sb.AppendLine("=== Primary Goal ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(prompt) ? "(none provided)" : prompt.Trim());
            sb.AppendLine();

            sb.AppendLine("=== Previous EPISODIC ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(episodic) ? "(empty)" : episodic);
            sb.AppendLine();

            sb.AppendLine("=== Xml Xsd for SqlXmlRequest (STRICTLY conform to this) ===");
            sb.AppendLine(sqlXmlRequestXsd);
            sb.AppendLine();

            sb.AppendLine("=== Database Schema ===");
            sb.AppendLine(schemaXml);
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(lastQueryResultXml))
            {
                sb.AppendLine("=== LastQueryResult (optional context) ===");
                if (lastQueryResultXml.Length > MaximumLastQueryOutputLength)
                {
                    sb.AppendLine("The LastQueryResult was too long to include in full. Consider writing queries that don't return as much data until the final iteration. Here is a truncated version:");
                    sb.AppendLine(lastQueryResultXml.Substring(0, MaximumLastQueryOutputLength));
                }
                else
                {
                    sb.AppendLine(lastQueryResultXml);
                }
                sb.AppendLine();
            }

            sb.AppendLine("Return ONLY one well-formed <SqlXmlRequest>...</SqlXmlRequest> payload. No commentary.");
            return sb.ToString();
        }

        private async Task<string> BuildEpisodicAsync(
            string prior,
            string prompt,
            string queryInputXml,
            string queryResultXml,
            int epoch,
            CancellationToken ct)
        {
            var instr = new StringBuilder();

            instr.AppendLine("You are assisting a SQL agent.");
            instr.AppendLine("Instructions:");
            //instr.AppendLine("The Objective takes priority over other instructions.");
            instr.AppendLine("Given the context, write a StateOfProgress and a NextStep. The StateOfProgress is the episodic.");
            instr.AppendLine("Write a StateOfProgress stating what objectives are complete and what needs to be done.");
            instr.AppendLine("Write a NextStep that is a realistic step towards the objective.");
            instr.AppendLine("Do not write lengthy sql for the NextStep, write minimalist English specifications that provide all of the instructions needed to achieve the NextStep. Information in the LastQueryResult will be available along with those minimalist instructions when the sql is written so you do not need to restate the LastQueryResult info.");
            instr.AppendLine("Any information that does not belong in StateOfProgress or NextStep can be written in Miscellaneous.");
            instr.AppendLine("If the LastQueryResult indicates that the same error has occurred as is described in the PriorEpisodic then the agent may be in an endless loop, try a completely different approach in the NextStep.");
            instr.AppendLine();
            instr.AppendLine("The rest of the information below is context:");
            instr.AppendLine();

            instr.AppendLine($"--- Start Context ---");
            instr.AppendLine();
            instr.AppendLine("=== Objective ===");
            instr.AppendLine(string.IsNullOrWhiteSpace(prompt) ? "(none provided)" : prompt.Trim());

            instr.AppendLine();
            instr.AppendLine("=== PriorEpisodic (rolling) ===");
            instr.AppendLine(string.IsNullOrWhiteSpace(prior) ? "(empty)" : prior);

            instr.AppendLine();
            instr.AppendLine("=== LastQueryInput (echo) ===");
            instr.AppendLine(queryInputXml ?? string.Empty);

            instr.AppendLine();
            instr.AppendLine("=== LastQueryResult (echo) ===");
            instr.AppendLine(queryResultXml ?? string.Empty);

            Response response = await LLM.Query(instr.ToString());
            string llmOut = response.Result;
            ct.ThrowIfCancellationRequested();

            return llmOut;
        }

        private static string SummarizeCritique(string prompt, string inXml, string outXml)
            => "Compared intent vs result; note mismatches, nulls, and row counts. (Implement finer diff if needed.)";

        private static string DerivePlanSnippet(string? prior, string outXml)
            => "Goal subgoals updated. If rows missing, add targeted SELECT/CREATE/UPDATE next.";

        private static string ExtractNextStepHint(string inXml)
            => "Prepare minimal parameterized SQL aligned with the strict XSD; prefer Query unless DDL/DML required.";



        private static bool IsSuccess(string execResultXml)
        {
            // Looks for Result success="true"
            return execResultXml.IndexOf("success=\"true\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Lightweight "done?" check. We ask the LLM to respond with &lt;Done&gt;true/false&lt;/Done&gt;.
        /// If the tag can't be parsed, we default to false and keep iterating.
        /// </summary>
        private async Task<bool> IsCompleteAsync(
            string prompt,
            string episodic,
            string schemaXml,
            string lastExecXml,
            int epoch,
            CancellationToken ct)
        {
            var sb = new StringBuilder(32 * 1024);
            sb.AppendLine("All of the objectives stated by the Prompt and only the objectives described by the Prompt need to be met in order to be Done.");
            sb.AppendLine("Respond with ONLY <Done>true</Done> or <Done>false</Done>.");
            sb.AppendLine($"Epoch just finished: {epoch}");
            sb.AppendLine("=== Prompt ===");
            sb.AppendLine(prompt);
            sb.AppendLine("=== Episodic ===");
            sb.AppendLine(episodic);
            sb.AppendLine("=== Schema ===");
            sb.AppendLine(schemaXml);
            sb.AppendLine();
            sb.AppendLine("=== LastExecutionResult ===");
            sb.AppendLine(lastExecXml);

            Response response = await LLM.Query(sb.ToString());
            var m = Regex.Match(response.Result ?? string.Empty, @"<Done>\s*(true|false)\s*</Done>", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            return string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFinalStateXml(string schemaXml, string? lastQueryResultXml, int epoch)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("=== Final State ===");
            sb.AppendLine("MyndSprout has stopped. Here is the final state of the short term memory.");
            sb.AppendLine("<ShortTermMemory>");
            if (lastQueryResultXml == "" || lastQueryResultXml == null)
            {
                sb.AppendLine("No data.");
            }
            else
            {
                sb.AppendLine(lastQueryResultXml);
            }
            sb.AppendLine("</ShortTermMemory>");
            sb.AppendLine("Epoch: " + epoch);
            sb.AppendLine("MyndSprout has stopped.");
            return sb.ToString();
        }

        public async Task<string> BuildFinalStateAsync(
            string inputPrompt,
            string existingResponse,
            string? stateOfProgress = null,
            CancellationToken ct = default)
        {
            string prompt = CreateFinishingPrompt(inputPrompt, existingResponse, stateOfProgress);

            Response response = await LLM.Query(prompt);

            return response.Result + Environment.NewLine + "MyndSprout has stopped.";
        }

        private static string CreateFinishingPrompt(string inputPrompt, string existingResponse, string? sop)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a precise assistant. Produce a final answer that meets the objectives in the user prompt. Respond in .txt, not .md.");
            sb.AppendLine();
            sb.AppendLine("=== USER PROMPT OBJECTIVES ===");
            sb.AppendLine(inputPrompt.Trim());
            sb.AppendLine();
            sb.AppendLine("=== EXISTING RESPONSE (DRAFT) ===");
            sb.AppendLine(existingResponse.Trim());
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(sop))
            {
                sb.AppendLine("=== STATE OF PROGRESS ===");
                sb.AppendLine("Summarize what is complete, what remains, and propose the next realistic step.");
                sb.AppendLine(sop!.Trim());
                sb.AppendLine();
            }

            sb.AppendLine("=== INSTRUCTIONS ===");
            sb.AppendLine("- Ensure the final answer directly satisfies the objectives in the USER PROMPT.");
            sb.AppendLine("- Improve clarity, fill gaps, and resolve inconsistencies in the existing response.");
            sb.AppendLine("- Keep any code blocks intact and runnable if present.");
            sb.AppendLine("- If uncertain, state the uncertainty and suggest the next step.");
            sb.AppendLine("- Output only the final answer (no extra meta commentary).");
            return sb.ToString();
        }

        public static async Task ClearEpisodicsAsync(SqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            const string sql = "DELETE FROM dbo.Episodics;";

            using var cmd = new SqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        // 1) Change your loader to be project-scoped (and allow null to mean "any"):
        private async Task<EpisodicRecord?> LoadMostRecentEpisodicRecordAsync(CancellationToken ct, int? projectId = 1)
        {
            string sql = @"
        SELECT TOP (1)
              [EpisodeId],
              [EpochIndex],
              [Time],
              [PrepareQueryPrompt],
              [QueryInput],
              [QueryResult],
              [EpisodicText],
              [DatabaseSchema],
              [ProjectId]
        FROM dbo.Episodics
        /**where**/
        ORDER BY [EpochIndex] DESC, [Time] DESC;";

            if (projectId.HasValue)
                sql = sql.Replace("/**where**/", "WHERE [ProjectId] = @pid");
            else
                sql = sql.Replace("/**where**/", "");

            using var cmd = new SqlCommand(sql, _sql.Database);
            if (projectId.HasValue) cmd.Parameters.AddWithValue("@pid", projectId.Value);

            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            if (!await reader.ReadAsync(ct)) return null;

            return new EpisodicRecord
            {
                EpisodeId = reader.GetGuid(reader.GetOrdinal("EpisodeId")),
                EpochIndex = reader.GetInt32(reader.GetOrdinal("EpochIndex")),
                Time = reader.GetDateTime(reader.GetOrdinal("Time")),
                PrepareQueryPrompt = reader["PrepareQueryPrompt"] as string ?? "",
                QueryInput = reader["QueryInput"] as string ?? "",
                QueryResult = reader["QueryResult"] as string ?? "",
                EpisodicText = reader["EpisodicText"] as string ?? "",
                DatabaseSchema = reader["DatabaseSchema"] as string ?? "",
                ProjectId = reader.GetInt32(reader.GetOrdinal("ProjectId"))
            };
        }


    }
}

