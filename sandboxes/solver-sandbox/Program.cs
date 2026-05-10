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

var mapper = new TimetableMapper(courses, instructors, rooms, 40);

// ==========================================
// PLAYGROUND VARIABLES 
// ==========================================
int popSize = 1000;
float mutRate = 0.08f;
int elitism = 10;
int maxGenerations = 2000;
// ==========================================

Console.WriteLine($"Courses: {courses.Count} | Population: {popSize} | Mutation: {mutRate:P1}");
Console.WriteLine("Gen\tBest Fit\tTime (ms)");
Console.WriteLine("--------------------------------------------------");

var ga = new GeneticAlgorithm(popSize, mutRate, elitism, mapper);
var stopwatch = Stopwatch.StartNew();

// Run the loop
for (int i = 1; i <= maxGenerations; i++)
{
    long startMs = stopwatch.ElapsedMilliseconds;
    
    ga.StepGeneration();
    
    long stepTime = stopwatch.ElapsedMilliseconds - startMs;

    // Print progress every 10 generations
    if (i % 10 == 0 || i == 1)
    {

        float bestFitness = ga.GetBestFitness(); 
        
        Console.ForegroundColor = bestFitness > 90 ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"{i}\t{bestFitness:F2}\t\t{stepTime}ms");
        Console.ResetColor();
    }
}

stopwatch.Stop();
Console.WriteLine("--------------------------------------------------");
Console.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");
