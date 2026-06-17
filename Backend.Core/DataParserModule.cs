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
        if (data == null) throw new Exception("Blad deserializacji danych z pliku JSON.");

        // Pobranie puli istniejacych ID dla walidacji krzyzowej
        var existingRoomTypes = data.Rooms?.Select(r => r.Type).ToHashSet() ?? new HashSet<string>();
        var existingSubjectIds = data.Courses?.Select(c => c.SubjectId).ToHashSet() ?? new HashSet<string>();
        var existingGroupIds = data.StudentGroups?.Select(g => g.Id).ToHashSet() ?? new HashSet<string>();

        // Walidacja Prowadzacych
        if (data.Instructors == null || !data.Instructors.Any())
            throw new Exception("Blad: Brak prowadzacych w danych.");

        if (data.Instructors.Select(i => i.Id).Distinct().Count() != data.Instructors.Count)
            throw new Exception("Blad: Wykryto zduplikowane ID prowadzacych.");

        foreach (var inst in data.Instructors)
        {
            if (string.IsNullOrWhiteSpace(inst.Id))
                throw new Exception($"Blad: Prowadzacy {inst.Name} nie posiada unikalnego ID.");
            if (inst.HoursPerSemester <= 0)
                throw new Exception($"Blad: Prowadzacy {inst.Id} ma nieprawidlowa liczbe godzin w semestrze ({inst.HoursPerSemester}).");

            // Walidacja czy przedmioty prowadzacego w ogole istnieja
            foreach (var subject in inst.Subjects)
            {
                if (!existingSubjectIds.Contains(subject))
                    throw new Exception($"Blad: Prowadzacy {inst.Id} ma przypisany przedmiot {subject}, ktory nie istnieje na liscie kursow.");
            }
        }

        // Walidacja Grup Studenckich
        if (data.StudentGroups == null || !data.StudentGroups.Any())
            throw new Exception("Blad: Brak grup studenckich w danych.");

        if (data.StudentGroups.Select(g => g.Id).Distinct().Count() != data.StudentGroups.Count)
            throw new Exception("Blad: Wykryto zduplikowane ID grup studenckich.");

        // Walidacja Sal
        if (data.Rooms == null || !data.Rooms.Any())
            throw new Exception("Blad: Brak sal w danych.");

        if (data.Rooms.Select(r => r.Id).Distinct().Count() != data.Rooms.Count)
            throw new Exception("Blad: Wykryto zduplikowane ID sal.");

        foreach (var room in data.Rooms)
        {
            if (string.IsNullOrWhiteSpace(room.Id))
                throw new Exception("Blad: Jedna z sal nie posiada unikalnego ID.");
            if (room.Capacity <= 0)
                throw new Exception($"Blad: Sala {room.Id} ma nieprawidlowa pojemnosc ({room.Capacity}).");
        }

        // Walidacja Przedmiotow
        if (data.Courses == null || !data.Courses.Any())
            throw new Exception("Blad: Brak przedmiotow w danych.");

        if (data.Courses.Select(c => c.Id).Distinct().Count() != data.Courses.Count)
            throw new Exception("Blad: Wykryto zduplikowane ID przedmiotow.");

        foreach (var course in data.Courses)
        {
            if (string.IsNullOrWhiteSpace(course.Id))
                throw new Exception("Blad: Jeden z przedmiotow nie posiada unikalnego ID.");
            if (course.HoursPerSemester <= 0)
                throw new Exception($"Blad: Przedmiot {course.Id} ma nieprawidlowa liczbe godzin w semestrze ({course.HoursPerSemester}).");

            // Walidacja relacyjna dla przedmiotow
            if (!existingRoomTypes.Contains(course.RequiredRoomType))
                throw new Exception($"Blad: Przedmiot {course.Id} wymaga sali typu {course.RequiredRoomType}, ktorej nie ma w bazie.");
            if (!existingGroupIds.Contains(course.GroupId))
                throw new Exception($"Blad: Przedmiot {course.Id} jest przypisany do nieistniejacei grupy {course.GroupId}.");
        }

        Console.WriteLine("Walidacja kompletnosci, zakresow i spojnosci krzyzowej zakonczona sukcesem.");
    }
}