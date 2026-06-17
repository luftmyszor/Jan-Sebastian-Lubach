using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Backend.LlmParser
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Now we expect TWO arguments
            if (args.Length < 2)
            {
                Console.Error.WriteLine("ERROR: Missing arguments. Usage: Backend.LlmParser.exe <input_json> <output_json>");
                Environment.Exit(1);
            }

            string inputFilePath = args[0];
            string outputFilePath = args[1];

            if (!File.Exists(inputFilePath))
            {
                Console.Error.WriteLine($"ERROR: Input file not found at: {inputFilePath}");
                Environment.Exit(1);
            }

            try
            {
                Console.Error.WriteLine($"[C#] Starting LLM pipeline for: {inputFilePath}");

                // 1. Parse the incoming JSON
                var parser = new DataParserModule();
                var universityData = parser.ParseAndValidate(inputFilePath);
                Console.Error.WriteLine("[C#] Data parsed successfully.");

                // 2. Run the LLM Preference Analyzer
                var llmAnalyzer = new LlmPreferenceAnalyzer(offlineMode: false);
                Console.Error.WriteLine("[C#] Extracting preferences via LLM...");
                var extractedPreferences = await llmAnalyzer.ExtractAllPreferencesAsync(universityData.Instructors);

                // 3. Inject preferences back into the data model
                foreach (var inst in universityData.Instructors)
                {
                    if (extractedPreferences.TryGetValue(inst.Id, out var prefs))
                    {
                        inst.ParsedPreferences = prefs;
                    }
                }

                // 4. Serialize the final data
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                string finalJsonOutput = JsonSerializer.Serialize(universityData, jsonOptions);
                
                // 5. Write directly to the output file!
                File.WriteAllText(outputFilePath, finalJsonOutput);
                
                // Print a success message so Python knows it worked
                Console.WriteLine($"SUCCESS: File saved to {outputFilePath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[C# FATAL ERROR] {ex.Message}\n{ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}