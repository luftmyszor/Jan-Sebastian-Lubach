using System;
using System.Collections.Generic;
using System.Linq;

public class TimetableDataGenerator
{
    private readonly Random _rand;

    private const int DAYS_IN_WEEK = 5;
    private const int SLOTS_PER_DAY = 7;
    private const int TOTAL_SLOTS = DAYS_IN_WEEK * SLOTS_PER_DAY;

    private readonly string[] _subjectPool = {
        "MAT-DYSK", "PROG-POD", "ARCH-KOMP", "PROG-OB", "ALGO",
        "BAZY-DAN", "SIECI", "SYS-OP", "INZ-OPR", "UCZ-MASZ",
        "BEZP-SYS", "STAT-RAC", "GRAF-KOMP", "PRZETW-SYG", "AN-NUM"
    };

    private readonly string[] _roomTypes = {
        "lecture_hall", "computer_lab", "exercise_room", "hardware_lab"
    };

    private readonly string[] _courseTypes = {
        "lecture", "exercise", "lab", "project"
    };

    public TimetableDataGenerator(int seed)
    {
        _rand = new Random(seed);
    }

    /// <summary>
    /// Generates 'n' Student Groups with varying student counts.
    /// </summary>
    public List<StudentGroup> GenerateStudentGroups(int n)
    {
        var groups = new List<StudentGroup>();

        for (int i = 1; i <= n; i++)
        {
            groups.Add(new StudentGroup
            {
                Id = $"G{i:D2}",
                Name = $"Grupa IS-{i}",
                Year = _rand.Next(1, 6),       
                Students = _rand.Next(12, 20)  
            });
        }

        return groups;
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
                AvailableSlots = GenerateRealisticAvailabilityBlocks(_rand.Next(12, 25)),
                HoursPerSemester = _rand.Next(120, 191) // Added from new model
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
                "lecture_hall" => _rand.Next(45, 201),
                "computer_lab" => _rand.Next(16, 31),
                "hardware_lab" => _rand.Next(12, 21),
                "exercise_room" => _rand.Next(18, 31),
                _ => 50
            };

            var room = new Room
            {
                Id = $"{roomType.Substring(0, 1).ToUpper()}{i:D2}", 
                Name = $"Sala {i} ({roomType})", // Added from new model
                Type = roomType,
                Capacity = capacity
            };
            rooms.Add(room);
        }

        return rooms;
    }

    /// <summary>
    /// Generates 'n' Courses. Requires a list of available student groups to map to.
    /// </summary>
    public List<Course> GenerateCourses(int n, List<StudentGroup> availableGroups)
    {
        var courses = new List<Course>();

        for (int i = 1; i <= n; i++)
        {
            string assignedSubject = _subjectPool[_rand.Next(_subjectPool.Length)];
            string reqRoomType = _roomTypes[_rand.Next(_roomTypes.Length)];
            string courseType = _courseTypes[_rand.Next(_courseTypes.Length)];
            int requiredSlots = _rand.Next(2, 4);
            
            // Pick a random student group to assign to this course
            var assignedGroup = availableGroups[_rand.Next(availableGroups.Count)];

            var course = new Course
            {
                Id = $"C{i:D2}",
                SubjectId = assignedSubject,
                Name = $"{assignedSubject} - {courseType}", // Added Name
                Type = courseType,                          // Added Type
                GroupId = assignedGroup.Id,                 // Mapped to Group ID
                Students = assignedGroup.Students,          // Inherited capacity needs
                RequiredRoomType = reqRoomType,
                RequiredSlots = requiredSlots
            };
            courses.Add(course);
        }

        return courses;
    }

    // --- Helper Methods ---

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