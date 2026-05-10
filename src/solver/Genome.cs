using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


public record struct Genome
{
    public int[] Genes;
    public float Fitness;
    public bool IsEvaluated;

    public Genome(int[] genes)
    {
        Genes = genes;
        Fitness = 0f;
        IsEvaluated = false;
    }
}


public class FitnessEvaluator
{
    private readonly TimetableMapper _mapper;
    
    // Weight of penalty per violation. ly.
    private const float HARD_PENALTY = -1000f; 

    public FitnessEvaluator(TimetableMapper mapper)
    {
        _mapper = mapper;
    }

    public float Evaluate(int[] genes)
    {
        var decodedSchedule = DecodeGenome(genes);

        int h1_violations = 0, h2_violations = 0, h3_violations = 0;
        int h4_violations = 0, h5_violations = 0, h6_violations = 0;
        int h7_violations = 0;

        // 2. Run the Hard Constraints in Parallel
        // Note: We already run the POPULATION in parallel. Running individual constraints 
        // in parallel can sometimes cause thread-overhead slowdowns, but this is how you do it!
        Parallel.Invoke(
            () => h1_violations = CheckH1_TeacherSingleBooked(decodedSchedule),
            () => h2_violations = CheckH2_RoomSingleBooked(decodedSchedule),
            () => h3_violations = CheckH3_RoomCapacity(decodedSchedule),
            () => h4_violations = CheckH4_TeacherQualified(decodedSchedule),
            () => h5_violations = CheckH5_RoomTypeCorrect(decodedSchedule),
            () => h6_violations = CheckH6_TeacherAvailable(decodedSchedule),
            () => h7_violations = CheckH7_StudentGroupSingleBooked(decodedSchedule)
        );

        int totalHardViolations = h1_violations + h2_violations + h3_violations + 
                                  h4_violations + h5_violations + h6_violations;

        // If Violations = 0 (as per your diagram), you would run Soft Constraints here.
        if (totalHardViolations == 0)
        {
            // TODO: Run S1, S2, S3, S4
            // return CalculateSoftFitness();
            return 100f; // Perfect score placeholder
        }

        // Return a heavily penalized score based on how many rules it broke
        return totalHardViolations * HARD_PENALTY;
    }

    // --- DECODER HELPER ---

    private List<DecodedGene> DecodeGenome(int[] genes)
    {
        var schedule = new List<DecodedGene>(_mapper.Courses.Count);
        
        for (int i = 0; i < _mapper.Courses.Count; i++)  // Loop through actual courses, not rented buffer
        {
            var (t, r, s) = _mapper.Decode(genes[i]);
            var course = _mapper.Courses[i];
            
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
                OccupiedSlots = occupiedSlots 
            });
        }
        return schedule;
    }

    // --- HARD CONSTRAINT FUNCTIONS ---

    private int CheckH1_TeacherSingleBooked(List<DecodedGene> schedule)
    {
        int violations = 0;
        var teacherGroups = schedule.GroupBy(g => g.T);

        foreach (var group in teacherGroups)
        {
            // Check if any of this teacher's occupied slots have duplicates
            var allOccupiedSlots = group.SelectMany(g => g.OccupiedSlots).ToList();
            var uniqueSlots = new HashSet<int>();
            
            foreach (var slot in allOccupiedSlots)
            {
                if (!uniqueSlots.Add(slot)) violations++;
            }
        }
        return violations;
    }

    private int CheckH2_RoomSingleBooked(List<DecodedGene> schedule)
    {
        int violations = 0;
        var roomGroups = schedule.GroupBy(g => g.R);

        foreach (var group in roomGroups)
        {
            // Check if any of this room's occupied slots have duplicates
            var allOccupiedSlots = group.SelectMany(g => g.OccupiedSlots).ToList();
            var uniqueSlots = new HashSet<int>();
            
            foreach (var slot in allOccupiedSlots)
            {
                if (!uniqueSlots.Add(slot)) violations++; // Duplicate found!
            }
        }
        return violations;
    }

    private int CheckH3_RoomCapacity(List<DecodedGene> schedule)
    {
        int violations = 0;
        foreach (var gene in schedule)
        {
            var room = _mapper.Rooms[gene.R];
            if (gene.Course.RequiredSlots > 0 && gene.Course.Students > room.Capacity) 
            {
                violations++;
            }
        }
        return violations;
    }

    private int CheckH4_TeacherQualified(List<DecodedGene> schedule)
    {
        int violations = 0;
        foreach (var gene in schedule)
        {
            var teacher = _mapper.Instructors[gene.T];
            if (!teacher.Subjects.Contains(gene.Course.SubjectId))
            {
                violations++;
            }
        }
        return violations;
    }

    private int CheckH5_RoomTypeCorrect(List<DecodedGene> schedule)
    {
        int violations = 0;
        foreach (var gene in schedule)
        {
            var room = _mapper.Rooms[gene.R];
            if (room.Type != gene.Course.RequiredRoomType)
            {
                violations++;
            }
        }
        return violations;
    }

    private int CheckH6_TeacherAvailable(List<DecodedGene> schedule)
    {
        int violations = 0;
        foreach (var gene in schedule)
        {
            var teacher = _mapper.Instructors[gene.T];
            // The teacher MUST be available for EVERY slot the course requires
            foreach (var slot in gene.OccupiedSlots)
            {
                if (!teacher.AvailableSlots.Contains(slot))
                {
                    violations++;
                    break;
                }
            }
        }
        return violations;
    }

    private int CheckH7_StudentGroupSingleBooked(List<DecodedGene> schedule)
    {
        int violations = 0;
        
        // Group all scheduled events by the Student Group ID (e.g., "IS1A")
        var studentGroups = schedule.GroupBy(g => g.Course.GroupId);

        foreach (var group in studentGroups)
        {
            // Gather every single slot this specific group of students is supposed to be in class
            var allOccupiedSlots = group.SelectMany(g => g.OccupiedSlots).ToList();
            var uniqueSlots = new HashSet<int>();
            
            foreach (var slot in allOccupiedSlots)
            {
                // If we can't add the slot to the set, it means this student group 
                // is scheduled for two different classes at the exact same time!
                if (!uniqueSlots.Add(slot)) 
                {
                    violations++;
                }
            }
        }
        return violations;
    }

    // Small struct to hold decoded data so we don't decode multiple times
    private struct DecodedGene
    {
        public int CourseIndex;
        public Course Course;
        public int T;
        public int R;
        public int S;
        public List<int> OccupiedSlots;
    }
}