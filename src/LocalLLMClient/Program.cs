using System;
using System.Threading.Tasks;
using LocalLLM;

namespace LocalLLMClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== LM Studio Chat Console ===");
            Console.WriteLine("Type 'exit' to quit.\n");

            // Change this model name to match what LM Studio shows
            var client = new LmStudioClient(model: "openai/gpt-oss-20b");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("You: ");
                Console.ResetColor();

                string? prompt = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(prompt)) continue;
                if (prompt.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                try
                {
                    string response = await client.QueryAsync(prompt);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Assistant: " + response + "\n");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}\n");
                    Console.ResetColor();
                }
            }
        }
    }
}
