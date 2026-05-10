using System;
using System.Diagnostics;

Console.WriteLine("==================================================");
Console.WriteLine("         GA SANDBOX & TUNING ENVIRONMENT          ");
Console.WriteLine("==================================================");

// 1. Generate Dummy Data (Using the generator we built earlier)
var gen = new TimetableDataGenerator(seed: 1337);

var groups = gen.GenerateStudentGroups(20);
var courses = gen.GenerateCourses(60, groups);
var instructors = gen.GenerateInstructors(30);
var rooms = gen.GenerateRooms(50);

var mapper = new TimetableMapper(courses, instructors, rooms, 10, 5); // 10 slots/day, 5 days

// ==========================================
// PLAYGROUND VARIABLES 
// ==========================================
int popSize = 20000;
float mutRate = 0.08f;
int elitism = 200;
int immigrationCount = 6000; //Keep at 20-30%
float parentPercentage = 0.8f;
int maxGenerations = 100000;
// ==========================================

Console.WriteLine($"Courses: {courses.Count} | Population: {popSize} | Mutation: {mutRate:P1}");
Console.WriteLine("Gen\tBest Fit\tTime (ms)");
Console.WriteLine("--------------------------------------------------");

var ga = new GeneticAlgorithm(popSize, mutRate, elitism, mapper, immigrationCount, parentPercentage);
var stopwatch = Stopwatch.StartNew();

// Run the loop
for (int i = 1; i <= maxGenerations; i++)
{
    long startMs = stopwatch.ElapsedMilliseconds;
    
    ga.StepGeneration();
    
    long stepTime = stopwatch.ElapsedMilliseconds - startMs;

    // Print progress every 10 generations
    if (i % 30 == 0 || i == 1)
    {

        float bestFitness = ga.GetBestFitness(); 
        float averageFitness = ga.GetAverageFitness();
        float worstFitness = ga.GetWorstFitness();
        
        Console.ForegroundColor = bestFitness > 90 ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"{i}\t{bestFitness:F2}\t{averageFitness:F2}\t{worstFitness:F2}\t{stepTime}ms");
        Console.ResetColor();
    }
}

stopwatch.Stop();
Console.WriteLine("--------------------------------------------------");
Console.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");
