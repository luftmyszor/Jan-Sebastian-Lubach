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
        var courses = gen.GenerateCourses(40, groups); 
        var instructors = gen.GenerateInstructors(40); 
        var rooms = gen.GenerateRooms(30);

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
    ga.StepGeneration();

    if (i % 10 == 0 || i == 1)
    {
        float bestFitness = ga.GetBestFitness(); 
        
        // Output in a specific format for Python to read
        Console.WriteLine($"PROGRESS|{i}|{bestFitness:F2}");
        if (bestFitness >= 940.0f) // Adjusted threshold for "perfect" solution based on new scoring
        {
            Console.WriteLine($"PERFECT_SOLUTION|{i}|{bestFitness:F2}");
            break;
        }
    }
}

    stopwatch.Stop();

    // --- EXPORT TIMETABLE FOR PYTHON ---
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
    
    // Fallback safety
    if(dayIdx >= days.Length) continue;

    var timeSlot = timeConfig.Slots[slotIdx];
    string timeStr = $"{timeSlot.Start} - {timeSlot.End}";

    // Match the exact dictionary keys the Python GUI needs
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

// Save to file
string outputPath = "generated_timetable.json";
System.IO.File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(exportedSchedule));
Console.WriteLine($"DONE|{outputPath}");
}}