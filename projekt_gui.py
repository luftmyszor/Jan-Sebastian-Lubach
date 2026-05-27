import json
import os
import tkinter as tk
from tkinter import messagebox, ttk
import subprocess
import threading
import matplotlib
import matplotlib.pyplot as plt
matplotlib.use("TkAgg") # Tell Matplotlib to render inside Tkinter
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure

class ScheduleApp:

    def __init__(self, root):
        self.root = root
        self.root.title("Projekt informatyka - Harmonogram Uczelniany")
        self.root.geometry("1300x750")
        self.root.configure(bg="#F4F7FC")

        # Godziny zajęć odwzorowane z widoku UI
        self.days = ["Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek"]
        # Load Time Slots
        with open("time_slots.json", "r", encoding="utf-8") as f:
            slot_data = json.load(f)
            self.time_slots = [f"{s['start']} - {s['end']}" for s in slot_data["slots"]]

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

    def start_solver(self):
        """Disables the button and starts the C# GA in a background thread."""
        self.btn_submit.config(state="disabled", text="⏳ Algorytm pracuje...")
        self.lbl_status.config(text="INICJALIZACJA...", fg="#F39C12")
        
        # Initialize lists
        self.generation_data = []
        self.fitness_data = []
        
        # Clear the live graph
        self.line.set_data([], [])
        self.ax.relim()
        self.ax.autoscale_view()
        self.canvas.draw()
        
        # Start background thread
        threading.Thread(target=self.run_solver_thread, daemon=True).start()

    def run_solver_thread(self):
        """Runs the .NET process and reads its stdout line by line."""
        # Replace this path with the path to your .csproj or compiled .exe
        project_path = os.path.join(os.path.dirname(__file__), "Backend.Tester", "Backend.Tester.csproj")
        
        try:
            # We use 'dotnet run' for development. For production, point directly to the compiled .exe
            process = subprocess.Popen(
                ["dotnet", "run", "--project", project_path],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                encoding='utf-8'
            )

            for line in process.stdout:
                line = line.strip()
                if line.startswith("PROGRESS|"):
                    parts = line.split("|")
                    
                    # --- NEW: Parse as integers/floats and save them ---
                    gen = int(parts[1])
                    # Parse using invariant culture (replace comma with dot if necessary)
                    fitness = float(parts[2].replace(',', '.')) 
                    
                    self.generation_data.append(gen)
                    self.fitness_data.append(fitness)
                    
                    # Safely update GUI from the background thread
                    self.root.after(0, self.update_progress_ui, gen, fitness)
                elif line.startswith("DONE|"):
                    json_path = line.split("|")[1]
                    # Trigger the loading of the new timetable
                    self.root.after(0, self.load_generated_timetable, json_path)

            process.wait()
            
        except Exception as e:
            self.root.after(0, lambda: messagebox.showerror("Błąd", f"Błąd uruchamiania backendu:\n{e}"))
            self.root.after(0, lambda: self.btn_submit.config(state="normal", text="🚀 Uruchom Algorytm Genetyczny"))

    def update_progress_ui(self, gen, fitness):
        """Updates the status label and the live graph during the GA run."""
        self.lbl_status.config(
            text=f"Generacja: {gen} | Fitness: {fitness:.2f}", 
            fg="#1A53BA"
        )
        
        # Update the line data
        self.line.set_data(self.generation_data, self.fitness_data)
        
        # Tell the axes to recalculate their limits based on new data
        self.ax.relim()
        self.ax.autoscale_view()
        
        # Redraw the canvas inside Tkinter
        self.canvas.draw()

    def load_generated_timetable(self, json_path):
        """Loads the JSON created by C# and updates the grid."""
        try:
            with open(json_path, "r", encoding="utf-8") as f:
                self.db_schedule = json.load(f)
            
            self.populate_filters()
            self.update_schedule_view()
            
            self.lbl_status.config(text="✅ ZAKOŃCZONO. HARMONOGRAM ZAŁADOWANY", fg="#27AE60")
            
            
        except Exception as e:
            messagebox.showerror("Błąd", f"Nie udało się wczytać wyników: {e}")
        finally:
            self.btn_submit.config(state="normal", text="🚀 Uruchom Algorytm Genetyczny")

    def show_fitness_graph(self):
        """Displays a Matplotlib graph of the fitness over generations."""
        if not self.generation_data or not self.fitness_data:
            return
            
        plt.figure(figsize=(8, 5))
        plt.plot(self.generation_data, self.fitness_data, marker='o', markersize=4, linestyle='-', color='#1A53BA')
        
        plt.title('Postęp Algorytmu Genetycznego', fontsize=14)
        plt.xlabel('Generacja', fontsize=12)
        plt.ylabel('Najlepszy Fitness (Wartość kary - im bliżej 0, tym lepiej)', fontsize=12)
        
        plt.grid(True, linestyle='--', alpha=0.7)
        plt.tight_layout()
        
        # This opens a new window with the interactive graph
        plt.show()

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
        
        # --- DODAJ TE 3 LINIJKI TUTAJ ---
        # To sprawi, że wybranie opcji z listy od razu odświeży siatkę kalendarza
        self.combo_room.bind("<<ComboboxSelected>>", lambda e: self.update_schedule_view())
        self.combo_lecturer.bind("<<ComboboxSelected>>", lambda e: self.update_schedule_view())
        self.combo_class.bind("<<ComboboxSelected>>", lambda e: self.update_schedule_view())
        # --------------------------------

        # 1. FIRST: CREATE THE GRAPH
        self.graph_frame = tk.Frame(sidebar, bg="white", pady=10)
        self.graph_frame.pack(fill="x", padx=10, pady=(10, 0))

        # Make sure ALL of these have "self." in front of them!
        self.fig = Figure(figsize=(4, 3), dpi=100)
        self.ax = self.fig.add_subplot(111)   # <--- THIS LINE IS CRITICAL
        self.ax.set_title("Live Fitness", fontsize=10)
        self.ax.set_xlabel("Generacja", fontsize=8)
        self.ax.set_ylabel("Fitness", fontsize=8)
        self.ax.tick_params(axis='both', which='major', labelsize=8)
        self.ax.grid(True, linestyle='--', alpha=0.5)

        self.line, = self.ax.plot([], [], color="#1A53BA", linewidth=2)

        self.canvas = FigureCanvasTkAgg(self.fig, master=self.graph_frame)
        self.canvas.get_tk_widget().pack(fill="both", expand=True)
        
        
        # Przycisk wyszukiwania / generowania
        self.btn_submit = tk.Button(
            sidebar,
            text="🚀 Uruchom Algorytm Genetyczny",
            bg="#1A53BA",
            fg="white",
            font=("Arial", 10, "bold"),
            relief="flat",
            command=self.start_solver,
            cursor="hand2",
            pady=6,
        )
        self.btn_submit.pack(fill="x", padx=20, pady=5)


        self.lbl_status = tk.Label( # <--- Added self.
            sidebar,
            text="GOTOWY DO URUCHOMIENIA",
            font=("Arial", 9, "bold"),
            fg="#27AE60",
            bg="white",
        )
        self.lbl_status.pack(padx=20, pady=2)
        
        

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

    def update_schedule_view(self, event=None):
        """Filtruje zbiór danych i rysuje kafelki zajęć w odpowiednich komórkach tabeli"""
        # Resetowanie układu siatki przed nałożeniem nowych kart
        self.render_grid_base()

        f_room = self.combo_room.get()
        f_lecturer = self.combo_lecturer.get()
        f_class = self.combo_class.get()

        for lesson in getattr(self, "db_schedule", []):
            # Dopasowanie filtrów (jeśli filtr nie jest "Wszystkie" ani pusty)
            if (
                f_room
                and f_room != "-- Wszystkie --"
                and lesson.get("sala") != f_room
            ):
                continue
            if (
                f_lecturer
                and f_lecturer != "-- Wszystkie --"
                and lesson.get("wykladowca") != f_lecturer
            ):
                continue
            if (
                f_class
                and f_class != "-- Wszystkie --"
                and lesson.get("klasa") != f_class
            ):
                continue

            # Znajdowanie współrzędnych komórki w siatce grid
            try:
                col_idx = self.days.index(lesson.get("dzien", "")) + 1
                row_idx = self.time_slots.index(lesson.get("godzina", "")) + 1
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
            typ = lesson.get('typ', '')
            short_type = f"({typ[0]})" if typ else ""
            
            tk.Label(
                card,
                text=f"{lesson.get('przedmiot', '')} {short_type}",
                font=("Arial", 9, "bold"),
                fg="#1A53BA",
                bg="#D1E3F8",
                anchor="w",
                justify="left",
            ).pack(fill="x", padx=6, pady=(5, 1))

            tk.Label(
                card,
                text=typ,
                font=("Arial", 8),
                fg="#555555",
                bg="#D1E3F8",
                anchor="w",
            ).pack(fill="x", padx=6)

            tk.Label(
                card,
                text=f"{lesson.get('wykladowca', '')}, {lesson.get('sala', '')}",
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
    def sort_treeview_column(self, col, reverse):
        """Sorts the Treeview contents when a column header is clicked."""
        # Grab all the data in the current column
        items = [(self.tree.set(k, col), k) for k in self.tree.get_children('')]
        
        # Sort the items
        try:
            # Try to sort numerically if possible
            items.sort(key=lambda t: float(t[0]), reverse=reverse)
        except ValueError:
            # Fallback to standard alphabetical string sort
            items.sort(reverse=reverse)
            
        # Rearrange the items in the Treeview
        for index, (val, k) in enumerate(items):
            self.tree.move(k, '', index)
            
        # Re-bind the heading so the NEXT click sorts in the opposite direction
        self.tree.heading(col, command=lambda _col=col: self.sort_treeview_column(_col, not reverse))

if __name__ == "__main__":
    root = tk.Tk()
    app = ScheduleApp(root)
    root.mainloop()