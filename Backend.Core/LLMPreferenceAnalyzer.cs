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
    private readonly string _apiUrl = "http://149.156.194.192:8088/v1/chat/completions";
    private readonly string _token = "bsk-00a229f80354793ad87e93fea4691b31521e4fb43a2cf8cd3d916fe02b64a010";
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
            Console.WriteLine("Tryb offline. Generowanie domyslnych preferencji.");
            return GenerateDefaultBatch(instructors);
        }

        

        int successCount = 0;

        // 2. PRZETWARZANIE POJEDYNCZO
        for (int i = 0; i < instructors.Count; i++)
        {
            var inst = instructors[i];

            // Pomijamy, jesli wynik jest juz wczytany
            if (finalDict.ContainsKey(inst.Id))
            {
                continue;
            }
            Console.WriteLine($"\n[{i + 1}/{instructors.Count}] Analiza dla: {inst.Name}...");

            var pref = await ProcessSingleInstructorAsync(inst);
            finalDict[inst.Id] = pref;
            // Zapis po kazdym udanym kroku
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
                        Console.WriteLine($"Serwer zajety (kod {(int)response.StatusCode}). Czekam {waitSeconds} sekund...");
                        await Task.Delay(waitSeconds * 1000);
                        continue;
                    }
                    else
                    {
                            Console.WriteLine($"[BLAD API - Kod {(int)response.StatusCode}] Serwer odrzucil zapytanie.");
                        continue;
                    }
                }

                return ParseLlmResponse(rawResponse);
            }
            catch (JsonException)
            {
                    Console.WriteLine($"[BLAD JSON] Model zwrocil zly format. Ponawiam...");
            }
            catch (Exception ex)
            {
                    Console.WriteLine($"[BLAD SIECI] (proba {i + 1}): {ex.Message}");
            }
        }

        Console.WriteLine($"Nie udalo sie przeanalizowac {inst.Name}. Ustawiam domyslne.");
        return GetDefaultPreferences();
    }

    private string GetSystemPrompt()
    {
                return @"Jestes precyzyjnym ekstraktorem danych. Zwroc WYLACZNIE poprawny obiekt JSON, bez zadnego tekstu pobocznego.

ZASADY:
1. Sloty w ktorych prowadzacy NIE MOGA uczyc wstaw do ""forbidden_slots"".
2. Skup sie na godzinach ""MOGA"".
3. Dni tygodnia to: ""Mon"", ""Tue"", ""Wed"", ""Thu"", ""Fri"".

OCZEKIWANY FORMAT (Zwroc bezposrednio ten obiekt):
{
    ""preferred_days"": [""Tue"", ""Thu""],
    ""preferred_hours_start"": 8,
    ""preferred_hours_end"": 20,
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
