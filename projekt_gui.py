import json
import os
import tkinter as tk
from tkinter import messagebox, ttk


class ScheduleApp:

    def __init__(self, root):
        self.root = root
        self.root.title("Projekt informatyka - Harmonogram Uczelniany")
        self.root.geometry("1300x750")
        self.root.configure(bg="#F4F7FC")

        # Godziny zajęć odwzorowane z widoku UI
        self.days = ["Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek"]
        self.time_slots = [
            "08:00 - 09:30",
            "09:45 - 11:15",
            "11:30 - 13:30",  # Przerwa obiadowa
            "14:00 - 15:30",
            "15:45 - 17:15",
        ]

        # --- WCZYTYWANIE I PARSOWANIE DANYCH Z NOWEGO PLIKU JSON ---
        folder_skryptu = os.path.dirname(os.path.abspath(__file__))
        self.db_schedule = self.load_and_parse_json(
        os.path.join(folder_skryptu, "dane_nowe.json")
        )

        # Konfiguracja stylów wizualnych
        self.style = ttk.Style()
        self.style.theme_use("clam")
        self.style.configure(
            "TCombobox", padding=5, background="white", relief="flat"
        )
        self.style.configure(
            "Sidebar.TFrame", background="white", relief="flat"
        )

        self.setup_ui()
        self.populate_filters()

    def load_and_parse_json(self, filename):
        """Wczytuje zaawansowany plik JSON i mapuje strukturę relacyjną na siatkę zajęć"""
        if not os.path.exists(filename):
            messagebox.showerror(
                "Błąd", f"Nie znaleziono pliku bazy danych: {filename}"
            )
            return []

        try:
            with open(filename, "r", encoding="utf-8") as file:
                data = json.load(file)

            parsed_schedule = []

            # Słowniki pomocnicze do szybkiego wyszukiwania relacji
            instructors = {inst["id"]: inst for inst in data["instructors"]}
            rooms_by_type = {}
            for room in data["rooms"]:
                rooms_by_type.setdefault(room["type"], []).append(room["name"])

            groups = {g["id"]: g["name"] for g in data["student_groups"]}

            # Liczniki ułatwiające równomierne rozłożenie zajęć w planie
            group_slot_counters = {}

            for idx, course in enumerate(data["courses"]):
                group_id = course["group_id"]
                group_name = groups.get(group_id, group_id)

                if group_id not in group_slot_counters:
                    group_slot_counters[group_id] = 0

                # Przypisanie dnia i godziny (pomijając przerwę obiadową na slocie indeksu 2)
                slot_index = group_slot_counters[group_id]
                day_idx = slot_index % len(self.days)
                time_idx = (slot_index // len(self.days)) % len(
                    self.time_slots
                )

                if (
                    time_idx == 2
                ):  # Rezerwacja slotu 11:30 - 13:30 na przerwę obiadową
                    slot_index += len(self.days)
                    day_idx = slot_index % len(self.days)
                    time_idx = (slot_index // len(self.days)) % len(
                        self.time_slots
                    )

                group_slot_counters[group_id] = slot_index + 1

                day = self.days[day_idx]
                time_str = self.time_slots[time_idx]

                # Dopasowanie wykładowcy uczącego danego przedmiotu
                lecturer_name = "Nieprzypisany"
                for inst in data["instructors"]:
                    if course["subject_id"] in inst["subjects"]:
                        lecturer_name = inst["name"]
                        break

                # Dopasowanie odpowiedniego typu sali
                room_type = course["required_room_type"]
                available_rooms = rooms_by_type.get(room_type, ["Sala Ogólna"])
                room_name = available_rooms[idx % len(available_rooms)]

                # Budowa płaskiego rekordu dla widoku UI
                parsed_schedule.append(
                    {
                        "klasa": group_name,
                        "wykladowca": lecturer_name,
                        "przedmiot": course["name"],
                        "typ": course["type"].upper(),
                        "dzien": day,
                        "godzina": time_str,
                        "sala": room_name,
                    }
                )

            return parsed_schedule

        except Exception as e:
            messagebox.showerror(
                "Błąd parsowania", f"Problem przy przetwarzaniu pliku: {e}"
            )
            return []

    def setup_ui(self):
        # Główny układ okna (2 kolumny)
        self.root.grid_columnconfigure(0, weight=0, minsize=320)
        self.root.grid_columnconfigure(1, weight=1)
        self.root.grid_rowconfigure(0, weight=1)

        # ==========================================
        # PASEK BOCZNY (SIDEBAR)
        # ==========================================
        sidebar = ttk.Frame(self.root, style="Sidebar.TFrame")
        sidebar.grid(row=0, column=0, sticky="nsew", padx=15, pady=15)

        # Sekcja Użytkownika
        lbl_user_title = tk.Label(
            sidebar,
            text="ZALOGOWANY:",
            font=("Arial", 8, "bold"),
            fg="#888888",
            bg="white",
        )
        lbl_user_title.pack(anchor="w", padx=20, pady=(15, 0))

        lbl_user_name = tk.Label(
            sidebar,
            text="Jan Kowalski",
            font=("Arial", 13, "bold"),
            fg="#2C3E50",
            bg="white",
        )
        lbl_user_name.pack(anchor="w", padx=20, pady=(0, 15))

        # Przyciski nawigacji zakładek
        nav_frame = tk.Frame(sidebar, bg="white")
        nav_frame.pack(fill="x", padx=20, pady=5)

        btn_harm = tk.Button(
            nav_frame,
            text="📅 Harmonogram",
            font=("Arial", 9, "bold"),
            bg="#EBF2FF",
            fg="#1A53BA",
            relief="flat",
            padx=10,
            pady=5,
        )
        btn_harm.pack(side="left", fill="x", expand=True, padx=(0, 5))

        btn_conf = tk.Button(
            nav_frame,
            text="📊 Analiza Konfliktów",
            font=("Arial", 9),
            bg="#F4F7FC",
            fg="#555555",
            relief="flat",
            padx=10,
            pady=5,
        )
        btn_conf.pack(side="left", fill="x", expand=True)

        # Filtry wyszukiwania
        lbl_filters = tk.Label(
            sidebar,
            text="Filtry",
            font=("Arial", 11, "bold"),
            fg="#2C3E50",
            bg="white",
        )
        lbl_filters.pack(anchor="w", padx=20, pady=(15, 5))

        # 1. Sala
        tk.Label(sidebar, text="Sala:", bg="white", fg="#666").pack(
            anchor="w", padx=20
        )
        self.combo_room = ttk.Combobox(sidebar, state="readonly")
        self.combo_room.pack(fill="x", padx=20, pady=(0, 10))

        # 2. Wykładowca
        tk.Label(sidebar, text="Wykładowca:", bg="white", fg="#666").pack(
            anchor="w", padx=20
        )
        self.combo_lecturer = ttk.Combobox(sidebar, state="readonly")
        self.combo_lecturer.pack(fill="x", padx=20, pady=(0, 10))

        # 3. Kierunek Studiów
        tk.Label(sidebar, text="Kierunek Studiów:", bg="white", fg="#666").pack(
            anchor="w", padx=20
        )
        self.combo_class = ttk.Combobox(sidebar, state="readonly")
        self.combo_class.pack(fill="x", padx=20, pady=(0, 15))

        # Przycisk wyszukiwania / generowania
        self.btn_submit = tk.Button(
            sidebar,
            text="🔍 Generuj wybrany plan",
            bg="#1A53BA",
            fg="white",
            font=("Arial", 10, "bold"),
            relief="flat",
            command=self.update_schedule_view,
            cursor="hand2",
            pady=6,
        )
        self.btn_submit.pack(fill="x", padx=20, pady=5)

        # Panel konfliktów i wykresów (zgodnie ze zdjęciem)
        tk.Label(
            sidebar,
            text="Nakładanie się i Konflikty",
            font=("Arial", 10, "bold"),
            bg="white",
            fg="#2C3E50",
        ).pack(anchor="w", padx=20, pady=(20, 5))

        self.canvas_chart = tk.Canvas(
            sidebar, bg="#F8FAFC", height=110, bd=0, highlightthickness=0
        )
        self.canvas_chart.pack(fill="x", padx=20, pady=5)
        self.draw_conflict_graphs()

        lbl_status = tk.Label(
            sidebar,
            text="WYKRYTO 0 MINIMALNYCH KOLIZJI",
            font=("Arial", 9, "bold"),
            fg="#27AE60",
            bg="white",
        )
        lbl_status.pack(padx=20, pady=2)

        # ==========================================
        # PANEL GŁÓWNY (WIDOK SIATKI)
        # ==========================================
        self.main_panel = tk.Frame(self.root, bg="#F4F7FC")
        self.main_panel.grid(row=0, column=1, sticky="nsew", padx=20, pady=20)

        # Górny pasek menu i akcji
        top_bar = tk.Frame(self.main_panel, bg="#F4F7FC")
        top_bar.pack(fill="x", pady=(0, 15))

        self.lbl_title = tk.Label(
            top_bar,
            text="PLAN ZAJĘĆ",
            font=("Arial", 24, "bold"),
            fg="#2C3E50",
            bg="#F4F7FC",
        )
        self.lbl_title.pack(side="left")

        # Przyciski akcji po prawej stronie górnego paska
        actions_frame = tk.Frame(top_bar, bg="#F4F7FC")
        actions_frame.pack(side="right")

        ttk.Button(actions_frame, text="Aktualny Harmonogram").pack(
            side="left", padx=2
        )
        ttk.Button(actions_frame, text="Harmonogram w formacie PDF").pack(
            side="left", padx=2
        )
        tk.Label(actions_frame, text="⚙️", font=("Arial", 16), bg="#F4F7FC").pack(
            side="left", padx=5
        )

        # Kontener główny siatki godzinowej
        self.grid_container = tk.Frame(self.main_panel, bg="#F4F7FC")
        self.grid_container.pack(fill="both", expand=True)

        self.render_grid_base()

    def render_grid_base(self):
        """Tworzy czysty szablon tabeli: kolumny dni i wiersze godzinowe"""
        # Czyszczenie starej zawartości kontenera
        for widget in self.grid_container.winfo_children():
            widget.destroy()

        # Konfiguracja szerokości kolumn (Kolumna 0 na godziny, reszta na dni)
        self.grid_container.grid_columnconfigure(0, weight=0, minsize=100)
        for idx in range(1, len(self.days) + 1):
            self.grid_container.grid_columnconfigure(idx, weight=1)

        # Nagłówki dni tygodnia
        for idx, day in enumerate(self.days):
            lbl = tk.Label(
                self.grid_container,
                text=day,
                font=("Arial", 10, "bold"),
                bg="#EBF2FF",
                fg="#1A53BA",
                pady=6,
                relief="flat",
            )
            lbl.grid(row=0, column=idx + 1, sticky="ew", padx=2, pady=2)

        # Generowanie wierszy dla przedziałów godzinowych
        for r_idx, time_slot in enumerate(self.time_slots):
            # Etykieta godziny po lewej stronie
            lbl_time = tk.Label(
                self.grid_container,
                text=time_slot,
                font=("Arial", 9, "bold"),
                fg="#555555",
                bg="#F4F7FC",
                anchor="center",
            )
            lbl_time.grid(row=r_idx + 1, column=0, sticky="nsew", pady=10)

            # Specjalna obsługa przerwy obiadowej wiersza (indeks czasowy 2)
            if time_slot == "11:30 - 13:30":
                lbl_break = tk.Label(
                    self.grid_container,
                    text="Przerwa obiadowa",
                    font=("Arial", 9, "italic"),
                    fg="#7F8C8D",
                    bg="#EAEDED",
                    justify="center",
                )
                lbl_break.grid(
                    row=r_idx + 1,
                    column=1,
                    columnspan=len(self.days),
                    sticky="ew",
                    pady=4,
                    padx=2,
                )

    def populate_filters(self):
        """Uzupełnia filtry unikalnymi danymi wyciągniętymi bezpośrednio z nowego pliku JSON"""
        rooms = sorted(list(set(item["sala"] for item in self.db_schedule)))
        lecturers = sorted(
            list(set(item["wykladowca"] for item in self.db_schedule))
        )
        classes = sorted(list(set(item["klasa"] for item in self.db_schedule)))

        self.combo_room["values"] = ["-- Wszystkie --"] + rooms
        self.combo_lecturer["values"] = ["-- Wszystkie --"] + lecturers
        self.combo_class["values"] = ["-- Wszystkie --"] + classes

        # Ustawienie domyślnego wyboru na pierwszą klasę/kierunek z brzegu
        if classes:
            self.combo_class.set(classes[0])
            self.combo_room.set("-- Wszystkie --")
            self.combo_lecturer.set("-- Wszystkie --")
            self.update_schedule_view()

    def update_schedule_view(self):
        """Filtruje zbiór danych i rysuje kafelki zajęć w odpowiednich komórkach tabeli"""
        # Resetowanie układu siatki przed nałożeniem nowych kart
        self.render_grid_base()

        f_room = self.combo_room.get()
        f_lecturer = self.combo_lecturer.get()
        f_class = self.combo_class.get()

        for lesson in self.db_schedule:
            # Dopasowanie filtrów (jeśli filtr nie jest "Wszystkie" ani pusty)
            if (
                f_room
                and f_room != "-- Wszystkie --"
                and lesson["sala"] != f_room
            ):
                continue
            if (
                f_lecturer
                and f_lecturer != "-- Wszystkie --"
                and lesson["wykladowca"] != f_lecturer
            ):
                continue
            if (
                f_class
                and f_class != "-- Wszystkie --"
                and lesson["klasa"] != f_class
            ):
                continue

            # Znajdowanie współrzędnych komórki w siatce grid
            try:
                col_idx = self.days.index(lesson["dzien"]) + 1
                row_idx = self.time_slots.index(lesson["godzina"]) + 1
            except ValueError:
                continue  # Bezpiecznik w razie niezgodności godzinowej

            # Budowa kafelka przedmiotu (Styling Flat Modern Blue)
            card = tk.Frame(
                self.grid_container,
                bg="#D1E3F8",
                bd=0,
                highlightbackground="#1A53BA",
                highlightthickness=1,
            )
            card.grid(row=row_idx, column=col_idx, sticky="nsew", padx=3, pady=3)

            # Pola tekstowe wewnątrz kafelka zajęć
            short_type = f"({lesson['typ'][0]})" if lesson["typ"] else ""
            tk.Label(
                card,
                text=f"{lesson['przedmiot']} {short_type}",
                font=("Arial", 9, "bold"),
                fg="#1A53BA",
                bg="#D1E3F8",
                anchor="w",
                justify="left",
            ).pack(fill="x", padx=6, pady=(5, 1))

            tk.Label(
                card,
                text=lesson["typ"],
                font=("Arial", 8),
                fg="#555555",
                bg="#D1E3F8",
                anchor="w",
            ).pack(fill="x", padx=6)

            tk.Label(
                card,
                text=f"{lesson['wykladowca']}, {lesson['sala']}",
                font=("Arial", 8),
                fg="#444444",
                bg="#D1E3F8",
                anchor="w",
            ).pack(fill="x", padx=6, pady=(0, 5))

    def draw_conflict_graphs(self):
        """Rysuje miniatury wykresów w panelu bocznym na wzór oryginalnej makiety"""
        # Wykres słupkowy (lewa strona canvasu)
        bars = [25, 65, 45, 20, 55]
        for idx, val in enumerate(bars):
            x0 = 15 + (idx * 16)
            y0 = 90 - val
            x1 = x0 + 10
            y1 = 95
            self.canvas_chart.create_rectangle(
                x0, y0, x1, y1, fill="CornflowerBlue", outline=""
            )

        # Wykres radarowy/pajęczynowy (prawa strona canvasu)
        center_x, center_y, radius = 210, 50, 35
        # Rysowanie tła radaru (sześciokąt)
        points = [
            (center_x, center_y - radius),
            (center_x + radius * 0.86, center_y - radius * 0.5),
            (center_x + radius * 0.86, center_y + radius * 0.5),
            (center_x, center_y + radius),
            (center_x - radius * 0.86, center_y + radius * 0.5),
            (center_x - radius * 0.86, center_y - radius * 0.5),
        ]
        self.canvas_chart.create_polygon(
            points, fill="", outline="#E2E8F0", width=1
        )
        # Przykładowy obszar zajętości na radarze
        radar_poly = [
            (center_x, center_y - radius * 0.4),
            (center_x + radius * 0.7, center_y - radius * 0.3),
            (center_x + radius * 0.5, center_y + radius * 0.4),
            (center_x, center_y + radius * 0.8),
            (center_x - radius * 0.3, center_y + radius * 0.2),
            (center_x - radius * 0.6, center_y - radius * 0.2),
        ]
        self.canvas_chart.create_polygon(
            radar_poly, fill="#1A53BA", outline="#1A53BA", stipple="gray25"
        )


if __name__ == "__main__":
    root = tk.Tk()
    app = ScheduleApp(root)
    root.mainloop()