using System;
using System.Collections.Generic;
using System.Linq;

public class TimetableDataGenerator
{
    private readonly Random _rand;

    private const int DAYS_IN_WEEK = 5;
    private const int SLOTS_PER_DAY = 10;
    private const int TOTAL_SLOTS = DAYS_IN_WEEK * SLOTS_PER_DAY;

    private readonly string[] _subjectPool = {
        "MAT-DYSK", "PROG-POD", "ARCH-KOMP", "PROG-OB", "ALGO",
        "BAZY-DAN", "SIECI", "SYS-OP", "INZ-OPR", "UCZ-MASZ",
        "BEZP-SYS", "STAT-RAC", "GRAF-KOMP", "PRZETW-SYG", "AN-NUM"
    };

    private readonly string[] _roomTypes = {
        "lecture_hall", "computer_lab", "exercise_room", "hardware_lab"
    };

    public TimetableDataGenerator(int seed)
    {
        _rand = new Random(seed);
    }

    /// <summary>
    /// Generates 'n' Instructors with concise availability blocks.
    /// </summary>
    public List<Instructor> GenerateInstructors(int n)
    {
        var instructors = new List<Instructor>();

        for (int i = 1; i <= n; i++)
        {
            var instructor = new Instructor
            {
                Id = $"I{i:D2}",
                Name = $"Instructor {i}",
                Subjects = GenerateRandomSubjects(1, 4),
                // Aim for roughly 12-24 slots of availability per week (1.5 to 3 days worth)
                AvailableSlots = GenerateRealisticAvailabilityBlocks(_rand.Next(12, 25))
            };
            instructors.Add(instructor);
        }

        return instructors;
    }

    /// <summary>
    /// Generates 'n' Rooms of varying types and capacities.
    /// </summary>
    public List<Room> GenerateRooms(int n)
    {
        var rooms = new List<Room>();

        for (int i = 1; i <= n; i++)
        {
            string roomType = _roomTypes[_rand.Next(_roomTypes.Length)];
            
            int capacity = roomType switch
            {
                "lecture_hall" => _rand.Next(50, 201), // Large capacities for lectures
                "computer_lab" => _rand.Next(16, 31),  // Standard lab sizes
                "hardware_lab" => _rand.Next(12, 21),  // Smaller hardware labs
                "exercise_room" => _rand.Next(20, 31), // Standard classroom sizes
                _ => 30
            };

            var room = new Room
            {
                // Give standard prefixes based on type
                Id = $"{roomType.Substring(0, 1).ToUpper()}{i:D2}", 
                Type = roomType,
                Capacity = capacity
            };
            rooms.Add(room);
        }

        return rooms;
    }

    /// <summary>
    /// Generates 'n' Courses matching available subjects and room types.
    /// </summary>
    public List<Course> GenerateCourses(int n)
    {
        var courses = new List<Course>();

        for (int i = 1; i <= n; i++)
        {
            string assignedSubject = _subjectPool[_rand.Next(_subjectPool.Length)];
            
            string reqRoomType = _roomTypes[_rand.Next(_roomTypes.Length)];


            int requiredSlots = _rand.Next(2, 4);

            var course = new Course
            {
                Id = $"C{i:D2}",
                SubjectId = assignedSubject,
                RequiredRoomType = reqRoomType,
                RequiredSlots = requiredSlots
            };
            courses.Add(course);
        }

        return courses;
    }


    private List<string> GenerateRandomSubjects(int min, int max)
    {
        var subjects = new HashSet<string>();
        int count = _rand.Next(min, max + 1);

        while (subjects.Count < count)
        {
            subjects.Add(_subjectPool[_rand.Next(_subjectPool.Length)]);
        }

        return subjects.ToList();
    }

    private List<int> GenerateRealisticAvailabilityBlocks(int targetSlots)
    {
        var availableSet = new HashSet<int>();

        while (availableSet.Count < targetSlots)
        {
            int day = _rand.Next(0, DAYS_IN_WEEK);

            int blockSize = _rand.Next(2, 5);

            int maxStartSlot = SLOTS_PER_DAY - blockSize;
            int startSlotInDay = _rand.Next(0, maxStartSlot + 1);

            int absoluteStartSlot = (day * SLOTS_PER_DAY) + startSlotInDay;

            for (int i = 0; i < blockSize; i++)
            {
                availableSet.Add(absoluteStartSlot + i);
            }
        }

        var sortedSlots = availableSet.ToList();
        sortedSlots.Sort();
        return sortedSlots;
    }
}