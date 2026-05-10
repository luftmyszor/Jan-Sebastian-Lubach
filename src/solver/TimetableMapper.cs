using System;
using System.Collections.Generic;
using System.Linq;

public class TimetableMapper
{
    // --- Data ---
    public List<Course> Courses { get; private set; }
    public List<Instructor> Instructors { get; private set; }
    public List<Room> Rooms { get; private set; }

    // --- GA Constants (System Constraints) ---
    public readonly int SlotsPerDay;
    public readonly int Days;
    public readonly int R_max;
    public readonly int S_max;
    public readonly int T_max;
    public readonly int GroupCount;
    public readonly int GenomeLength;

    // Pre-calculated multiplier
    private readonly int _RS_Multiplier;

    // --- Fast Lookups ---
    // We group rooms and instructors by criteria so we can pick valid ones quickly during seed generation
    private Dictionary<string, List<int>> _roomsByType = new();
    private Dictionary<string, List<int>> _instructorsBySubject = new();
    private Dictionary<string, int> _groupIndexById = new();

    public TimetableMapper(List<Course> courses, List<Instructor> instructors, List<Room> rooms, int slotsPerDay = 10, int days = 5)
    {
        Courses = courses;
        Instructors = instructors;
        Rooms = rooms;
        SlotsPerDay = slotsPerDay;
        Days = days;

        // Set Constraints
        R_max = rooms.Count;
        T_max = instructors.Count;
        S_max = slotsPerDay * days;
        GroupCount = courses.Select(c => c.GroupId).Distinct().Count();
        GenomeLength = courses.Count; // 1 Gene = 1 Course

        _RS_Multiplier = R_max * S_max;

        BuildFastLookups();
    }

    private void BuildFastLookups()
    {
        // Group Room Indices by Room Type
        _roomsByType = Rooms
            .Select((room, index) => new { room.Type, index })
            .GroupBy(x => x.Type)
            .ToDictionary(g => g.Key, g => g.Select(x => x.index).ToList());

        // Group Instructor Indices by Subject ID
        _instructorsBySubject = new Dictionary<string, List<int>>();
        for (int i = 0; i < Instructors.Count; i++)
        {
            foreach (var subject in Instructors[i].Subjects)
            {
                if (!_instructorsBySubject.ContainsKey(subject))
                    _instructorsBySubject[subject] = new List<int>();
                
                _instructorsBySubject[subject].Add(i);
            }
        }

        _groupIndexById = new Dictionary<string, int>();
        for (int i = 0; i < Courses.Count; i++)
        {
            var groupId = Courses[i].GroupId;
            if (!_groupIndexById.ContainsKey(groupId))
            {
                _groupIndexById[groupId] = _groupIndexById.Count;
            }
        }
    }
    
    public int Encode(int t, int r, int s)
    {
        // PackedValue = T * (R_max * S_max) + R * S_max + S
        return (t * _RS_Multiplier) + (r * S_max) + s;
    }

    public (int T, int R, int S) Decode(int packedValue)
    {
        // Extract Slot (S)
        int s = packedValue % S_max;
        
        // Extract Room (R)
        int r = (packedValue / S_max) % R_max;
        
        // Extract Teacher (T)
        int t = (packedValue / _RS_Multiplier) % T_max;

        return (t, r, s);
    }

    public int GetGroupIndex(string groupId)
    {
        return _groupIndexById[groupId];
    }

    // --- INITIAL POPULATION BUILDER ---
    
    /// <summary>
    /// Generates a single Gene where hard constraints H4 (Teacher Qualified) 
    /// and H5 (Room Type Correct) are already satisfied.
    /// </summary>
    public int CreateSingleValidGene(int courseIndex, Random rand)
    {
        var course = Courses[courseIndex];

        // 1. Pick a valid Teacher (T)
        var validTeachers = _instructorsBySubject[course.SubjectId];
        int t = validTeachers[rand.Next(validTeachers.Count)];

        // 2. Pick a valid Room (R)
        var validRooms = _roomsByType[course.RequiredRoomType];
        int r = validRooms[rand.Next(validRooms.Count)];

        // 3. Pick a random Start Slot (S)
        // Prevent the course from overflowing past the end of the day.
        int day = rand.Next(0, Days);
        int maxStartInDay = SlotsPerDay - course.RequiredSlots; // Prevent overflow
        int sInDay = rand.Next(0, maxStartInDay + 1);
        int s = (day * SlotsPerDay) + sInDay;

        return Encode(t, r, s);
    }
    public Genome CreateSmartSeedGenome(Random rand)
    {
        int[] genes = new int[GenomeLength];

        for (int i = 0; i < GenomeLength; i++)
        {
            genes[i] = CreateSingleValidGene(i, rand);
        }

        return new Genome(genes);
    }
}