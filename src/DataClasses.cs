using System.Collections.Generic;

public class Instructor
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<int> AvailableSlots { get; set; } = new List<int>();
    public List<string> Subjects { get; set; } = new List<string>();
}

public class Room
{
    public string Id { get; set; }
    public string Type { get; set; }
    public int Capacity { get; set; }
}

public class Course
{
    public string Id { get; set; }
    public string SubjectId { get; set; }
    public string RequiredRoomType { get; set; }
    public int RequiredSlots { get; set; } 
}