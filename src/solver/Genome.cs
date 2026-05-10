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
    private const float HARD_PENALTY = -1f; 

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

        // 2. DO NOT reun in parallel - causes thread starvation
            h1_violations = CheckH1_TeacherSingleBooked(decodedSchedule);
            h2_violations = CheckH2_RoomSingleBooked(decodedSchedule);
            h3_violations = CheckH3_RoomCapacity(decodedSchedule);
            h4_violations = CheckH4_TeacherQualified(decodedSchedule);
            h5_violations = CheckH5_RoomTypeCorrect(decodedSchedule);
            h6_violations = CheckH6_TeacherAvailable(decodedSchedule);
            h7_violations = CheckH7_StudentGroupSingleBooked(decodedSchedule);
        

        int totalHardViolations = h1_violations + h2_violations + h3_violations + 
                                  h4_violations + h5_violations + h6_violations + 
                                  h7_violations;

        // If Violations = 0 (
        if (totalHardViolations == 0)
        {
            // TODO: Run S1, S2, S3, S4
            // return CalculateSoftFitness();
            return 100f; // Perfect score placeholder
        }

        // Return a heavily penalized score based on how many rules it broke
        //return totalHardViolations * HARD_PENALTY;
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

    private int CheckH1_TeacherSingleBooked(List<DecodedGene> schedule)
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
                }
                teacherBookingMatrix[index] = 1; // Mark as booked
            }
        }
        
        ArrayPool<int>.Shared.Return(teacherBookingMatrix);
        return violations;
    }

    private int CheckH2_RoomSingleBooked(List<DecodedGene> schedule)
    {
        int violations = 0;
        int[] roomBookingMatrix = ArrayPool<int>.Shared.Rent(_mapper.R_max * _mapper.S_max);
        Array.Clear(roomBookingMatrix, 0, roomBookingMatrix.Length);

        foreach (var gene in schedule)
        {
            // The Zero-Allocation Loop!
            for (int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int currentSlot = gene.S + offset;
                int index = (gene.R * _mapper.S_max) + currentSlot;
                
                if (roomBookingMatrix[index] == 1) 
                {
                    violations++; // Room is double-booked!
                }
                roomBookingMatrix[index] = 1; 
            }
        }
        
        ArrayPool<int>.Shared.Return(roomBookingMatrix);
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
            for (int offset = 0; offset < gene.Course.RequiredSlots; offset++)
            {
                int currentSlot = gene.S + offset;
                
                if (!teacher.AvailableSlots.Contains(currentSlot))
                {
                    violations++;
                    break; // One broken slot ruins the whole class placement
                }
            }
        }
        return violations;
    }

    private int CheckH7_StudentGroupSingleBooked(List<DecodedGene> schedule)
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
                }
                groupBookingMatrix[index] = 1;
            }
        }

        ArrayPool<int>.Shared.Return(groupBookingMatrix);
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
        public int GroupIndex;
    }
}