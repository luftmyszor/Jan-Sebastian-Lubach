using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public static class TimetableLoader
{
    public static (List<Course> courses, List<Instructor> instructors, List<Room> rooms) LoadFromJson(string filePath, int slotsPerDay = 10, int startHourOfDay = 8)
    {
        string json = File.ReadAllText(filePath);
        using JsonDocument doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Use ?? new List<T>() to guarantee we never return null, fixing CS8619
        var rooms = JsonSerializer.Deserialize<List<Room>>(root.GetProperty("rooms").GetRawText()) ?? new List<Room>();
        var courses = JsonSerializer.Deserialize<List<Course>>(root.GetProperty("courses").GetRawText()) ?? new List<Course>();
        var instructors = JsonSerializer.Deserialize<List<Instructor>>(root.GetProperty("instructors").GetRawText()) ?? new List<Instructor>();

        // 2. Adapt Courses (HoursPerSemester -> RequiredSlots)
        foreach (var course in courses)
        {
            var courseElement = root.GetProperty("courses").EnumerateArray()
                .FirstOrDefault(c => c.GetProperty("id").GetString() == course.Id);

            // Check if element is valid before accessing properties (Fixes CS8602)
            if (courseElement.ValueKind != JsonValueKind.Undefined && 
                courseElement.TryGetProperty("hours_per_semester", out JsonElement hoursEl))
            {
                int hoursPerSemester = hoursEl.GetInt32();
                course.RequiredSlots = (int)Math.Ceiling(hoursPerSemester / 15.0); 
                if (course.RequiredSlots == 0) course.RequiredSlots = 1;
            }
            else
            {
                course.RequiredSlots = 1; // Failsafe default
            }
        }

        // 3. Adapt Instructors (LLM Preferences -> 1D Slot Indexes)
        var daysMap = new Dictionary<string, int> 
        { 
            {"Poniedziałek", 0}, {"Wtorek", 1}, {"Środa", 2}, {"Czwartek", 3}, {"Piątek", 4} 
        };

        foreach (var inst in instructors)
        {
            var availableSlots = Enumerable.Range(0, 5 * slotsPerDay).ToList();
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
                        
                        // Check if day is not null before using it as dictionary key (Fixes CS8600/CS8604)
                        if (!string.IsNullOrEmpty(day) && daysMap.TryGetValue(day, out int dayIdx))
                        {
                            int from = forbid.GetProperty("from").GetInt32();
                            int to = forbid.GetProperty("to").GetInt32();
                            
                            int startSlot = Math.Max(0, from - startHourOfDay);
                            int endSlot = Math.Min(slotsPerDay, to - startHourOfDay);
                            
                            for (int s = startSlot; s < endSlot; s++)
                            {
                                availableSlots.Remove((dayIdx * slotsPerDay) + s);
                            }
                        }
                    }
                }

                // B. Handle Preferred Slots
                if (prefs.TryGetProperty("preferred_days", out JsonElement prefDays))
                {
                    int? prefStart = prefs.TryGetProperty("preferred_hours_start", out JsonElement ps) && ps.ValueKind != JsonValueKind.Null ? ps.GetInt32() : null;
                    int? prefEnd = prefs.TryGetProperty("preferred_hours_end", out JsonElement pe) && pe.ValueKind != JsonValueKind.Null ? pe.GetInt32() : null;

                    foreach (var dayEl in prefDays.EnumerateArray())
                    {
                        string? dayName = dayEl.GetString();
                        if (!string.IsNullOrEmpty(dayName) && daysMap.TryGetValue(dayName, out int dayIdx))
                        {
                            int startSlot = Math.Max(0, (prefStart ?? startHourOfDay) - startHourOfDay);
                            int endSlot = Math.Min(slotsPerDay, (prefEnd ?? (startHourOfDay + slotsPerDay)) - startHourOfDay);

                            for (int s = startSlot; s < endSlot; s++)
                            {
                                int slotIndex = (dayIdx * slotsPerDay) + s;
                                if (availableSlots.Contains(slotIndex))
                                {
                                    preferredSlots.Add(slotIndex);
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