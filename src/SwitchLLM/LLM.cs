//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2025.
using OpenAI;
using OpenAILLM;
using OpenRouter;
using OpenAI.Chat;
using LocalLLM;

namespace SwitchLLM
{
    public class LLM
    {
        //Developer,Key,InputCost,Outputcost,InputTokens,OutputTokens,Misc.
        public static string DefaultModelDescription = "Local,openai/gpt-oss-20".Trim();
        public static string DefaultSearchModelDescription = "OpenAI,gpt-5-mini,$.25,$2,400000,128000".Trim();

        static LmStudioClient localClient = new LmStudioClient();

        public async static Task<Response> SearchQuery(string prompt)
        {
            var parts = DefaultSearchModelDescription.Split(',');
            var developer = parts[0];
            var modelKey = parts[1];
            string hoster = "";
            if (parts.Length >= 5)
            {
                hoster = parts[5];
            }
            (string response, double cost) = await OpenAILLM.LLM.SearchAsync(prompt, modelKey);
            return new Response(true, response + Environment.NewLine + "Cost=" + cost);
        }

        public async static Task<Response> Query(string prompt)
        {
            return await Query(prompt, DefaultModelDescription);
        }

        public async static Task<Response> Query(string prompt, string modelDescription)
        {
            var parts = modelDescription.Split(',');
            var developer = parts[0];
            var modelKey = parts[1];
            string hoster = "";
            if (parts.Length >= 5)
            {
                hoster = parts[5];
            }
            var additional = parts.Length > 6 ? parts[6] : null;

            switch (developer)
            {
                case "Local":
                    var localResponse = await localClient.QueryAsync(prompt);
                    return new Response(true, localResponse);

                case "OpenAI":
                    // Determine highLevel based on optional ReasoningLevel setting
                    bool highLevel = false;
                    if (additional != null && additional.StartsWith("ReasoningLevel"))
                    {
                        var setting = additional.Split('=')[1];
                        highLevel = setting != "Low";
                    }
                    return await QueryOpenAI(prompt, modelKey, highLevel);

                case "OpenRouter":
                    return await QueryOpenRouter(prompt, modelKey, hoster);

                default:
                    return new Response(false, $"Unknown developer '{developer}' in DefaultModelDescription");
            }
        }

#pragma warning disable OPENAI001
        public async static Task<Response> QueryOpenAI(string prompt, string modelKey, bool highLevel)
        {
            if (highLevel)
            {
                (string result, double cost) = await OpenAILLM.LLM.Query(prompt, modelKey, ChatReasoningEffortLevel.High);
                return new Response(true, result + Environment.NewLine + "Cost=" + cost);
            }
            else
            {
                (string result, double cost) = await OpenAILLM.LLM.Query(prompt, modelKey, ChatReasoningEffortLevel.Medium);
                return new Response(true, result + Environment.NewLine + "Cost=" + cost);
            }
        }

        public async static Task<Response> QueryOpenRouter(string prompt, string modelKey, string provider)
        {
            var (success, result, cost) = await OpenRouter.LLM.Query(prompt, modelKey, provider);
            return new Response(success, result);
        }

        public static List<string> ModelDescriptions()
        {
            return ModelSheet.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static List<string> SearchModelDescriptions()
        {
            return SearchModelSheet.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        //Developer,Key,InputCost,Outputcost,InputTokens,OutputTokens,Misc.
        public static string SearchModelSheet = @"
OpenAI,gpt-5,$1.25,$10,400000,128000
OpenAI,gpt-5-mini,$.25,$2,400000,128000
OpenAI,gpt-5-nano,$.05,$.40,400000,128000
OpenRouter,perplexity/sonar-reasoning-pro,$2,$8,128000,128000
OpenRouter,perplexity/sonar-pro,$3,$15,200000,8000
OpenAI,gpt-4o-search-preview-2025-03-11,$2.5,$10,128000,128000
OpenAI,gpt-4o-mini-search-preview-2025-03-11,$0.15,$0.60,128000,128000
OpenAI,gpt-4.1-2025-04-14,$2,$8,1000000,32000
OpenAI,gpt-4.1-mini-2025-04-14,$0.40,$1.60,1000000,32000";

        public static string ModelSheet = @"
GeminiCLI,GeminiCLI,$0.0,$0.0,1000000,66000
OpenAI,gpt-5,$1.25,$10,400000,128000
OpenAI,gpt-5-mini,$.25,$2,400000,128000
OpenAI,gpt-5-nano,$.05,$.40,400000,128000
OpenAI,o3-2025-04-16,$2,$8,200000,100000
OpenAI,gpt-4.1-2025-04-14,$2,$8,1000000,32000
OpenAI,gpt-4.1-mini-2025-04-14,$0.40,$1.60,1000000,32000
OpenAI,gpt-4.1-nano-2025-04-14,$0.10,$0.40,1000000,32000
OpenAI,o4-mini-2025-04-16,$1.10,$4.40,200000,100000,ReasoningLevel=High
OpenAI,o4-mini-2025-04-16,$1.10,$4.40,200000,100000,ReasoningLevel=Medium
OpenAI,o4-mini-2025-04-16,$1.10,$4.40,200000,100000,ReasoningLevel=Low
OpenRouter,google/gemini-2.5-pro,$1.25,$10,1000000,66000,Google
OpenRouter,google/gemini-2.5-flash,$0.30,$2.50,1000000,66000,Google
OpenRouter,google/gemini-2.5-flash-preview-05-20:thinking,$0.15,$3.50,1000000,66000,Google
OpenRouter,google/gemini-2.5-flash-preview-05-20,$0.15,$60,1000000,66000,Google
OpenRouter,google/gemini-2.0-flash-001,$0.10,$0.40,1000000,8000,Google
OpenRouter,google/gemini-2.5-flash-lite-preview-06-17,$0.10,$0.40,1000000,66000,google-vertex
OpenRouter,meta-llama/llama-4-maverick:free,$0.0,$0.0,128000,128000,baseten/fp16
OpenRouter,meta-llama/llama-4-scout,$0.0,$0.0,1280000,4000,Meta
OpenRouter,x-ai/grok-3-mini,$0.30,$0.50,131000,13000,xAI
OpenRouter,x-ai/grok-3,$3,$15,131000,13000,xAI
OpenRouter,qwen/qwen3-235b-a22b,$0.20,$0.80,128000,128000,NovitaAI
OpenRouter,qwen/qwen3-32b,$0.10,$0.45,128000,128000,NovitaAI
OpenRouter,arcee-ai/coder-large,$0.50,$0.80,33000,33000,NovitaAI
OpenRouter,microsoft/phi-4-reasoning-plus:free,$0.0,$0.0,33000,33000,Chutes
OpenRouter,microsoft/mai-ds-r1:free,$0.0,$0.0,164000,164000,Chutes
OpenRouter,deepseek/deepseek-chat-v3-0324:free,$0.0,$0.0,164000,164000,Chutes
OpenRouter,deepseek/deepseek-r1-0528:free,$0.0,$0.0,128000,128000,Targon
OpenRouter,deepseek/deepseek-r1-0528:free,$0.0,$0.0,128000,128000,Chutes
OpenRouter,nvidia/llama-3.1-nemotron-ultra-253b-v1:free,$0.0,$0.0,131000,131000,Chutes
OpenRouter,inception/mercury-coder-small-beta,$0.25,$1.0,131000,131000,Inception
OpenRouter,moonshotai/kimi-dev-72b:free,$0.0,$0.0,131000,131000,Chutes
OpenRouter,tngtech/deepseek-r1t-chimera:free,$0.0,$0.0,131000,131000,Chutes
OpenRouter,openrouter/cypher-alpha:free,$0.0,$0.0,1000000,10000,Stealth
OpenRouter,meta-llama/llama-3.3-70b-instruct,$0.038,$0.12,131000,131000,klusterai/fp8
OpenRouter,qwen/qwen-2.5-coder-32b-instruct,$0.06,$0.18,131000,131000,nebius/fp8
OpenRouter,google/gemma-3-27b-it,$0.10,$0.40,131000,131000,parasail/fp8
OpenRouter,google/gemma-3-4b-it,$0.02,$0.02,131000,131000,deepinfra/bf16
OpenRouter,mistralai/codestral-2501,$0.30,$0.90,262000,262000,mistral
OpenRouter,moonshotai/kimi-k2-0905,$0.30,$1.18,63000,63000,chutes/fp8
OpenRouter,openai/gpt-oss-120b,$0.073,$0.29,63000,63000,atlas-cloud/fp8
OpenRouter,x-ai/grok-4-fast:free,$0.0,$0.0,2M,30000,xai
Local,openai/gpt-oss-20
Local,qwen/qwen3-4b-thinking-2507
Local,microsoft/phi-4-reasoning-plus
OpenRouter,deepseek/deepseek-chat-v3.1:free,$0.0,$0.0,64000,64000,deepinfra/fp4
OpenRouter,deepseek/deepseek-chat-v3.1,$0.0,$0.0,64000,64000,chutes/fp8
OpenRouter,qwen/qwen3-coder:free,$0.0,$0.0,64000,64000,chutes/fp8
";

        //Anthropic,claude-sonnet-4-20250514,$3.00,$15.00,200000,64000
        //Anthropic,claude-opus-4-20250514,$15.00,$75.00,200000,64000

    }
}