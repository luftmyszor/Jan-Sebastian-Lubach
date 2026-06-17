using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

namespace Backend.Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("      USOS GA SOLVER (PRODUCTION MODE)            ");
            Console.WriteLine("==================================================");

            // Accept a file path from the command line, or default to the cache
            string inputFilePath = args.Length > 0 ? args[0] : "wynik_preferencje.json";

            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine($"[ERROR] Missing parsed data file: {inputFilePath}. Run the LLM Parser first.");
                return;
            }

            Console.WriteLine("\n[PHASE 0] Loading Time Config...");
            var timeConfig = TimetableLoader.LoadTimeSlots("time_slots.json");

            Console.WriteLine($"\n[PHASE 1] Loading real data from {inputFilePath}...");
            var (courses, instructors, rooms) = TimetableLoader.LoadFromJson(inputFilePath, timeConfig);

            Console.WriteLine("\n[PHASE 2] Initializing Mapper...");
            var mapper = new TimetableMapper(courses, instructors, rooms, slotsPerDay: timeConfig.SlotsPerDay, days: 5); 

            Console.WriteLine("\n[PHASE 3] Running Genetic Algorithm...");

            foreach (var teacher in instructors)
            {
                teacher.InitializeBitmasks();
            }
            // -------------------------------------------------------------
            // USTAWIENIA ALGORYTMU GENETYCZNEGO
            // -------------------------------------------------------------
            int popSize = 250;
            float mutRate = 0.05f;
            int elitismCount = 5;               
            int immigrationCount = 50;          
            float parentPercentage = 0.3f;      

            int maxGenerations = 2000;
            float bestFitnessEver = float.MinValue;
            
            using (var ga = new GeneticAlgorithm(popSize, mutRate, elitismCount, mapper, immigrationCount, parentPercentage))
            {
                for (int gen = 0; gen < maxGenerations; gen++)
                {
                    var generationStopwatch = Stopwatch.StartNew();

                    ga.StepGeneration();

                    float currentBest = ga.GetBestFitness();
                    long generationTimeMs = generationStopwatch.ElapsedMilliseconds;

                    if (currentBest > bestFitnessEver)
                    {
                        bestFitnessEver = currentBest;
                    }



                    if (gen % 10 == 0) 
                    {
                        generationStopwatch.Stop();
                        Console.WriteLine($"PROGRESS|{gen}|{currentBest:0.00}|{generationTimeMs}");
                    }

                    if (currentBest >= 995.0f)
                    {
                        Console.WriteLine($"PERFECT_SOLUTION|{gen}|{currentBest:0.00}|{generationTimeMs}");
                        break;
                    }
                }
                ga.RunDiagnostics();

                // =========================================================
                // NAPRAWIONY ZAPIS WYNIKU DO JSON
                // =========================================================
                int[] bestGenome = ga.GetBestGenes();
                
                // Tworzymy listę obiektów, które będą pasować do formatu GUI w Pythonie
                var exportList = new System.Collections.Generic.List<object>();
                
                string[] daysPl = { "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek" };
                string[] hoursPl = { "08:00 - 09:30", "09:45 - 11:15", "11:30 - 13:00", "13:15 - 14:45", "15:00 - 16:30", "16:45 - 18:15", "18:30 - 20:00" };
                int slotsPerDay = hoursPl.Length; 

                // Ręczne dekodowanie każdego genu z int do czytelnych wartości
                for (int i = 0; i < bestGenome.Length; i++)
                {
                    var (t, r, s) = mapper.Decode(bestGenome[i]);
                    var course = mapper.Courses[i];
                    
                    // Rozbijamy długie zajęcia na pojedyncze kafelki (godziny)
                    for(int offset = 0; offset < course.RequiredSlots; offset++)
                    {
                        int currentS = s + offset;
                        int d = currentS / slotsPerDay;
                        int h = currentS % slotsPerDay;

                        // Upewniamy się, że nie wykraczamy poza tydzień
                        if (d >= 0 && d < 5 && h >= 0 && h < slotsPerDay)
                        {
                            exportList.Add(new {
                                dzien = daysPl[d],
                                godzina = hoursPl[h],
                                sala = mapper.Rooms[r].Id,
                                // Tutaj wstawiamy ID np "I01", bo napisaliśmy wcześniej łatkę 
                                // w Pythonie, która i tak sama zamieni to na "prof. dr hab. ..."
                                wykladowca = mapper.Instructors[t].Id, 
                                przedmiot = course.Name,
                                klasa = course.GroupId,
                                typ = course.Type
                            });
                        }
                    }
                }

                string outputFilename = "generated_timetable.json";
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string jsonString = System.Text.Json.JsonSerializer.Serialize(exportList, options);
                
                System.IO.File.WriteAllText(outputFilename, jsonString);

                Console.WriteLine($"DONE|{outputFilename}");
                
            }
        }
    }
}