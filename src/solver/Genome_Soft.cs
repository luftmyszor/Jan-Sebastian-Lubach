using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


public record struct Genome
{
    public int[] Genes;
    public float Fitness;
    public bool IsEvaluated;
    public ulong BrokenGenesMask;

    public Genome(int[] genes)
    {
        Genes = genes;
        Fitness = 0f;
        IsEvaluated = false;
        BrokenGenesMask = 0;
    }
}


public class FitnessEvaluator
{
    private readonly TimetableMapper _mapper;
    
    // Weight of penalty per violation. ly.
    private const float HARD_PENALTY = -1f;

    // Values for soft constraints
    private const int DAYS_PER_WEEK = 5;
    private const int LATE_START_THRESHOLD = 6;

    public FitnessEvaluator(TimetableMapper mapper)
    {
        _mapper = mapper;
    }
    
    public (float Fitness, ulong Mask) Evaluate(int[] genes)
    {
        var decodedSchedule = DecodeGenome(genes);

        // Create the empty mask right at the start
        ulong brokenMask = 0; 

        int h1_violations = CheckH1_TeacherSingleBooked(decodedSchedule, ref brokenMask);
        int h2_violations = CheckH2_RoomSingleBooked(decodedSchedule, ref brokenMask);
        int h3_violations = CheckH3_RoomCapacity(decodedSchedule, ref brokenMask);
        int h4_violations = CheckH4_TeacherQualified(decodedSchedule, ref brokenMask);
        int h5_violations = CheckH5_RoomTypeCorrect(decodedSchedule, ref brokenMask);
        int h6_violations = CheckH6_TeacherAvailable(decodedSchedule, ref brokenMask);
        int h7_violations = CheckH7_StudentGroupSingleBooked(decodedSchedule, ref brokenMask);

        int totalHardViolations = h1_violations + h2_violations + h3_violations + 
                                  h4_violations + h5_violations + h6_violations + h7_violations;

        if (totalHardViolations == 0)
        {
            float softFitness = CalculateSoftFitness(decodedSchedule);
            return (softFitness, brokenMask); 
        }


        float finalFitness = (totalHardViolations * HARD_PENALTY);

        return (finalFitness, brokenMask);
    }

    // --- DECODER HELPER ---

    private List<DecodedGene> DecodeGenome(int[] genes)
    {
        var schedule = new List<DecodedGene>(_mapper.Courses.Count);
        
        for (int i = 0; i < _mapper.Courses.Count; i++) 
        {
            var (t, r, s) = _mapper.Decode(genes[i]);
            var course = _mapper.Courses[i];
            var groupIndex = _mapper.GetGroupIndex(course.GroupId);
            
            // Expand the start slot 's' into a list of all occupied slots
            // e.g., Start slot 10, length 3 -> occupies slots [10, 11, 12]
            var occupiedSlots = new List<int>(course.RequiredSlots);
            for (int slotOffset = 0; slotOffset < course.RequiredSlots; slotOffset++)
            {
                occupiedSlots.Add(s + slotOffset);
            }

            schedule.Add(new DecodedGene 
            { 
                CourseIndex = i, Course = course, 
                T = t, R = r, S = s, 
                GroupIndex = groupIndex,

            });
        }
        return schedule;
    }

    // --- HARD CONSTRAINT FUNCTIONS ---

    private int CheckH1_TeacherSingleBooked(List<DecodedGene> schedule, ref ulong brokenMask)
    {
        int violations = 0;
        
        // Use an array to track booked slots per teacher instead of HashSets and GroupBys
        // Size = Total Teachers * Total Slots
        int[] teacherBookingMatrix = ArrayPool<int>.Shared.Rent(_mapper.T_max * _mapper.S_max);
        Array.Clear(teacherBookingMatrix, 0, teacherBookingMatrix.Length);

        foreach (var gene in schedule)
        {
            // Loop from the Start Slot (S) for the duration of the course
            for(int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int currentSlot = gene.S + offset;
                
                // 1D array indexing simulating a 2D grid [Teacher, Slot]
                int index = (gene.T * _mapper.S_max) + currentSlot;
                
                if (teacherBookingMatrix[index] == 1) 
                {
                    violations++; // Teacher is in two places at once!
                    brokenMask |= (1UL << gene.CourseIndex);
                }
                teacherBookingMatrix[index] = 1; // Mark as booked
            }
        }
        
        ArrayPool<int>.Shared.Return(teacherBookingMatrix);
        return violations;
    }

    private int CheckH2_RoomSingleBooked(List<DecodedGene> schedule, ref ulong brokenMask)
    {
        int violations = 0;
        int[] roomBookingMatrix = ArrayPool<int>.Shared.Rent(_mapper.R_max * _mapper.S_max);
        Array.Clear(roomBookingMatrix, 0, roomBookingMatrix.Length);

        foreach (var gene in schedule)
        {
            for (int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int currentSlot = gene.S + offset;
                int index = (gene.R * _mapper.S_max) + currentSlot;
                
                int existingOccupant = roomBookingMatrix[index];
                
                // If it's greater than 0, someone is already in this room!
                if (existingOccupant > 0) 
                {
                    violations++; 
                    
                    // 1. Flag the new course that just tried to enter
                    brokenMask |= (1UL << gene.CourseIndex);
                    
                    // 2. Flag the original course that was already sitting there!
                    // (Subtract 1 to get the actual CourseIndex back)
                    brokenMask |= (1UL << (existingOccupant - 1));
                }
                else
                {
                    // Room is empty. Claim it by storing this course's ID + 1
                    roomBookingMatrix[index] = gene.CourseIndex + 1; 
                }
            }
        }
        
        ArrayPool<int>.Shared.Return(roomBookingMatrix);
        return violations;
    }

    private int CheckH3_RoomCapacity(List<DecodedGene> schedule, ref ulong brokenMask)
    {
        int violations = 0;
        foreach (var gene in schedule)
        {
            var room = _mapper.Rooms[gene.R];
            if (gene.Course.RequiredSlots > 0 && gene.Course.Students > room.Capacity) 
            {
                violations++;
                brokenMask |= (1UL << gene.CourseIndex);
            }
        }
        return violations;
    }

    private int CheckH4_TeacherQualified(List<DecodedGene> schedule, ref ulong brokenMask)
    {
        int violations = 0;
        foreach (var gene in schedule)
        {
            var teacher = _mapper.Instructors[gene.T];
            if (!teacher.Subjects.Contains(gene.Course.SubjectId))
            {
                violations++;
                brokenMask |= (1UL << gene.CourseIndex);
            }
        }
        return violations;
    }

    private int CheckH5_RoomTypeCorrect(List<DecodedGene> schedule, ref ulong brokenMask)
    {
        int violations = 0;
        foreach (var gene in schedule)
        {
            var room = _mapper.Rooms[gene.R];
            if (room.Type != gene.Course.RequiredRoomType)
            {
                violations++;
                brokenMask |= (1UL << gene.CourseIndex);
            }
        }
        return violations;
    }

    private int CheckH6_TeacherAvailable(List<DecodedGene> schedule, ref ulong brokenMask)
    {
        int violations = 0;
        foreach (var gene in schedule)
        {
            var teacher = _mapper.Instructors[gene.T];
            
            // The teacher MUST be available for EVERY slot the course requires
            for (int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int currentSlot = gene.S + offset;
                
                if (!teacher.AvailableSlots.Contains(currentSlot))
                {
                    violations++;
                    brokenMask |= (1UL << gene.CourseIndex);
                    break; // One broken slot ruins the whole class placement
                }
            }
        }
        return violations;
    }

    private int CheckH7_StudentGroupSingleBooked(List<DecodedGene> schedule, ref ulong brokenMask)
    {
        int violations = 0;
        int[] groupBookingMatrix = ArrayPool<int>.Shared.Rent(_mapper.GroupCount * _mapper.S_max);
        Array.Clear(groupBookingMatrix, 0, groupBookingMatrix.Length);

        foreach (var gene in schedule)
        {
            // The Zero-Allocation Loop!
            for (int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int currentSlot = gene.S + offset;
                int index = (gene.GroupIndex * _mapper.S_max) + currentSlot;

                if (groupBookingMatrix[index] == 1)
                {
                    violations++; // Students are in two places at once!
                    brokenMask |= (1UL << gene.CourseIndex);
                }
                groupBookingMatrix[index] = 1;
            }
        }

        ArrayPool<int>.Shared.Return(groupBookingMatrix);
        return violations;
    }

    // ---  SOFT CONSTRAINT FUNCTIONS ---

    private float EvaluateS1_TeacherPreferences(List<DecodedGene> schedule)
    {
        int hits = 0;
        int total = 0;

        foreach (var gene in schedule)
        {
            var teacher = _mapper.Instructors[gene.T];

            if (teacher.PreferredSlots == null || teacher.PreferredSlots.Count == 0)
            {
                // No preferences declared => treat every slot as preferred
                total += gene.Course.RequiredSlots;
                hits += gene.Course.RequiredSlots;
                continue;
            }

            for (int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int slot = gene.S + offset;
                total++;
                if (teacher.PreferredSlots.Contains(slot))
                    hits++;
            }
        }

        return total == 0 ? 1f : (float)hits / total;
    }

    private float EvaluateS2_StudentGaps(List<DecodedGene> schedule)
    {
        int slotsPerDay = _mapper.S_max / DAYS_PER_WEEK;
        if (slotsPerDay < 1) return 1f;

        // Track (earliest slot, latest slot, occupied count) per (group, day)
        var daySpans = new Dictionary<(int group, int day), (int min, int max, int count)>();

        foreach (var gene in schedule)
        {
            for (int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int slot = gene.S + offset;
                int day = slot / slotsPerDay;
                var key = (gene.GroupIndex, day);

                if (daySpans.TryGetValue(key, out var cur))
                    daySpans[key] = (Math.Min(cur.min, slot), Math.Max(cur.max, slot), cur.count + 1);
                else
                    daySpans[key] = (slot, slot, 1);
            }
        }

        int totalGaps = 0;
        int maxPossible = 0;

        foreach (var kv in daySpans)
        {
            var (min, max, count) = kv.Value;
            int span = max - min + 1;
            totalGaps += span - count;  // Idle slots within the span
            maxPossible += Math.Max(0, slotsPerDay - 2);    // Realistic worst case per group-day
        }

        if (maxPossible == 0) return 1f;
        return 1f - Math.Min(1f, (float)totalGaps / maxPossible);
    }

    private float EvaluateS3_LateClasses(List<DecodedGene> schedule)
    {
        if (schedule.Count == 0) return 1f;

        int slotsPerDay = _mapper.S_max / DAYS_PER_WEEK;
        int earlyStarts = 0;

        foreach (var gene in schedule)
        {
            int slotInDay = slotsPerDay > 0 ? gene.S % slotsPerDay : gene.S;
            if (slotInDay < LATE_START_THRESHOLD)
                earlyStarts++;
        }

        return (float)earlyStarts / schedule.Count;
    }

    private float EvaluateS4_DailyBalance(List<DecodedGene> schedule)
    {
        int slotsPerDay = _mapper.S_max / DAYS_PER_WEEK;
        if (slotsPerDay < 1) return 1f;

        // Count course starts per (group, day)
        var dayCounts = new Dictionary<(int group, int day), int>();

        foreach (var gene in schedule)
        {
            int day = gene.S / slotsPerDay;
            var key = (gene.GroupIndex, day);
            dayCounts[key] = dayCounts.GetValueOrDefault(key, 0) + 1;
        }

        // Aggregate into per-group daily arrays
        var byGroup = new Dictionary<int, int[]>();

        foreach (var kv in dayCounts)
        {
            int group = kv.Key.group;
            int day = kv.Key.day;

            if (!byGroup.TryGetValue(group, out int[]? counts))
            {
                counts = new int[DAYS_PER_WEEK];
                byGroup[group] = counts;
            }

            if (day < DAYS_PER_WEEK)
                counts[day] += kv.Value;
        }

        if (byGroup.Count == 0) return 1f;

        float totalCV = 0f;

        foreach (var counts in byGroup.Values)
        {
            float mean = counts.Average();
            if (mean < 0.001f) continue;

            float variance = counts.Average(c => (float)Math.Pow(c - mean, 2));
            float stddev = (float)Math.Sqrt(variance);
            float cv = stddev / mean;   // 0 = perfectly balanced
            totalCV += Math.Min(1f, cv);    // Cap per group so one outlier doesn't dominate
        }

        float meanCV = totalCV / byGroup.Count;
        return 1f - meanCV;     // Invert so higher = better
    }

    private float CalculateSoftFitness(List<DecodedGene> schedule)
    {
        // Start with a massive base score so valid schedules always beat invalid ones
        float score = 1000f;

        score -= (1f - EvaluateS1_TeacherPreferences(schedule)) * 500f;
        score -= (1f - EvaluateS2_StudentGaps(schedule)) * 300f;
        score -= (1f - EvaluateS3_LateClasses(schedule)) * 100f;
        score -= (1f - EvaluateS4_DailyBalance(schedule)) * 100f;

        return score;
    }

    // Small struct to hold decoded data so we don't decode multiple times
    private struct DecodedGene
    {
        public int CourseIndex;
        public Course Course;
        public int T;
        public int R;
        public int S;
        public int GroupIndex;
    }
}