using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string filePath = "dane_nowe.json";
        var parser = new DataParserModule();
        UniversityData universityData;

        try
        {
            Console.WriteLine("Wczytywanie i walidacja pliku JSON...");
            universityData = parser.ParseAndValidate(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd Modułu 1: {ex.Message}");
            return;
        }

        var llmAnalyzer = new LlmPreferenceAnalyzer();
        Console.WriteLine($"\nWysyłam zapytanie do LLM dla {universityData.Instructors.Count} prowadzących...");

        var extractedPreferences = await llmAnalyzer.ExtractAllPreferencesAsync(universityData.Instructors);

        Console.WriteLine("\nWyniki z LLM:");
        foreach (var instructor in universityData.Instructors)
        {
            if (extractedPreferences.TryGetValue(instructor.Id, out var pref))
            {
                instructor.ParsedPreferences = pref;
                Console.WriteLine($"- {instructor.Name}: Dni: {string.Join(", ", pref.PreferredDays)} | Godz: {pref.PreferredHoursStart}-{pref.PreferredHoursEnd}");
            }
            else
            {
                Console.WriteLine($"- {instructor.Name}: LLM nie zwrócił danych dla tego ID.");
            }
        }

        // Zapisanie kompletnego obiektu uczelni do pliku wyjściowego
        string outputFilePath = "wynik_preferencje.json";
        string finalJson = JsonSerializer.Serialize(universityData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(outputFilePath, finalJson);
        Console.WriteLine($"\nGotowe preferencje oraz dane strukturalne zapisano do pliku: {outputFilePath}");
    }
}