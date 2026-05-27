using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class FitnessEvaluatorTests
{
    private (TimetableMapper mapper, List<Course> courses) SetupEnvironment()
    {
        var gen = new TimetableDataGenerator(seed: 42); // Fixed seed for predictable tests
        var groups = gen.GenerateStudentGroups(5);
        var courses = gen.GenerateCourses(10, groups); 
        
        // ZMIANA TUTAJ: Generujemy 50 instruktorów zamiast 10, 
        // aby upewnić się, że każdy przedmiot ma nauczyciela!
        var instructors = gen.GenerateInstructors(50);
        var rooms = gen.GenerateRooms(50);

        var mapper = new TimetableMapper(courses, instructors, rooms, 50); 
        return (mapper, courses);
    }

    [Fact]
    public void Mapper_EncodeAndDecode_ShouldBeSymmetrical()
    {
        // Arrange
        var (mapper, _) = SetupEnvironment();
        int expectedT = 5;
        int expectedR = 8;
        int expectedS = 24;

        // Act
        int packedGene = mapper.Encode(expectedT, expectedR, expectedS);
        var (actualT, actualR, actualS) = mapper.Decode(packedGene);

        // Assert
        Assert.Equal(expectedT, actualT);
        Assert.Equal(expectedR, actualR);
        Assert.Equal(expectedS, actualS);
    }

    [Fact]
    public void Evaluator_RoomDoubleBooking_ShouldIncreasePenaltyAndSetMask()
    {
        // Arrange
        var (mapper, courses) = SetupEnvironment();
        var evaluator = new FitnessEvaluator(mapper);

        // Create a perfect smart seed (0 collisions)
        Genome testGenome = mapper.CreateSmartSeedGenome(new Random(100));

        // Let's force a collision! 
        // We will make Course[0] and Course[1] happen in the EXACT SAME ROOM at the EXACT SAME TIME.
        int conflictingRoom = 2;
        int conflictingSlot = 10;
        
        // Keep their original teachers so we don't trigger an H1 violation
        var (t0, _, _) = mapper.Decode(testGenome.Genes[0]);
        var (t1, _, _) = mapper.Decode(testGenome.Genes[1]);

        testGenome.Genes[0] = mapper.Encode(t0, conflictingRoom, conflictingSlot);
        testGenome.Genes[1] = mapper.Encode(t1, conflictingRoom, conflictingSlot);

        // Act
        var fitness = evaluator.Evaluate(testGenome);

        // Assert
        Assert.True(fitness < 0, $"Expected negative fitness, but got {fitness}");

        // The genome's mask must have bits 0 and 1 set
        Assert.True(testGenome.IsBroken(0), "Mask did not flag Course 0 as broken.");
        Assert.True(testGenome.IsBroken(1), "Mask did not flag Course 1 as broken.");
    }

    [Fact]
    public void Evaluator_StudentGroupDoubleBooking_ShouldSetMask()
    {
        // Arrange
        var (mapper, courses) = SetupEnvironment();
        var evaluator = new FitnessEvaluator(mapper);

        Genome testGenome = mapper.CreateSmartSeedGenome(new Random(100));

        // Find two courses that belong to the SAME student group
        var groupGroups = courses.GroupBy(c => c.GroupId).First(g => g.Count() >= 2).ToList();
        int courseIndexA = courses.IndexOf(groupGroups[0]);
        int courseIndexB = courses.IndexOf(groupGroups[1]);

        // Force them to happen at the exact same time, but in DIFFERENT rooms with DIFFERENT teachers
        int conflictingSlot = 15;
        
        testGenome.Genes[courseIndexA] = mapper.Encode(1, 1, conflictingSlot);
        testGenome.Genes[courseIndexB] = mapper.Encode(2, 2, conflictingSlot);

        // Act
        var fitness = evaluator.Evaluate(testGenome);

        // Assert
        Assert.True(fitness < 0, "Schedule should be invalid due to student group overlap.");

        Assert.True(testGenome.IsBroken(courseIndexA), $"Mask did not flag Course {courseIndexA} as broken.");
        Assert.True(testGenome.IsBroken(courseIndexB), $"Mask did not flag Course {courseIndexB} as broken.");
    }

    [Fact]
    public void Evaluator_RoomCapacity_ShouldSetMask()
    {
        // Arrange
        var (mapper, courses) = SetupEnvironment();
        var evaluator = new FitnessEvaluator(mapper);
        Genome testGenome = mapper.CreateSmartSeedGenome(new Random(100));

        // Znajdźmy kurs, który ma dużo studentów
        var largeCourse = courses.OrderByDescending(c => c.Students).First();
        int courseIndex = courses.IndexOf(largeCourse);

        // Znajdźmy salę, która jest dla nich za mała
        var smallRoom = mapper.Rooms.First(r => r.Capacity < largeCourse.Students);
        int roomIndex = mapper.Rooms.IndexOf(smallRoom);

        // Zmieniamy tylko pokój dla tego konkretnego kursu (nauczyciel i slot zostają te same)
        var (t, _, s) = mapper.Decode(testGenome.Genes[courseIndex]);
        testGenome.Genes[courseIndex] = mapper.Encode(t, roomIndex, s);

        // Act
        var fitness = evaluator.Evaluate(testGenome);

        // Assert
        Assert.True(testGenome.IsBroken(courseIndex), "Mask did not flag the course for exceeding room capacity.");
    }

    [Fact]
    public void Evaluator_TeacherNotAvailable_ShouldSetMask()
    {
        // Arrange
        var (mapper, _) = SetupEnvironment();
        var evaluator = new FitnessEvaluator(mapper);
        Genome testGenome = mapper.CreateSmartSeedGenome(new Random(100));

        // Bierzemy nauczyciela z pierwszego kursu
        var (t, r, _) = mapper.Decode(testGenome.Genes[0]);
        var teacher = mapper.Instructors[t];

        // Znajdźmy slot, w którym nauczyciel na 100% NIE pracuje
        int unavailableSlot = 0;
        while (teacher.AvailableSlots.Contains(unavailableSlot))
        {
            unavailableSlot++;
        }

        // Przypisujemy kurs na ten niedostępny slot
        testGenome.Genes[0] = mapper.Encode(t, r, unavailableSlot);

        // Act
        var fitness = evaluator.Evaluate(testGenome);

        // Assert
        Assert.True(testGenome.IsBroken(0), "Mask did not flag the course scheduled outside teacher's hours.");
    }
}