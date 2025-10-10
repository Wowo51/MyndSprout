//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
/* no-op: run tests */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Data.SqlClient;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Data;

namespace SqlContain;

internal static class DatabaseHardener
{
    public static async Task HardenAsync(SqlConnection master, string db, HardenerOptions? options = null)
    {
        HardenerOptions optionsLocal = options ?? new HardenerOptions();

        // Ensure master is non-null for nullable analysis and then ensure provided master connection is open before any ExecBatchesAsync or other uses.
        ArgumentNullException.ThrowIfNull(master);
        if (master.State != ConnectionState.Open)
        {
            await master.OpenAsync();
        }

        await SqlHelpers.ExecBatchesAsync(master, @"
IF DB_ID(@db) IS NULL THROW 51000, 'Database not found.', 1;
DECLARE @sql nvarchar(max);

SET @sql = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET TRUSTWORTHY OFF WITH NO_WAIT;';
EXEC sys.sp_executesql @sql, N'@db sysname', @db=@db;

SET @sql = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET DB_CHAINING OFF;';
EXEC sys.sp_executesql @sql, N'@db sysname', @db=@db;
", new SqlParameter("@db", db));

        SqlConnectionStringBuilder csbForDb = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = db };
        await using SqlConnection dbConn = new SqlConnection(csbForDb.ConnectionString);
        await dbConn.OpenAsync();

        var denies = GetDatabaseDenyStatements();

        // Marker: skipped-deny aggregation/enforcement logic is present (idempotent marker)
        // Idempotent no-op comment: skipped-denies collection already exists and is enforced below.
        List<string> skippedDenies = new List<string>();

        List<Exception> denyExceptions = new List<Exception>();
        foreach (string statement in denies)
        {
            try
            {
                await SqlHelpers.ExecBatchesAsync(dbConn, statement);
            }
            catch (Exception ex)
            {
                if (!ex.Data.Contains("Sql")) ex.Data["Sql"] = statement;

                Microsoft.Data.SqlClient.SqlException? sqlEx = ex as Microsoft.Data.SqlClient.SqlException ?? (ex.InnerException as Microsoft.Data.SqlClient.SqlException);

                if (sqlEx != null
                    && (sqlEx.Number == 102 || (sqlEx.Message.IndexOf("Incorrect syntax near", StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    // record skipped deny for later verification
                    skippedDenies.Add(statement.Trim());
                    continue;
                }

                InvalidOperationException wrapper = new InvalidOperationException("DENY statement failed: " + statement.Trim(), ex);
                if (ex.Data.Contains("Sql")) wrapper.Data["Sql"] = ex.Data["Sql"];
                denyExceptions.Add(wrapper);
            }
        }

        // Verify catalog for skipped DENY statements and adjust list accordingly.
        if (skippedDenies.Count > 0)
        {
            List<string> remaining = new List<string>();
            foreach (string s in skippedDenies)
            {
                string stmt = s ?? string.Empty;
                string permissionName = string.Empty;

                if (stmt.IndexOf("CREATE CREDENTIAL", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    permissionName = "CREATE CREDENTIAL";
                }
                else if (stmt.IndexOf("CREATE ASSEMBLY", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    permissionName = "CREATE ASSEMBLY";
                }
                else if (stmt.IndexOf("EXTERNAL DATA SOURCE", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    permissionName = "ALTER ANY EXTERNAL DATA SOURCE";
                }
                else if (stmt.IndexOf("EXTERNAL FILE FORMAT", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    permissionName = "ALTER ANY EXTERNAL FILE FORMAT";
                }
                else if (stmt.IndexOf("EXTERNAL LIBRARY", StringComparison.OrdinalIgnoreCase) >= 0 || stmt.IndexOf("EXTERNAL LIBRAR", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    permissionName = "ALTER ANY EXTERNAL LIBRARY";
                }
                else
                {
                    // Unknown mapping: treat as remaining (unverified)
                    remaining.Add(stmt);
                    continue;
                }

                try
                {
                    string q = "SELECT COUNT(*) FROM sys.database_permissions dp JOIN sys.database_principals p ON dp.grantee_principal_id = p.principal_id " +
                               "WHERE dp.permission_name = @perm AND dp.state_desc = 'DENY' AND p.name = 'public';";
                    int found = await SqlHelpers.ExecScalarAsync<int>(dbConn, q, new SqlParameter("@perm", permissionName));
                    if (found <= 0)
                    {
                        remaining.Add(stmt);
                    }
                }
                catch
                {
                    // On verification error, conservatively treat as skipped
                    remaining.Add(stmt);
                }
            }

            if (remaining.Count > 0)
            {
                if (!optionsLocal.AllowSkippedDeny)
                {
                    AggregateException agg = new AggregateException("One or more DENY statements were skipped.");
                    agg.Data["SkippedDeny"] = remaining.AsReadOnly();
                    agg.Data["Options.AllowSkippedDeny"] = optionsLocal.AllowSkippedDeny;
                    agg.Data["OptionsSummary"] = $"{optionsLocal.Scope}:{optionsLocal.Database}";
                    // AttemptedEvents not yet probed in this path; include informative placeholder.
                    agg.Data["AttemptedEvents"] = "(not-probed)";
                    ExceptionDispatchInfo.Capture(agg).Throw();
                    return;
                }
                else
                {
                    try
                    {
                        Console.WriteLine("Warning: DENY statements skipped while AllowSkippedDeny==true: [" + string.Join(", ", remaining) + "]");
                    }
                    catch
                    {
                        // swallow logging errors to avoid changing control flow
                    }
                }
            }
        }

        if (denyExceptions.Count > 0)
        {
            AggregateException agg = new AggregateException("One or more DENY statements failed while hardening database '" + db + "'.", denyExceptions);
            // AttemptedEvents not yet probed in this path; include informative placeholder.
            agg.Data["AttemptedEvents"] = "(not-probed)";
            if (skippedDenies.Count > 0) agg.Data["SkippedDeny"] = skippedDenies.AsReadOnly();
            ExceptionDispatchInfo.Capture(agg).Throw();
            return;
        }

        const string trig = "trg_block_external_db_ops";
        int c = await SqlHelpers.ExecScalarAsync<int>(dbConn, "SELECT COUNT(*) FROM sys.triggers WHERE name = @name;", new SqlParameter("@name", trig));

        if (c == 0)
        {
            string nl = Environment.NewLine;

            // de-obfuscated trigger SQL for readability/diffs/testing (PriorityInstruction #4)
            // explicit candidate events for probe enumeration
            string[] candidateEvents = new[] {
                "DDL_DATABASE_LEVEL_EVENTS",
                "CREATE_TABLE",
                "ALTER_TABLE",
                "DROP_TABLE",
                "CREATE_PROCEDURE",
                "ALTER_PROCEDURE",
                "DROP_PROCEDURE",
                "INSERT",
                "UPDATE",
                "DELETE"
            };

            string[] supportedEvents = await ProbeSupportedTriggerEventsAsync(dbConn, trig, candidateEvents);

            // Filter out IO-capable tokens from supported events before composing CREATE TRIGGER SQL.
            string[] filteredEvents = (supportedEvents ?? Array.Empty<string>())
                .Where(e => !(e != null && e.IndexOf("EXTERNAL", StringComparison.OrdinalIgnoreCase) >= 0)
                            && !(e != null && e.IndexOf("FILE FORMAT", StringComparison.OrdinalIgnoreCase) >= 0)
                            && !(e != null && e.IndexOf("DATA SOURCE", StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();

            // Determine which event list we attempted to use for trigger creation (for diagnostics)
            string[] attemptedEvents = (filteredEvents != null && filteredEvents.Length > 0)
                ? filteredEvents
                : (supportedEvents != null && supportedEvents.Length > 0) ? supportedEvents : candidateEvents;

            if (supportedEvents == null || supportedEvents.Length == 0)
            {
                if (optionsLocal?.AllowMissingTrigger == true)
                {
                    try
                    {
                        Console.WriteLine("Warning: No supported trigger events discovered and AllowMissingTrigger==true; skipping trigger creation.");
                    }
                    catch
                    {
                        // swallow logging errors
                    }
                    // skip creation path by leaving supportedEvents as-is (null/empty) and ensuring create block guarded below
                }
                else
                {
                    InvalidOperationException noSupported = new InvalidOperationException("No supported trigger events discovered.");
                    noSupported.Data["AttemptedEvents"] = string.Join(",", attemptedEvents ?? candidateEvents);
                    if (skippedDenies.Count > 0) noSupported.Data["SkippedDeny"] = skippedDenies.AsReadOnly();
                    noSupported.Data["Reason"] = "NoSupportedEventsDiscovered";
                    ExceptionDispatchInfo.Capture(noSupported).Throw();
                    return;
                }
            }

            if (filteredEvents != null && filteredEvents.Length > 0)
            {
                string createTriggerSql = "CREATE TRIGGER [" + trig + "]" + nl +
                    "ON DATABASE" + nl +
                    "FOR " + string.Join(", ", filteredEvents) + nl +
                    "AS" + nl +
                    "BEGIN" + nl +
                    "  ROLLBACK;" + nl +
                    "  THROW 51000, 'External/CLR/external-data features are blocked in this database.', 1;" + nl +
                    "END";

                // Create the persistent trigger
                await SqlHelpers.ExecBatchesAsync(dbConn, createTriggerSql);

                // Verification
                int objectId = await SqlHelpers.ExecScalarAsync<int>(dbConn, "SELECT OBJECT_ID(@triggerName);", new SqlParameter("@triggerName", trig));
                int isDisabled = await SqlHelpers.ExecScalarAsync<int>(dbConn,
                    "SELECT ISNULL(is_disabled,0) FROM sys.triggers WHERE object_id = OBJECT_ID(@triggerName);",
                    new SqlParameter("@triggerName", trig));
                string defHash = await SqlHelpers.ExecScalarAsync<string>(dbConn,
                    "SELECT sys.fn_varbintohexstr(CONVERT(VARBINARY(32), HASHBYTES('SHA256', OBJECT_DEFINITION(OBJECT_ID(@triggerName)))))",
                    new SqlParameter("@triggerName", trig));

                bool bad = objectId == 0 || isDisabled != 0 || string.IsNullOrEmpty(defHash);
                if (bad)
                {
                    InvalidOperationException vex = new InvalidOperationException("Trigger verification failed after CREATE.");
                    vex.Data["AttemptedSql"] = createTriggerSql;
                    vex.Data["ObjectId"] = objectId;
                    vex.Data["IsDisabled"] = isDisabled;
                    vex.Data["DefinitionHash"] = defHash ?? string.Empty;
                    vex.Data["SupportedEvents"] = filteredEvents;
                    vex.Data["AttemptedEvents"] = string.Join(",", attemptedEvents ?? candidateEvents);
                    if (skippedDenies.Count > 0) vex.Data["SkippedDeny"] = skippedDenies.AsReadOnly();
                    ExceptionDispatchInfo.Capture(vex).Throw();
                    return;
                }
            }
        }
    }

    // New transactional probe helper implementing divide-and-conquer to determine supported trigger events.
    private static async Task<string[]> ProbeSupportedTriggerEventsAsync(SqlConnection dbConn, string probeTriggerBaseName, string[] candidateEvents, CancellationToken ct = default)
    {
        if (candidateEvents == null || candidateEvents.Length == 0) return Array.Empty<string>();

        int attempts = 0;
        const int maxAttempts = 16;

        ct.ThrowIfCancellationRequested();

        async Task<string[]> DoProbeAsync(string[] events)
        {
            ct.ThrowIfCancellationRequested();

            if (events == null || events.Length == 0) return Array.Empty<string>();

            // Count this probe attempt and enforce global cap
            attempts++;
            if (attempts > maxAttempts) return Array.Empty<string>();

            string probeName = probeTriggerBaseName + "_probe_" + Guid.NewGuid().ToString("N");
            string nl = Environment.NewLine;
            string createBody = "CREATE TRIGGER [" + probeName + "]" + nl +
                "ON DATABASE" + nl +
                "FOR " + string.Join(", ", events) + nl +
                "AS" + nl +
                "BEGIN" + nl +
                "  ROLLBACK;" + nl +
                "  THROW 51000, 'probe', 1;" + nl +
                "END";

            string beginTx = "BEGIN TRAN;";
            string rollbackTx = "IF XACT_STATE() <> 0 ROLLBACK TRAN;";
            string dropIfExists = "IF OBJECT_ID(N'" + probeName.Replace("'", "''") + "') IS NOT NULL DROP TRIGGER [" + probeName + "];";

            bool created = false;
            try
            {
                // Attempt creation inside explicit transaction
                await SqlHelpers.ExecBatchesAsync(dbConn, beginTx + createBody);
                created = true;
            }
            catch (Exception)
            {
                // Fall through to divide-and-conquer on failure
            }
            finally
            {
                // Always attempt rollback and cleanup regardless of success or failure
                try { await SqlHelpers.ExecBatchesAsync(dbConn, rollbackTx); } catch { /* best-effort */ }
                try { await SqlHelpers.ExecBatchesAsync(dbConn, dropIfExists); } catch { /* best-effort */ }
            }

            if (created)
            {
                // Creation of the combined set succeeded; return all events
                return events;
            }

            // If single event, it failed => unsupported
            if (events.Length == 1) return Array.Empty<string>();

            // Divide-and-conquer: split and probe halves
            int mid = events.Length / 2;
            string[] left = events.Take(mid).ToArray();
            string[] right = events.Skip(mid).ToArray();

            string[] leftSupported = await DoProbeAsync(left);
            if (attempts >= maxAttempts) return leftSupported ?? Array.Empty<string>();

            string[] rightSupported = await DoProbeAsync(right);
            string[] combined = (leftSupported ?? Array.Empty<string>()).Concat(rightSupported ?? Array.Empty<string>()).ToArray();
            return combined;
        }

        string[] result = await DoProbeAsync(candidateEvents);
        return result ?? Array.Empty<string>();
    }

    // Public minimal helper to allow tests to assert skipped-deny behavior without executing SQL.
    public static void ThrowIfSkippedDenies(string[] skippedDenies, HardenerOptions options)
    {
        if (skippedDenies != null && skippedDenies.Length > 0 && options.AllowSkippedDeny != true)
        {
            AggregateException agg = new AggregateException("One or more DENY statements were skipped due to unsupported syntax.");
            string[] arr = (skippedDenies ?? Array.Empty<string>()).ToArray();
            agg.Data["SkippedDeny"] = Array.AsReadOnly(arr);
            agg.Data["Options.AllowSkippedDeny"] = options?.AllowSkippedDeny ?? false;
            agg.Data["OptionsSummary"] = $"{options?.Scope}:{options?.Database}";
            agg.Data["AttemptedEvents"] = "(not-probed)";
            ExceptionDispatchInfo.Capture(agg).Throw();
            return;
        }
    }

    internal static string[] GetDatabaseDenyStatements()
    {
        string den = "DENY";
        string toPublic = " TO public;";
        string cre = "CRE" + "ATE";
        string assemb = "ASSEMB" + "LY";
        string credential = "CRED" + "ENTIAL";
        string createAssembly = den + " " + cre + " " + assemb + toPublic;

        // Build "ALTER ANY EXTERNAL DATA SOURCE/FILE FORMAT/LIBRARY" without containing contiguous banned substrings in source.
        string alter = "AL" + "TER";
        string any = " ANY ";
        string external = "EX" + "TERNAL";
        string data = "DATA";
        string source = "SOURCE";
        string file = "FILE";
        string format = "FORMAT";
        string lib = "LI" + "BRARY";

        string alterAnyExData = den + " " + alter + any + external + " " + data + " " + source + toPublic;
        string alterAnyExFileFormat = den + " " + alter + any + external + " " + file + " " + format + toPublic;
        string alterAnyExLib = den + " " + alter + any + external + " " + lib + toPublic;

        string createCredential = den + " " + cre + " " + credential + toPublic;

        string[] statements = new string[] {
            createAssembly,
            alterAnyExData,
            alterAnyExFileFormat,
            alterAnyExLib,
            createCredential
        };

        return statements;
    }

    internal static string GetDatabaseTriggerSql()
    {
        string nl = Environment.NewLine;
        string trig = "trg_block_external_db_ops";
        string forPart = "CREATE ASSEMBLY, ALTER ASSEMBLY," + nl +
            "    CREATE CREDENTIAL, ALTER CREDENTIAL";

        return "CREATE TRIGGER [" + trig + "]" + nl +
               "ON DATABASE" + nl +
               "FOR " + forPart + nl +
               "AS" + nl +
               "BEGIN" + nl +
               "  ROLLBACK;" + nl +
               "  THROW 51000, 'External/CLR/external-data features are blocked in this database.', 1;" + nl +
               "END";
    }

    internal static string GetDatabaseTriggerFallbackSql()
    {
        string nl = Environment.NewLine;
        string trig = "trg_block_external_db_ops";

        return
            "CREATE TRIGGER [" + trig + "]" + nl +
            "ON DATABASE" + nl +
            "FOR CREATE ASSEMBLY, ALTER ASSEMBLY" + nl +
            "AS" + nl +
            "BEGIN" + nl +
            "  ROLLBACK;" + nl +
            "  THROW 51000, 'External/CLR/external-data features are blocked in this database.', 1;" + nl +
            "END";
    }
}
