using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class LlmPreferenceAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "";
    private readonly string _token = "";
    private readonly string _modelName = "SpeakLeash/bielik-11b-v3.0-instruct:Q4_K_M";
    private readonly bool _offlineMode;
    private readonly string _cacheFilePath = "llm_cache.json";

    public LlmPreferenceAnalyzer(bool offlineMode = false)
    {
        _offlineMode = offlineMode;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public async Task<Dictionary<string, InstructorPreferences>> ExtractAllPreferencesAsync(List<Instructor> instructors)
    {
        var finalDict = new Dictionary<string, InstructorPreferences>();
        if (instructors == null || !instructors.Any()) return finalDict;

        if (_offlineMode)
        {
            Console.WriteLine("Tryb offline. Generowanie domyślnych preferencji.");
            return GenerateDefaultBatch(instructors);
        }

        // 1. ZAPYTANIE O POSTĘP (INTERAKCJA Z UŻYTKOWNIKIEM)
        if (File.Exists(_cacheFilePath))
        {
            Console.WriteLine("\n[INFO] Znaleziono zapisany postęp z poprzedniej sesji (llm_cache.json).");
            Console.Write("Czy chcesz wczytać zapisane wyniki i kontynuować? (T - Tak / N - Nie, zacznij od nowa): ");

            string choice = Console.ReadLine()?.Trim().ToUpper();

            if (choice == "T")
            {
                try
                {
                    var cacheJson = File.ReadAllText(_cacheFilePath);
                    finalDict = JsonSerializer.Deserialize<Dictionary<string, InstructorPreferences>>(cacheJson) ?? new Dictionary<string, InstructorPreferences>();
                    Console.WriteLine($"Wczytano {finalDict.Count} gotowych wyników. Kontynuuję od miejsca przerwania...");
                }
                catch (Exception)
                {
                    Console.WriteLine("[OSTRZEŻENIE] Plik cache uszkodzony, zaczynam od nowa.");
                }
            }
            else
            {
                Console.WriteLine("Zaczynam analizę od nowa (stary plik postępu zostanie nadpisany)...");
                File.Delete(_cacheFilePath); // Kasowanie starego postępu
            }
        }

        int successCount = 0;

        // 2. PRZETWARZANIE POJEDYNCZO
        for (int i = 0; i < instructors.Count; i++)
        {
            var inst = instructors[i];

            // Pomijamy, jeśli wynik jest już wczytany
            if (finalDict.ContainsKey(inst.Id))
            {
                continue;
            }

            Console.WriteLine($"\n[{i + 1}/{instructors.Count}] Analiza dla: {inst.Name}...");

            var pref = await ProcessSingleInstructorAsync(inst);
            finalDict[inst.Id] = pref;

            // Zapis po każdym udanym kroku
            File.WriteAllText(_cacheFilePath, JsonSerializer.Serialize(finalDict, new JsonSerializerOptions { WriteIndented = true }));
            successCount++;

            if (i < instructors.Count - 1)
            {
                Console.WriteLine("Oczekiwanie 3 sekundy przed kolejnym...");
                await Task.Delay(3000);
            }
        }

        return finalDict;
    }

    private async Task<InstructorPreferences> ProcessSingleInstructorAsync(Instructor inst)
    {
        var requestBody = new
        {
            model = _modelName,
            messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = $"Tekst preferencji: {inst.PreferencesText}" }
            },
            temperature = 0.0,
            top_p = 0.9
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        int maxRetries = 5;
        for (int i = 0; i < maxRetries; i++)
        {
            string rawResponse = "";
            try
            {
                var response = await _httpClient.PostAsync(_apiUrl, content);
                rawResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429 || (int)response.StatusCode == 503)
                    {
                        int waitSeconds = 10 * (i + 1);
                        Console.WriteLine($"Serwer zajęty (kod {(int)response.StatusCode}). Czekam {waitSeconds} sekund...");
                        await Task.Delay(waitSeconds * 1000);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"[BŁĄD API - Kod {(int)response.StatusCode}] Serwer odrzucił zapytanie.");
                        continue;
                    }
                }

                return ParseLlmResponse(rawResponse);
            }
            catch (JsonException)
            {
                Console.WriteLine($"[BŁĄD JSON] Model zwrócił zły format. Ponawiam...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BŁĄD SIECI] (próba {i + 1}): {ex.Message}");
            }
        }

        Console.WriteLine($"Nie udało się przeanalizować {inst.Name}. Ustawiam domyślne.");
        return GetDefaultPreferences();
    }

    private string GetSystemPrompt()
    {
        return @"Jesteś precyzyjnym ekstraktorem danych. Zwróć WYŁĄCZNIE poprawny obiekt JSON, bez żadnego tekstu pobocznego.

ZASADY:
1. Pomiń dni, w których prowadzący NIE MOGĄ uczyć. Wstaw je do ""forbidden_slots"".
2. Skup się na godzinach ""MOGĘ"".
3. Dni tygodnia to: ""Mon"", ""Tue"", ""Wed"", ""Thu"", ""Fri"".

OCZEKIWANY FORMAT (Zwróć bezpośrednio ten obiekt):
{
  ""preferred_days"": [""Tue"", ""Thu""],
  ""preferred_hours_start"": 8,
  ""preferred_hours_end"": 12,
  ""forbidden_slots"": [
    {""day"": ""Fri"", ""from"": 12, ""to"": 20}
  ],
  ""min_start_hour"": 8
}";
    }

    private InstructorPreferences ParseLlmResponse(string jsonResponse)
    {
        using var doc = JsonDocument.Parse(jsonResponse);
        var messageContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        var cleanJson = messageContent.Replace("```json", "").Replace("```", "").Trim();
        return JsonSerializer.Deserialize<InstructorPreferences>(cleanJson);
    }

    private Dictionary<string, InstructorPreferences> GenerateDefaultBatch(List<Instructor> batch)
    {
        var dict = new Dictionary<string, InstructorPreferences>();
        foreach (var inst in batch)
        {
            dict[inst.Id] = GetDefaultPreferences();
        }
        return dict;
    }

    private InstructorPreferences GetDefaultPreferences()
    {
        return new InstructorPreferences
        {
            PreferredDays = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri" },
            PreferredHoursStart = 8,
            PreferredHoursEnd = 18,
            ForbiddenSlots = new List<ForbiddenSlot>(),
            MinStartHour = 8
        };
    }
}