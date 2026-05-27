using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public static class TimetableLoader
{
    // New method to load the Time Slots JSON
    public static TimeSlotConfig LoadTimeSlots(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Brak pliku z konfiguracją godzin: {filePath}");

        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<TimeSlotConfig>(json) ?? new TimeSlotConfig();
    }

    // Pass TimeSlotConfig instead of hardcoded slotsPerDay/startHourOfDay
    public static (List<Course> courses, List<Instructor> instructors, List<Room> rooms) LoadFromJson(
        string filePath, 
        TimeSlotConfig timeConfig)
    {
        string json = File.ReadAllText(filePath);
        using JsonDocument doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var rooms = JsonSerializer.Deserialize<List<Room>>(root.GetProperty("rooms").GetRawText()) ?? new List<Room>();
        var courses = JsonSerializer.Deserialize<List<Course>>(root.GetProperty("courses").GetRawText()) ?? new List<Course>();
        var instructors = JsonSerializer.Deserialize<List<Instructor>>(root.GetProperty("instructors").GetRawText()) ?? new List<Instructor>();

        // 2. Adapt Courses (HoursPerSemester -> RequiredSlots using 30h = 1 slot of 1.5h per week)
        foreach (var course in courses)
        {
            var courseElement = root.GetProperty("courses").EnumerateArray()
                .FirstOrDefault(c => c.GetProperty("id").GetString() == course.Id);

            if (courseElement.ValueKind != JsonValueKind.Undefined && 
                courseElement.TryGetProperty("hours_per_semester", out JsonElement hoursEl))
            {
                int hoursPerSemester = hoursEl.GetInt32();
                // 30 hours per semester = 1 block of 1.5h per week
                course.RequiredSlots = (int)Math.Round(hoursPerSemester / 30.0, MidpointRounding.AwayFromZero); 
                if (course.RequiredSlots == 0) course.RequiredSlots = 1;
            }
            else
            {
                course.RequiredSlots = 1; 
            }
        }

        // 3. Adapt Instructors using the real Time Slots configuration
        var daysMap = new Dictionary<string, int> 
        { 
            {"Poniedziałek", 0}, {"Wtorek", 1}, {"Środa", 2}, {"Czwartek", 3}, {"Piątek", 4},
            // Fallbacks for English LLM output
            {"Mon", 0}, {"Tue", 1}, {"Wed", 2}, {"Thu", 3}, {"Fri", 4}
        };

        foreach (var inst in instructors)
        {
            var availableSlots = Enumerable.Range(0, 5 * timeConfig.SlotsPerDay).ToList();
            var preferredSlots = new List<int>();

            var instElement = root.GetProperty("instructors").EnumerateArray()
                .FirstOrDefault(i => i.GetProperty("id").GetString() == inst.Id);
            
            if (instElement.ValueKind != JsonValueKind.Undefined && 
                instElement.TryGetProperty("parsed_preferences", out JsonElement prefs) && 
                prefs.ValueKind != JsonValueKind.Null)
            {
                // A. Handle Forbidden Slots
                if (prefs.TryGetProperty("forbidden_slots", out JsonElement forbiddenSlots))
                {
                    foreach (var forbid in forbiddenSlots.EnumerateArray())
                    {
                        string? day = forbid.GetProperty("day").GetString();
                        
                        if (!string.IsNullOrEmpty(day) && daysMap.TryGetValue(day, out int dayIdx))
                        {
                            double from = forbid.GetProperty("from").GetDouble();
                            double to = forbid.GetProperty("to").GetDouble();
                            
                            foreach (var slot in timeConfig.Slots)
                            {
                                // If the slot overlaps with the forbidden timeframe, remove it
                                // Overlap logic: slot.start < forbid.to AND slot.end > forbid.from
                                if (slot.StartHour < to && slot.EndHour > from)
                                {
                                    int absoluteSlotIndex = (dayIdx * timeConfig.SlotsPerDay) + slot.Index;
                                    availableSlots.Remove(absoluteSlotIndex);
                                }
                            }
                        }
                    }
                }

                // B. Handle Preferred Slots
                if (prefs.TryGetProperty("preferred_days", out JsonElement prefDays))
                {
                    double prefStart = prefs.TryGetProperty("preferred_hours_start", out JsonElement ps) && ps.ValueKind != JsonValueKind.Null ? ps.GetDouble() : 8.0;
                    double prefEnd = prefs.TryGetProperty("preferred_hours_end", out JsonElement pe) && pe.ValueKind != JsonValueKind.Null ? pe.GetDouble() : 20.0;

                    foreach (var dayEl in prefDays.EnumerateArray())
                    {
                        string? dayName = dayEl.GetString();
                        if (!string.IsNullOrEmpty(dayName) && daysMap.TryGetValue(dayName, out int dayIdx))
                        {
                            foreach (var slot in timeConfig.Slots)
                            {
                                // If the slot is fully entirely inside the preferred timeframe
                                if (slot.StartHour >= prefStart && slot.EndHour <= prefEnd)
                                {
                                    int absoluteSlotIndex = (dayIdx * timeConfig.SlotsPerDay) + slot.Index;
                                    if (availableSlots.Contains(absoluteSlotIndex))
                                    {
                                        preferredSlots.Add(absoluteSlotIndex);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            inst.AvailableSlots = availableSlots;
            inst.PreferredSlots = preferredSlots;
        }

        return (courses, instructors, rooms);
    }
}