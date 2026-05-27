using System.Collections.Generic;
using System.Text.Json.Serialization;

public class UniversityData
{
    [JsonPropertyName("instructors")] public List<Instructor> Instructors { get; set; } = new();
    [JsonPropertyName("rooms")] public List<Room> Rooms { get; set; } = new();
    [JsonPropertyName("student_groups")] public List<StudentGroup> StudentGroups { get; set; } = new();
    [JsonPropertyName("courses")] public List<Course> Courses { get; set; } = new();
}

public class StudentGroup
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("year")] public int Year { get; set; }
    [JsonPropertyName("students")] public int Students { get; set; }
}

public class Instructor
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("subjects")] public List<string> Subjects { get; set; } = new();
    [JsonPropertyName("preferences_text")] public string PreferencesText { get; set; }
    [JsonPropertyName("hours_per_semester")] public int HoursPerSemester { get; set; }
    [JsonPropertyName("parsed_preferences")] public InstructorPreferences ParsedPreferences { get; set; }
}

public class Room
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("capacity")] public int Capacity { get; set; }
    [JsonPropertyName("availability")] public Dictionary<string, List<int>> Availability { get; set; }
}

public class Course
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("subject_id")] public string SubjectId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("group_id")] public string GroupId { get; set; }
    [JsonPropertyName("students")] public int Students { get; set; }
    [JsonPropertyName("hours_per_semester")] public int HoursPerSemester { get; set; }
    [JsonPropertyName("required_room_type")] public string RequiredRoomType { get; set; }
}

public class InstructorPreferences
{
    [JsonPropertyName("preferred_days")] public List<string> PreferredDays { get; set; } = new();
    [JsonPropertyName("preferred_hours_start")] public int? PreferredHoursStart { get; set; }
    [JsonPropertyName("preferred_hours_end")] public int? PreferredHoursEnd { get; set; }
    [JsonPropertyName("forbidden_slots")] public List<ForbiddenSlot> ForbiddenSlots { get; set; } = new();
    [JsonPropertyName("min_start_hour")] public int? MinStartHour { get; set; }
}

public class ForbiddenSlot
{
    [JsonPropertyName("day")] public string Day { get; set; }
    [JsonPropertyName("from")] public int From { get; set; }
    [JsonPropertyName("to")] public int To { get; set; }
}