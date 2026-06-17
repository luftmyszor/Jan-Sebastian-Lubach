using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


public class Genome 
{
    public int[] Genes;
    public float Fitness;
    public bool IsEvaluated;
    
    // NEW: The reusable bitmask
    public ulong[] BrokenGenesMask;

    // Construct from a rented (or owned) array of genes
    public Genome(int[] genes)
    {
        Genes = genes;
        Fitness = 0f;
        IsEvaluated = false;

        // Allocate the mask ONCE based on how many courses (genes) we have
        int numCourses = genes?.Length ?? 0;
        BrokenGenesMask = new ulong[(numCourses + 63) / 64];
    }

    // Alternative constructor for ArrayPool usage (length + total courses)
    public Genome(int length, int numCourses)
    {
        Genes = System.Buffers.ArrayPool<int>.Shared.Rent(length);
        Fitness = 0f;
        IsEvaluated = false;

        BrokenGenesMask = new ulong[(numCourses + 63) / 64];
    }

    // High-performance Bitwise Helpers
    public void SetBroken(int courseIndex) 
    { 
        BrokenGenesMask[courseIndex >> 6] |= (1UL << (courseIndex & 63)); 
    }

    public bool IsBroken(int courseIndex) 
    { 
        return (BrokenGenesMask[courseIndex >> 6] & (1UL << (courseIndex & 63))) != 0; 
    }
}


public class FitnessEvaluator
{
    private readonly TimetableMapper _mapper;
    
    private const float HARD_PENALTY = -1f;
    private const int DAYS_PER_WEEK = 5;
    private const int LATE_START_THRESHOLD = 6;

    // PRE-ALLOCATED BUFFERS (Zero Garbage Collection)
    private readonly DecodedGene[] _decodedBuffer;
    private readonly int[] _teacherMatrix;
    private readonly int[] _roomMatrix;
    private readonly int[] _groupMatrix;
    private readonly int[] _teacherHours;

    public FitnessEvaluator(TimetableMapper mapper)
    {
        _mapper = mapper;
        
        // Allocate ONCE per Evaluator instance
        _decodedBuffer = new DecodedGene[mapper.Courses.Count];
        _teacherMatrix = new int[mapper.T_max * mapper.S_max];
        _roomMatrix = new int[mapper.R_max * mapper.S_max];
        _groupMatrix = new int[mapper.GroupCount * mapper.S_max];
        _teacherHours = new int[mapper.T_max];
    }
    
    public float Evaluate(Genome genome)
    {
        if (genome.BrokenGenesMask != null)
            Array.Clear(genome.BrokenGenesMask, 0, genome.BrokenGenesMask.Length);

        // 1. Fast Decode directly into pre-allocated buffer
        DecodeGenome(genome.Genes);

        // 2. Clear Matrices
        Array.Clear(_teacherMatrix, 0, _teacherMatrix.Length);
        Array.Clear(_roomMatrix, 0, _roomMatrix.Length);
        Array.Clear(_groupMatrix, 0, _groupMatrix.Length);
        Array.Clear(_teacherHours, 0, _teacherHours.Length);

        int hardViolations = 0;

        // 3. THE GOD LOOP: Do almost everything in one single pass
        for (int i = 0; i < _decodedBuffer.Length; i++)
        {
            ref DecodedGene gene = ref _decodedBuffer[i]; // Use 'ref' to avoid struct copying
            var teacher = _mapper.Instructors[gene.T];
            var room = _mapper.Rooms[gene.R];
            var course = gene.Course;

            // H3: Room Capacity
            if (course.RequiredSlots > 0 && course.Students > room.Capacity)
            {
                hardViolations++;
                genome.SetBroken(i);
            }

            // H4: Teacher Qualified
            if (!teacher.Subjects.Contains(course.SubjectId))
            {
                hardViolations++;
                genome.SetBroken(i);
            }

            // H5: Room Type
            if (room.Type != course.RequiredRoomType)
            {
                hardViolations++;
                genome.SetBroken(i);
            }

            // H8 Part 1: Accumulate Hours
            _teacherHours[gene.T] += course.HoursPerSemester;

            // Loop through the slots required for this specific class
            for (int offset = 0; offset < course.RequiredSlots; offset++)
            {
                int currentSlot = gene.S + offset;
                
                // H6: Teacher Available (Using ultra-fast Bitmask!)
                if ((teacher.AvailableSlotsMask & (1UL << currentSlot)) == 0)
                {
                    hardViolations++;
                    genome.SetBroken(i);
                }

                // Booking Matrices indices
                int tIndex = (gene.T * _mapper.S_max) + currentSlot;
                int rIndex = (gene.R * _mapper.S_max) + currentSlot;
                int gIndex = (gene.GroupIndex * _mapper.S_max) + currentSlot;

                // H1: Teacher Single Booked
                if (_teacherMatrix[tIndex] == 1) { hardViolations++; genome.SetBroken(i); }
                _teacherMatrix[tIndex] = 1;

                // H2: Room Single Booked
                int existingOccupant = _roomMatrix[rIndex];
                if (existingOccupant > 0) 
                { 
                    hardViolations++; 
                    genome.SetBroken(i); 
                    genome.SetBroken(existingOccupant - 1); 
                }
                else { _roomMatrix[rIndex] = i + 1; }

                // H7: Group Single Booked
                if (_groupMatrix[gIndex] == 1) { hardViolations++; genome.SetBroken(i); }
                _groupMatrix[gIndex] = 1;
            }
        }

        // H8 Part 2: Teacher Max Hours Evaluation (Requires a separate quick loop over teachers)
        for (int t = 0; t < _mapper.T_max; t++)
        {
            if (_teacherHours[t] > _mapper.Instructors[t].HoursPerSemester)
            {
                hardViolations++;
                // Mark all courses taught by this teacher as broken
                for (int i = 0; i < _decodedBuffer.Length; i++)
                {
                    if (_decodedBuffer[i].T == t) genome.SetBroken(i);
                }
            }
        }

        if (hardViolations == 0)
        {
            return CalculateSoftFitness(); // Passed empty, accesses _decodedBuffer natively
        }

        return hardViolations * HARD_PENALTY;
    }

    private void DecodeGenome(int[] genes)
    {
        for (int i = 0; i < _mapper.Courses.Count; i++) 
        {
            var (t, r, s) = _mapper.Decode(genes[i]);
            var course = _mapper.Courses[i];
            
            // Write directly to pre-allocated buffer
            _decodedBuffer[i] = new DecodedGene 
            { 
                CourseIndex = i, 
                Course = course, 
                T = t, 
                R = r, 
                S = s, 
                GroupIndex = _mapper.GetGroupIndex(course.GroupId)
            };
        }
    }

    
    // ---  SOFT CONSTRAINT FUNCTIONS ---

    private float CalculateSoftFitness()
    {
        // Start with a massive base score so valid schedules always beat invalid ones
        float score = 1000f;

        // No parameters needed! They all read from the internal _decodedBuffer
        score -= (1f - EvaluateS1_TeacherPreferences()) * 500f;
        score -= (1f - EvaluateS2_StudentGaps()) * 300f;
        score -= (1f - EvaluateS3_LateClasses()) * 100f;
        score -= (1f - EvaluateS4_DailyBalance()) * 100f;

        return score;
    }

    private float EvaluateS1_TeacherPreferences()
    {
        int hits = 0;
        int total = 0;

        for (int i = 0; i < _decodedBuffer.Length; i++)
        {
            ref DecodedGene gene = ref _decodedBuffer[i]; // Ultra-fast struct reference
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
                
                // If the bit at 'slot' is NOT 0, it means the teacher prefers it!
                if ((teacher.PreferredSlotsMask & (1UL << slot)) != 0)
                {
                    hits++;
                }
            }
        }

        return total == 0 ? 1f : (float)hits / total;
    }

    private float EvaluateS2_StudentGaps()
    {
        int slotsPerDay = _mapper.S_max / DAYS_PER_WEEK;
        if (slotsPerDay < 1) return 1f;

        int size = _mapper.GroupCount * DAYS_PER_WEEK;
        int[] minSlot = ArrayPool<int>.Shared.Rent(size);
        int[] maxSlot = ArrayPool<int>.Shared.Rent(size);
        int[] slotCount = ArrayPool<int>.Shared.Rent(size);

        for (int i = 0; i < size; i++)
        {
            minSlot[i] = int.MaxValue;
            maxSlot[i] = -1;
            slotCount[i] = 0;
        }

        for (int i = 0; i < _decodedBuffer.Length; i++)
        {
            ref DecodedGene gene = ref _decodedBuffer[i];
            for (int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int slot = gene.S + offset;
                int day = slot / slotsPerDay;
                if (day >= DAYS_PER_WEEK)
                    continue;
                
                int index = gene.GroupIndex * DAYS_PER_WEEK + day;

                if (slot < minSlot[index])
                    minSlot[index] = slot;
                if (slot > maxSlot[index])
                    maxSlot[index] = slot;
                
                slotCount[index]++;
            }
        }

        int totalGaps = 0;
        int maxPossible = 0;

        for (int i = 0; i < size; i++)
        {
            if (maxSlot[i] == -1)
                continue;    // No classes this group-day
                
            int span = maxSlot[i] - minSlot[i] + 1;
            totalGaps += span - slotCount[i];
            maxPossible += Math.Max(0, slotsPerDay - 2);
        }

        ArrayPool<int>.Shared.Return(minSlot);
        ArrayPool<int>.Shared.Return(maxSlot);
        ArrayPool<int>.Shared.Return(slotCount);

        return maxPossible == 0 ? 1f : 1f - Math.Min(1f, (float)totalGaps / maxPossible);
    }

    private float EvaluateS3_LateClasses()
    {
        if (_decodedBuffer.Length == 0) return 1f;

        int slotsPerDay = _mapper.S_max / DAYS_PER_WEEK;
        int earlyStarts = 0;

        for (int i = 0; i < _decodedBuffer.Length; i++)
        {
            ref DecodedGene gene = ref _decodedBuffer[i];
            int slotInDay = slotsPerDay > 0 ? gene.S % slotsPerDay : gene.S;
            if (slotInDay < LATE_START_THRESHOLD)
                earlyStarts++;
        }

        return (float)earlyStarts / _decodedBuffer.Length;
    }

    private float EvaluateS4_DailyBalance()
    {
        int slotsPerDay = _mapper.S_max / DAYS_PER_WEEK;
        if (slotsPerDay < 1) return 1f;

        int size = _mapper.GroupCount * DAYS_PER_WEEK;
        int[] counts = ArrayPool<int>.Shared.Rent(size);
        Array.Clear(counts, 0, size);

        for (int i = 0; i < _decodedBuffer.Length; i++)
        {
            ref DecodedGene gene = ref _decodedBuffer[i];
            int day = gene.S / slotsPerDay;
            if (day >= DAYS_PER_WEEK)
                continue;
                
            counts[gene.GroupIndex * DAYS_PER_WEEK + day]++;
        }

        float totalCV = 0f;
        int groupsSeen = 0;

        for (int g = 0; g < _mapper.GroupCount; g++)
        {
            float mean = 0f;
            for (int d = 0; d < DAYS_PER_WEEK; d++)
                mean += counts[g * DAYS_PER_WEEK + d];
            mean /= DAYS_PER_WEEK;

            if (mean < 0.001f)
                continue;
            groupsSeen++;

            float variance = 0f;
            for (int d = 0; d < DAYS_PER_WEEK; d++)
            {
                float diff = counts[g * DAYS_PER_WEEK + d] - mean;
                variance += diff * diff;
            }
            variance /= DAYS_PER_WEEK;

            float cv = (float)Math.Sqrt(variance) / mean;
            totalCV += Math.Min(1f, cv);
        }

        ArrayPool<int>.Shared.Return(counts);

        if (groupsSeen == 0)
            return 1f;
            
        return 1f - (totalCV / groupsSeen);
    }
    // =====================================================================
    // DIAGNOSTIC TOOL: Tells you EXACTLY why the fitness is negative
    // =====================================================================
    public void AnalyzeViolations(Genome genome)
    {
        Console.WriteLine("\n=== DIAGNOZA BŁĘDÓW ===");
        DecodeGenome(genome.Genes);

        Array.Clear(_teacherMatrix, 0, _teacherMatrix.Length);
        Array.Clear(_roomMatrix, 0, _roomMatrix.Length);
        Array.Clear(_groupMatrix, 0, _groupMatrix.Length);
        Array.Clear(_teacherHours, 0, _teacherHours.Length);

        int h1 = 0, h2 = 0, h3 = 0, h4 = 0, h5 = 0, h6 = 0, h7 = 0, h8 = 0;

        for (int i = 0; i < _decodedBuffer.Length; i++)
        {
            ref DecodedGene gene = ref _decodedBuffer[i];
            var teacher = _mapper.Instructors[gene.T];
            var room = _mapper.Rooms[gene.R];
            var course = gene.Course;

            if (course.RequiredSlots > 0 && course.Students > room.Capacity) h3++;
            if (!teacher.Subjects.Contains(course.SubjectId)) h4++;
            if (room.Type != course.RequiredRoomType) h5++;

            _teacherHours[gene.T] += course.HoursPerSemester;

            for (int offset = 0; offset < course.RequiredSlots; offset++)
            {
                int currentSlot = gene.S + offset;
                
                if (!teacher.AvailableSlots.Contains(currentSlot)) h6++;

                int tIndex = (gene.T * _mapper.S_max) + currentSlot;
                int rIndex = (gene.R * _mapper.S_max) + currentSlot;
                int gIndex = (gene.GroupIndex * _mapper.S_max) + currentSlot;

                if (_teacherMatrix[tIndex] == 1) h1++;
                _teacherMatrix[tIndex] = 1;

                if (_roomMatrix[rIndex] > 0) h2++;
                else _roomMatrix[rIndex] = i + 1;

                if (_groupMatrix[gIndex] == 1) h7++;
                _groupMatrix[gIndex] = 1;
            }
        }

        for (int t = 0; t < _mapper.T_max; t++)
        {
            if (_teacherHours[t] > _mapper.Instructors[t].HoursPerSemester) h8++;
        }

        Console.WriteLine($"H1 (Wykładowca w 2 miejscach naraz): {h1}");
        Console.WriteLine($"H2 (Sala podwójnie zajęta):         {h2}");
        Console.WriteLine($"H3 (Za mała pojemność sali):        {h3}");
        Console.WriteLine($"H4 (Brak kwalifikacji wykładowcy):  {h4}");
        Console.WriteLine($"H5 (Zły typ sali np. brak lab):     {h5}");
        Console.WriteLine($"H6 (Wykładowca niedostępny w tych godzinach): {h6}");
        Console.WriteLine($"H7 (Grupa w 2 miejscach naraz):     {h7}");
        Console.WriteLine($"H8 (Przekroczono limit godzin):     {h8}");
        Console.WriteLine("===============================\n");
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
