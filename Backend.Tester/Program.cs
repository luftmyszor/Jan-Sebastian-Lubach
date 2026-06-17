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
            int popSize = 1000;            
            float mutRate = 0.05f;         
            int elitism = 50;              
            int immigrationCount = 60;     
            float parentPercentage = 0.7f; 
            int maxGenerations = 10000;

            Console.WriteLine($"Courses: {courses.Count} | Instructors: {instructors.Count} | Rooms: {rooms.Count}");
            Console.WriteLine("Gen\tBest\tAvg\tWorst\tTime(ms)");
            Console.WriteLine("--------------------------------------------------");

            var ga = new GeneticAlgorithm(popSize, mutRate, elitism, mapper, immigrationCount, parentPercentage);
            var stopwatch = Stopwatch.StartNew();

            for (int i = 1; i <= maxGenerations; i++)
            {
                ga.StepGeneration();

                if (i % 10 == 0 || i == 1)
                {
                    float bestFitness = ga.GetBestFitness(); 
                    Console.WriteLine($"PROGRESS|{i}|{bestFitness:F2}");
                    if (bestFitness >= 940.0f)
                    {
                        Console.WriteLine($"PERFECT_SOLUTION|{i}|{bestFitness:F2}");
                        break;
                    }
                }
            }

            stopwatch.Stop();

            Console.WriteLine("\n[PHASE 4] Exporting generated schedule...");
            var bestGenes = ga.GetBestGenes();
            var exportedSchedule = new List<object>();
            string[] days = {"Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek"};

            for (int idx = 0; idx < courses.Count; idx++)
            {
                var (t, r, s) = mapper.Decode(bestGenes[idx]);
                var course = courses[idx];
                var instructor = instructors[t];
                var room = rooms[r];

                int dayIdx = s / timeConfig.SlotsPerDay;
                int slotIdx = s % timeConfig.SlotsPerDay;
                
                if(dayIdx >= days.Length) continue;

                var timeSlot = timeConfig.Slots[slotIdx];
                string timeStr = $"{timeSlot.Start} - {timeSlot.End}";

                exportedSchedule.Add(new {
                    klasa = course.GroupId, 
                    wykladowca = instructor.Name,
                    przedmiot = course.Name,
                    typ = course.Type.ToUpper(),
                    dzien = days[dayIdx],
                    godzina = timeStr,
                    sala = room.Name
                });
            }

            string outputPath = "generated_timetable.json";
            System.IO.File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(exportedSchedule));
            Console.WriteLine($"DONE|{outputPath}");
        }
    }
}