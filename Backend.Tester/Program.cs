using System;
using System.Diagnostics;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("      USOS GA SOLVER (GENERATOR TEST MODE)        ");
        Console.WriteLine("==================================================");

        Console.WriteLine("\n[PHASE 0] Loading Settings...");
        var timeConfig = TimetableLoader.LoadTimeSlots("time_slots.json");

        /* =========================================================
         * COMMENTED OUT: REAL DATA & LLM PIPELINE
         * =========================================================
         * * string inputFilePath = "dane_nowe.json"; 
         * string intermediateFilePath = "wynik_preferencje.json"; 
         * var parser = new DataParserModule();
         * var universityData = parser.ParseAndValidate(inputFilePath);
         * var llmAnalyzer = new LlmPreferenceAnalyzer(offlineMode: true); 
         * var extractedPreferences = await llmAnalyzer.ExtractAllPreferencesAsync(universityData.Instructors);
         * // ... apply preferences and save ...
         * var (courses, instructors, rooms) = TimetableLoader.LoadFromJson(intermediateFilePath);
         */

        // ==========================================
        // PHASE 1: GENERATE DUMMY DATA
        // ==========================================
        Console.WriteLine("\n[PHASE 1] Generating controlled dummy data...");
        
        var gen = new TimetableDataGenerator(seed: 1337);

        var groups = gen.GenerateStudentGroups(10);     
        var courses = gen.GenerateCourses(50, groups); 
        var instructors = gen.GenerateInstructors(40); 
        var rooms = gen.GenerateRooms(50);

        // ==========================================
        // PHASE 2: INITIALIZE MAPPER
        // ==========================================
        Console.WriteLine("\n[PHASE 2] Loading data into Mapper...");
        var mapper = new TimetableMapper(courses, instructors, rooms, slotsPerDay: timeConfig.SlotsPerDay, days: 5); 

        // ==========================================
        // PHASE 3: GENETIC ALGORITHM
        // ==========================================
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
            long startMs = stopwatch.ElapsedMilliseconds;
            
            ga.StepGeneration();
            
            long stepTime = stopwatch.ElapsedMilliseconds - startMs;

            if (i % 10 == 0 || i == 1)
            {
                float bestFitness = ga.GetBestFitness(); 
                float averageFitness = ga.GetAverageFitness();
                float worstFitness = ga.GetWorstFitness();
                
                Console.ForegroundColor = bestFitness > 90 ? ConsoleColor.Green : ConsoleColor.Yellow;
                Console.WriteLine($"{i}\t{bestFitness:F2}\t{averageFitness:F2}\t{worstFitness:F2}\t{stepTime}ms");
                Console.ResetColor();

                // if (bestFitness >= 99.9f)
                // {
                //     Console.WriteLine($"\n[SUCCESS] Perfect solution found at generation {i}!");
                //     break;
                // }
                Console.Out.Flush();
            }
        }

        stopwatch.Stop();
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"Total GA Time: {stopwatch.ElapsedMilliseconds}ms");
    }
}