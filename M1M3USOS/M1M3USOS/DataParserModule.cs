using System;
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

        // Walidacja Prowadzących
        if (data.Instructors == null || !data.Instructors.Any())
            throw new Exception("Błąd krytyczny: Brak prowadzących w danych.");

        foreach (var inst in data.Instructors)
        {
            if (string.IsNullOrWhiteSpace(inst.Id))
                throw new Exception($"Błąd: Prowadzący {inst.Name} nie posiada unikalnego ID.");
            if (inst.HoursPerSemester <= 0)
                throw new Exception($"Błąd: Prowadzący {inst.Id} ma nieprawidłową liczbę godzin w semestrze ({inst.HoursPerSemester}).");
        }

        // Walidacja Sal
        if (data.Rooms == null || !data.Rooms.Any())
            throw new Exception("Błąd krytyczny: Brak sal w danych.");

        foreach (var room in data.Rooms)
        {
            if (string.IsNullOrWhiteSpace(room.Id))
                throw new Exception("Błąd: Jedna z sal nie posiada unikalnego ID.");
            if (room.Capacity <= 0)
                throw new Exception($"Błąd: Sala {room.Id} ma nieprawidłową pojemność ({room.Capacity}).");
            if (string.IsNullOrWhiteSpace(room.Type))
                throw new Exception($"Błąd: Sala {room.Id} nie ma zdefiniowanego typu.");
        }

        // Walidacja Przedmiotów
        if (data.Courses == null || !data.Courses.Any())
            throw new Exception("Błąd krytyczny: Brak przedmiotów w danych.");

        foreach (var course in data.Courses)
        {
            if (string.IsNullOrWhiteSpace(course.Id))
                throw new Exception("Błąd: Jeden z przedmiotów nie posiada unikalnego ID.");
            if (course.Students <= 0)
                throw new Exception($"Błąd: Przedmiot {course.Id} ma nieprawidłową liczbę studentów ({course.Students}).");
            if (course.HoursPerSemester <= 0)
                throw new Exception($"Błąd: Przedmiot {course.Id} ma nieprawidłową liczbę godzin w semestrze ({course.HoursPerSemester}).");
            if (string.IsNullOrWhiteSpace(course.RequiredRoomType))
                throw new Exception($"Błąd: Przedmiot {course.Id} nie posiada wymaganego typu sali.");
        }

        Console.WriteLine("Walidacja kompletności i zakresów danych (Moduł 1) zakończona sukcesem.");
    }
}