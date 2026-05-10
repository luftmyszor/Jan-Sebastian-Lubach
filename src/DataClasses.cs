using System.Collections.Generic;

// NEW CLASS: Represents a specific group of students
public class StudentGroup
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public int Year { get; set; }
    public int Students { get; set; }
}

public class Instructor
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    
    // We will populate this later by parsing "preferences_text"
    public List<int> AvailableSlots { get; set; } = new List<int>(); 
    public List<string> Subjects { get; set; } = new List<string>();
    
    public int HoursPerSemester { get; set; } 
}

public class Room
{
    public required string Id { get; set; }
    public required string Name { get; set; } 
    public required string Type { get; set; }
    public int Capacity { get; set; }
}

public class Course
{
    public required string Id { get; set; }
    public required string SubjectId { get; set; }
    public required string Name { get; set; } 
    public required string Type { get; set; } 
    public required string GroupId { get; set; }
    public int Students { get; set; }
    
    public required string RequiredRoomType { get; set; }
    public int RequiredSlots { get; set; } 
}