using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public class DataParserModule
{
    public UniversityData ParseAndValidate(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Nie znaleziono pliku: {filePath}");

        string json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<UniversityData>(json);

        ValidateData(data);
        return data;
    }

    private void ValidateData(UniversityData data)
    {
        if (data == null) throw new Exception("Błąd deserializacji danych z pliku JSON.");

        // Pobranie puli istniejących ID dla walidacji krzyżowej
        var existingRoomTypes = data.Rooms?.Select(r => r.Type).ToHashSet() ?? new HashSet<string>();
        var existingSubjectIds = data.Courses?.Select(c => c.SubjectId).ToHashSet() ?? new HashSet<string>();
        var existingGroupIds = data.StudentGroups?.Select(g => g.Id).ToHashSet() ?? new HashSet<string>();

        // Walidacja Prowadzących
        if (data.Instructors == null || !data.Instructors.Any())
            throw new Exception("Błąd: Brak prowadzących w danych.");

        if (data.Instructors.Select(i => i.Id).Distinct().Count() != data.Instructors.Count)
            throw new Exception("Błąd: Wykryto zduplikowane ID prowadzących.");

        foreach (var inst in data.Instructors)
        {
            if (string.IsNullOrWhiteSpace(inst.Id))
                throw new Exception($"Błąd: Prowadzący {inst.Name} nie posiada unikalnego ID.");
            if (inst.HoursPerSemester <= 0)
                throw new Exception($"Błąd: Prowadzący {inst.Id} ma nieprawidłową liczbę godzin w semestrze ({inst.HoursPerSemester}).");

            // Walidacja czy przedmioty prowadzącego w ogóle istnieją
            foreach (var subject in inst.Subjects)
            {
                if (!existingSubjectIds.Contains(subject))
                    throw new Exception($"Błąd: Prowadzący {inst.Id} ma przypisany przedmiot {subject}, który nie istnieje na liście kursów.");
            }
        }

        // Walidacja Grup Studenckich
        if (data.StudentGroups == null || !data.StudentGroups.Any())
            throw new Exception("Błąd: Brak grup studenckich w danych.");

        if (data.StudentGroups.Select(g => g.Id).Distinct().Count() != data.StudentGroups.Count)
            throw new Exception("Błąd: Wykryto zduplikowane ID grup studenckich.");

        // Walidacja Sal
        if (data.Rooms == null || !data.Rooms.Any())
            throw new Exception("Błąd: Brak sal w danych.");

        if (data.Rooms.Select(r => r.Id).Distinct().Count() != data.Rooms.Count)
            throw new Exception("Błąd: Wykryto zduplikowane ID sal.");

        foreach (var room in data.Rooms)
        {
            if (string.IsNullOrWhiteSpace(room.Id))
                throw new Exception("Błąd: Jedna z sal nie posiada unikalnego ID.");
            if (room.Capacity <= 0)
                throw new Exception($"Błąd: Sala {room.Id} ma nieprawidłową pojemność ({room.Capacity}).");
        }

        // Walidacja Przedmiotów
        if (data.Courses == null || !data.Courses.Any())
            throw new Exception("Błąd: Brak przedmiotów w danych.");

        if (data.Courses.Select(c => c.Id).Distinct().Count() != data.Courses.Count)
            throw new Exception("Błąd: Wykryto zduplikowane ID przedmiotów.");

        foreach (var course in data.Courses)
        {
            if (string.IsNullOrWhiteSpace(course.Id))
                throw new Exception("Błąd: Jeden z przedmiotów nie posiada unikalnego ID.");
            if (course.HoursPerSemester <= 0)
                throw new Exception($"Błąd: Przedmiot {course.Id} ma nieprawidłową liczbę godzin w semestrze ({course.HoursPerSemester}).");

            // Walidacja relacyjna dla przedmiotów
            if (!existingRoomTypes.Contains(course.RequiredRoomType))
                throw new Exception($"Błąd: Przedmiot {course.Id} wymaga sali typu {course.RequiredRoomType}, której nie ma w bazie.");
            if (!existingGroupIds.Contains(course.GroupId))
                throw new Exception($"Błąd: Przedmiot {course.Id} jest przypisany do nieistniejącej grupy {course.GroupId}.");
        }

        Console.WriteLine("Walidacja kompletności, zakresów i spójności krzyżowej zakończona sukcesem.");
    }
}