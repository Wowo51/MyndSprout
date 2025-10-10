//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2024.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

namespace OpenAILLM
{
 #pragma warning disable OPENAI001
    public class LLM
    {
        public static double AccumulatedCost = 0;

        /// <summary>
        /// The PID of the process used for the last LLM call attempt.
        /// </summary>
        public static int? LastPid { get; private set; } = null;

        /// <summary>
        /// If set to true, will kill the process on timeout. Default is false.
        /// </summary>
        public static bool KillOnTimeout { get; set; } = false;

        /// <summary>
        /// Timeout for LLM calls. Default is 30 seconds.
        /// </summary>
        public static TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);


        public static string OpenAiKeyPath { get; set; } = string.Empty;

        private readonly record struct Prices(double PromptPerM, double CompletionPerM);


        /// <summary>
        /// Calls the LLM API, ensuring a timeout and robust error reporting.
        /// </summary>
        public static async Task<(string, double)> Query(
            string query,
            string modelKey,
            ChatReasoningEffortLevel? level
        )
        {
            if (string.IsNullOrWhiteSpace(OpenAiKeyPath) || !File.Exists(OpenAiKeyPath))
            {
                return ("Error calling OpenAI LLM: OpenAiKeyPath is not set or file missing.", 0);
            }

            string apiKey = File.ReadAllText(OpenAiKeyPath).Trim();


            int count = apiKey.Length;
            int? pid = null;
            try
            {
                pid = Process.GetCurrentProcess().Id;
                LastPid = pid;
            }
            catch { /* ignore */ }

            try
            {
                var client = new OpenAIResponseClient(model: modelKey, apiKey: apiKey);
                
                var llmTask = client.CreateResponseAsync(userInputText: query);

                var completedTask = await Task.WhenAny(llmTask, Task.Delay(Timeout));
                if (completedTask != llmTask)
                {
                    HandleTimeout(pid);
                    return ($"Error: LLM call timed out after {Timeout.TotalSeconds} seconds. PID={pid}.", 0);
                }

                var response = await llmTask; // Should be completed
                var sb = new StringBuilder();
                foreach (var item in response.Value.OutputItems)
                {
                    if (item is MessageResponseItem msgItem)
                    {
                        foreach (var part in msgItem.Content)
                            sb.Append(part.Text);
                    }
                }
                double cost = Cost(response);
                AccumulatedCost += cost;
                return (sb.ToString(), cost);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return ($"Error calling OpenAI LLM (PID={pid}): {ex.Message}", 0);
            }
        }

        /// <summary>
        /// Calls the LLM API with web search, ensuring a timeout and robust error reporting.
        /// </summary>
        public static async Task<(string, double)> SearchAsync(
            string query,
            string modelKey
        )
        {
            if (string.IsNullOrWhiteSpace(OpenAiKeyPath) || !File.Exists(OpenAiKeyPath))
            {
                return ("Error calling OpenAI LLM: OpenAiKeyPath is not set or file missing.", 0);
            }

            string apiKey = File.ReadAllText(OpenAiKeyPath);
            int? pid = null;
            try
            {
                pid = Process.GetCurrentProcess().Id;
                LastPid = pid;
            }
            catch { /* ignore */ }

            try
            {
                var client = new OpenAIResponseClient(model: modelKey, apiKey: apiKey);

                var llmTask = client.CreateResponseAsync(
                    userInputText: query,
                    new ResponseCreationOptions
                    {
                        Tools = { ResponseTool.CreateWebSearchTool() }
                    });

                var completedTask = await Task.WhenAny(llmTask, Task.Delay(Timeout));
                if (completedTask != llmTask)
                {
                    HandleTimeout(pid);
                    return ($"Error: LLM call timed out after {Timeout.TotalSeconds} seconds. PID={pid}.", 0);
                }

                var response = await llmTask;
                var sb = new StringBuilder();
                foreach (var item in response.Value.OutputItems)
                {
                    if (item is MessageResponseItem msgItem)
                    {
                        foreach (var part in msgItem.Content)
                            sb.Append(part.Text);
                    }
                }
                double cost = Cost(response) + 0.01;
                AccumulatedCost += cost;
                return (sb.ToString(), cost);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return ($"Error calling OpenAI LLM (PID={pid}): {ex.Message}", 0);
            }
        }

        private static void HandleTimeout(int? pid)
        {
            if (KillOnTimeout && pid.HasValue)
            {
                try
                {
                    var proc = Process.GetProcessById(pid.Value);
                    proc.Kill();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not kill process {pid}: {ex}");
                }
            }
        }

        public static double Cost(OpenAIResponse response)
        {
            if (response is null)
                throw new ArgumentNullException(nameof(response));

            var usage = response.Usage
                        ?? throw new InvalidOperationException("Usage block is missing.");

            int promptTokens = usage.InputTokenCount;
            int completionTokens = usage.OutputTokenCount;

            return Cost(promptTokens, completionTokens, response.Model);
        }

        public static double Cost(ChatCompletion response)
        {
            if (response is null)
                throw new ArgumentNullException(nameof(response));

            var usage = response.Usage
                        ?? throw new InvalidOperationException("Usage block is missing.");

            int promptTokens = usage.InputTokenCount;
            int completionTokens = usage.OutputTokenCount;

            return Cost(promptTokens, completionTokens, response.Model);
        }

        public static double Cost(double inputTokenCount, double outputTokenCount, string modelKey)
        {
            if (!TryGetPrices(modelKey, out var prices))
            {
                var normalized = NormalizeModelKey(modelKey);
                throw new NotSupportedException(
                    $"No pricing data for model '{modelKey}' (normalized='{normalized}'). " +
                    $"If this is a new/variant model id, add a regex to PriceCatalog.");
            }

            const double oneMillion = 1_000_000d;
            double cost =
                  (inputTokenCount * prices.PromptPerM) / oneMillion
                + (outputTokenCount * prices.CompletionPerM) / oneMillion;

            return Math.Round(cost, 6);
        }

        private static readonly (System.Text.RegularExpressions.Regex rx, Prices p)[] PriceCatalog =
{
            // GPT-5 family
            (Rx("^gpt-5$"),           new Prices(10.00, 30.00)),
            (Rx("^gpt-5-mini$"),      new Prices( 0.60,  2.40)),
            (Rx("^gpt-5-nano$"),      new Prices( 0.10,  0.40)),

            // GPT-4.1 family
            (Rx("^gpt-4\\.1$"),       new Prices( 2.00,  8.00)),
            (Rx("^gpt-4\\.1-mini$"),  new Prices( 0.40,  1.60)),
            (Rx("^gpt-4\\.1-nano$"),  new Prices( 0.10,  0.40)),

            // o3 / o4
            (Rx("^o3$"),              new Prices(10.00, 40.00)),
            (Rx("^o4-mini$"),         new Prices( 1.10,  4.40)),

            // 4o family (treat most 4o variants as 4o baseline unless you want finer tiers)
            (Rx("^gpt-4o$"),          new Prices( 5.00, 20.00)),
            (Rx("^gpt-4o-mini$"),     new Prices( 0.60,  2.40)),
            (Rx("^gpt-4o(-.*)?$"),    new Prices( 5.00, 20.00)), // realtime / audio / search-preview etc.

            // 3.5 for completeness
            (Rx("^gpt-3\\.5-turbo$"), new Prices( 0.50,  1.50)),
        };

        private static System.Text.RegularExpressions.Regex Rx(string pattern)
            => new(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        // Map many possible incoming ids to a stable base id and strip date suffixes like -2025-05-01
        private static string NormalizeModelKey(string modelKey)
        {
            if (string.IsNullOrWhiteSpace(modelKey)) return string.Empty;
            var mk = modelKey.Trim().ToLowerInvariant();

            // strip ISO date suffixes e.g., "-2025-05-01"
            mk = System.Text.RegularExpressions.Regex.Replace(mk, "-20\\d{2}-\\d{2}-\\d{2}$", "");

            // common aliases
            mk = mk switch
            {
                "gpt-4-mini" => "gpt-4.1-mini",
                "gpt-4-nano" => "gpt-4.1-nano",
                "gpt-4.1-nano" => "gpt-4.1-nano",   // just to be explicit
                "gpt-4o-2024-05-13" => "gpt-4o",
                "gpt-4.1-2025-04-14" => "gpt-4.1",
                "gpt-4.1-mini-2025-04-14" => "gpt-4.1-mini",
                "o4-mini-2025-04-16" => "o4-mini",
                _ => mk
            };

            return mk;
        }

        private static bool TryGetPrices(string modelKey, out Prices prices)
        {
            var mk = NormalizeModelKey(modelKey);
            foreach (var (rx, p) in PriceCatalog)
            {
                if (rx.IsMatch(mk)) { prices = p; return true; }
            }
            prices = default;
            return false;
        }

    }
}