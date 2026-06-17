import os
import json
import subprocess
from tkinter import filedialog, messagebox
import tkinter as tk
from tkinter import ttk
import threading
import re


class ScheduleApp:

    def __init__(self, root):
        self.root = root
        self.root.title("Projekt informatyka - Harmonogram z własnym LLM")
        
        # Szerokość 1350, aby ładnie pomieścić stałe kolumny planu bez ścisku
        self.root.geometry("1350x750")
        self.root.configure(bg="#F4F7FC")

        # Godziny zajęć i dni tygodnia
        self.days = ["Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek"]
        self.wczytaj_time_slots("time_slots.json")

        self.raw_json_data = ""
        self.db_preferences = {}
        self.db_schedule = []

        # Automatyczne załadowanie ostatnich danych (jeśli istnieją w folderze)
        self.load_and_parse_json(r"./wynik_preferencje.json")
        self.wczytaj_plik_harmonogramu(r"./generated_timetable.json", silent=True)

        # Konfiguracja nowoczesnych stylów
        self.style = ttk.Style()
        self.style.theme_use("clam")
        self.style.configure("TCombobox", padding=5, background="white", relief="flat")
        self.style.configure("Sidebar.TFrame", background="white", relief="flat")

        self.setup_ui()
        self.populate_filters()

        # Domyślny widok startowy
        self.switch_view("harmonogram")

    def load_and_parse_json(self, filepath):
        """Wczytuje JSON i wyciąga wyekstrahowane preferencje oraz oryginalny tekst"""
        if not os.path.exists(filepath):
            self.log_message(f"[BŁĄD] Nie znaleziono pliku JSON: {filepath}")
            return False
        try:
            with open(filepath, "r", encoding="utf-8") as file:
                self.raw_json_data = file.read()
                file.seek(0)
                data = json.load(file)

            self.db_preferences = {}
            for inst in data.get("instructors", []):
                name = inst["name"]
                prefs = inst.get("parsed_preferences", {})
                # Pobieramy oryginalny tekst (zabezpieczenie w razie jego braku)
                raw_text = inst.get("preferences_text", "Brak oryginalnego opisu w pliku JSON.")
                
                # Zapisujemy obie rzeczy
                self.db_preferences[name] = {
                    "parsed": prefs,
                    "text": raw_text
                }

            return True
        except Exception as e:
            self.log_message(f"[BŁĄD] Problem przy przetwarzaniu pliku JSON: {e}")
            return False

    def setup_ui(self):
        self.root.grid_columnconfigure(0, weight=0, minsize=320)
        self.root.grid_columnconfigure(1, weight=1)
        self.root.grid_rowconfigure(0, weight=1)

        # ==========================================
        # PASEK BOCZNY (SIDEBAR)
        # ==========================================
        sidebar = ttk.Frame(self.root, style="Sidebar.TFrame")
        sidebar.grid(row=0, column=0, sticky="nsew", padx=15, pady=15)

        # Zakładki nawigacji
        nav_frame = tk.Frame(sidebar, bg="white")
        nav_frame.pack(fill="x", padx=20, pady=5)

        self.btn_harm = tk.Button(nav_frame, text="Plan", font=("Arial", 9), bg="#F4F7FC", fg="#555555", relief="flat", padx=5, pady=5, command=lambda: self.switch_view("harmonogram"), cursor="hand2")
        self.btn_harm.pack(side="left", fill="x", expand=True, padx=(0, 2))

        self.btn_conf = tk.Button(nav_frame, text="Optymalizacja GA", font=("Arial", 9), bg="#F4F7FC", fg="#555555", relief="flat", padx=5, pady=5, command=lambda: self.switch_view("konflikty"), cursor="hand2")        
        self.btn_conf.pack(side="left", fill="x", expand=True, padx=(0, 2))

        self.btn_ai = tk.Button(nav_frame, text="LLM Parser", font=("Arial", 9), bg="#F4F7FC", fg="#555555", relief="flat", padx=5, pady=5, command=lambda: self.switch_view("ai_chat"), cursor="hand2")
        self.btn_ai.pack(side="left", fill="x", expand=True)

        # Filtry globalne
        lbl_filters = tk.Label(sidebar, text="Filtry globalne", font=("Arial", 11, "bold"), fg="#2C3E50", bg="white")
        lbl_filters.pack(anchor="w", padx=20, pady=(25, 5))
        
        self.view_mode = tk.StringVar(value="schedule")
        mode_frame = tk.Frame(sidebar, bg="white")
        mode_frame.pack(fill="x", padx=20, pady=(0, 15))
        
        tk.Radiobutton(mode_frame, text="Plan zajęć", variable=self.view_mode, value="schedule", bg="white", command=self.on_mode_switch).pack(side="left")
        tk.Radiobutton(mode_frame, text="Preferencje", variable=self.view_mode, value="preferences", bg="white", command=self.on_mode_switch).pack(side="left")
        
        load_frame = tk.Frame(sidebar, bg="white")
        load_frame.pack(fill="x", padx=20, pady=(0, 15))
        
        btn_load_sched = tk.Button(load_frame, text="📂 Wczytaj Plan", font=("Arial", 8), bg="#EBF2FF", fg="#1A53BA", relief="flat", command=self.wybierz_plik_harmonogramu_recznie, cursor="hand2")
        btn_load_sched.pack(side="left", fill="x", expand=True, padx=(0, 2))
        
        btn_load_prefs = tk.Button(load_frame, text="📂 Wczytaj Pref.", font=("Arial", 8), bg="#EBF2FF", fg="#1A53BA", relief="flat", command=self.wybierz_plik_preferencji_recznie, cursor="hand2")
        btn_load_prefs.pack(side="left", fill="x", expand=True, padx=(2, 0))

        tk.Label(sidebar, text="Sala:", bg="white", fg="#666").pack(anchor="w", padx=20)

        self.combo_room = ttk.Combobox(sidebar, state="readonly")
        self.combo_room.pack(fill="x", padx=20, pady=(0, 10))

        tk.Label(sidebar, text="Wykładowca:", bg="white", fg="#666").pack(anchor="w", padx=20)
        self.combo_lecturer = ttk.Combobox(sidebar, state="readonly")
        self.combo_lecturer.pack(fill="x", padx=20, pady=(0, 10))

        tk.Label(sidebar, text="Kierunek Studiów:", bg="white", fg="#666").pack(anchor="w", padx=20)
        self.combo_class = ttk.Combobox(sidebar, state="readonly")
        self.combo_class.pack(fill="x", padx=20, pady=(0, 15))

        self.btn_submit = tk.Button(sidebar, text="🔍 Zastosuj filtry", bg="#1A53BA", fg="white", font=("Arial", 10, "bold"), relief="flat", command=self.on_filter_submit, cursor="hand2", pady=6)
        self.btn_submit.pack(fill="x", padx=20, pady=5)

        # ==========================================
        # KONTENERY PANELU GŁÓWNEGO (WIDOKI)
        # ==========================================
        # Widok 1: Harmonogram / Wizualizator Preferencji
        self.view_schedule_frame = tk.Frame(self.root, bg="#F4F7FC")
        
        # Ramka na oryginalny tekst promptu ---
        self.pref_text_frame = tk.Frame(self.view_schedule_frame, bg="#EBF2FF", bd=1, relief="solid", padx=15, pady=10)
        
        tk.Label(self.pref_text_frame, text="Oryginalny wpis wykładowcy (Prompt do LLM):", font=("Arial", 10, "bold"), fg="#1A53BA", bg="#EBF2FF").pack(anchor="w")
        self.lbl_pref_text = tk.Label(self.pref_text_frame, text="", font=("Arial", 10, "italic"), fg="#444", bg="#EBF2FF", wraplength=950, justify="left")
        self.lbl_pref_text.pack(anchor="w", pady=(5,0))

        # Kontener na siatkę kalendarza
        self.grid_container = tk.Frame(self.view_schedule_frame, bg="#F4F7FC")
        self.grid_container.pack(fill="both", expand=True, pady=(10, 0))

        # ==========================================
        # Widok 2: Moduł Algorytmu Genetycznego (GA)
        # ==========================================
        self.view_conflicts_frame = tk.Frame(self.root, bg="#F4F7FC")
        
        lbl_ga_title = tk.Label(self.view_conflicts_frame, text="Solver Algorytmu Genetycznego", font=("Arial", 20, "bold"), fg="#27AE60", bg="#F4F7FC")
        lbl_ga_title.pack(anchor="w", pady=(0, 20))

        # --- Panel Sterowania GA ---
        ga_file_frame = tk.Frame(self.view_conflicts_frame, bg="white", padx=15, pady=15, relief="solid", bd=1)
        ga_file_frame.pack(fill="x", pady=(0, 15))

        self.lbl_ga_selected_file = tk.Label(ga_file_frame, text="Plik z preferencjami (JSON): Brak", font=("Arial", 11), bg="white", fg="#555")
        self.lbl_ga_selected_file.pack(side="left")

        btn_ga_select = tk.Button(ga_file_frame, text="📂 Wybierz plik", bg="#EBF2FF", fg="#1A53BA", font=("Arial", 10, "bold"), relief="flat", command=self.wybierz_plik_ga, cursor="hand2", padx=10)
        btn_ga_select.pack(side="right")

        self.btn_run_ga = tk.Button(self.view_conflicts_frame, text="🧬 Uruchom Algorytm (Solver)", bg="#27AE60", fg="white", font=("Arial", 12, "bold"), relief="flat", command=self.uruchom_algorytm_ga, cursor="hand2", pady=10)
        self.btn_run_ga.pack(fill="x", pady=(0, 15))
        self.btn_run_ga.config(state="disabled")

        # --- Panel Wykresu na Żywo ---
        plot_panel = tk.Frame(self.view_conflicts_frame, bg="white", relief="solid", bd=1)
        plot_panel.pack(fill="both", expand=True, pady=(0, 15))
        
        tk.Label(plot_panel, text="Wykres Funkcji Przystosowania (Fitness)", font=("Arial", 10, "bold"), bg="white", fg="#555").pack(anchor="nw", padx=10, pady=5)
        
        self.ga_canvas = tk.Canvas(plot_panel, bg="#FAFAFA", bd=0, highlightthickness=0)
        self.ga_canvas.pack(fill="both", expand=True, padx=10, pady=10)
        self.ga_canvas.bind("<Configure>", lambda e: self.draw_ga_plot()) # Przerysuj przy zmianie rozmiaru okna

        # --- Panel Logów GA ---
        tk.Label(self.view_conflicts_frame, text="Logi Solvera:", font=("Arial", 10, "bold"), bg="#F4F7FC", fg="#2C3E50").pack(anchor="w")
        self.ga_log_history = tk.Text(self.view_conflicts_frame, bg="white", fg="#2C3E50", font=("Consolas", 9), state="disabled", wrap="word", bd=1, height=8)
        self.ga_log_history.pack(fill="x", pady=5)

        # Zmienne wewnętrzne algorytmu
        self.wybrany_plik_ga_path = None
        self.ga_generations = []
        self.ga_fitness = []

       # ==========================================
        # Widok 3: Moduł LLM (C# Subprocess)
        # ==========================================
        self.view_ai_frame = tk.Frame(self.root, bg="#F4F7FC")
        
        lbl_ai_title = tk.Label(self.view_ai_frame, text="Zarządzanie Danymi LLM (C# Backend)", font=("Arial", 20, "bold"), fg="#1A53BA", bg="#F4F7FC")
        lbl_ai_title.pack(anchor="w", pady=(0, 20))

        # Kontener na wybór pliku
        file_frame = tk.Frame(self.view_ai_frame, bg="white", padx=15, pady=15, relief="solid", bd=1)
        file_frame.pack(fill="x", pady=(0, 20))

        self.lbl_selected_file = tk.Label(file_frame, text="Wybrany plik: Brak", font=("Arial", 11), bg="white", fg="#555")
        self.lbl_selected_file.pack(side="left")

        btn_select_file = tk.Button(file_frame, text="📂 Wybierz plik", bg="#EBF2FF", fg="#1A53BA", font=("Arial", 10, "bold"), relief="flat", command=self.wybierz_plik_llm, cursor="hand2", padx=10)
        btn_select_file.pack(side="right")

        # Przycisk uruchamiający moduł C#
        self.btn_run_csharp = tk.Button(self.view_ai_frame, text="🚀 Przetwórz przez moduł C#", bg="#1A53BA", fg="white", font=("Arial", 12, "bold"), relief="flat", command=self.uruchom_modul_csharp, cursor="hand2", pady=10)
        self.btn_run_csharp.pack(fill="x", pady=(0, 20))
        self.btn_run_csharp.config(state="disabled") # Disabled until a file is chosen

        # Przycisk do ręcznego wczytania ostatniego cache'u
        self.btn_load_cache = tk.Button(self.view_ai_frame, text="📂 Wczytaj wygenerowany plan z pliku", bg="#EBF2FF", fg="#1A53BA", font=("Arial", 10, "bold"), relief="flat", command=self.wybierz_plik_preferencji_recznie, cursor="hand2", pady=8)
        self.btn_load_cache.pack(fill="x", pady=(0, 20))
        
        # Okno logów operacji
        lbl_logs = tk.Label(self.view_ai_frame, text="Logi systemowe:", font=("Arial", 11, "bold"), bg="#F4F7FC", fg="#2C3E50")
        lbl_logs.pack(anchor="w")

        self.log_history = tk.Text(self.view_ai_frame, bg="white", fg="#2C3E50", font=("Consolas", 10), state="disabled", wrap="word", bd=0, height=15)
        self.log_history.pack(fill="both", expand=True, pady=5)
        
        # Zmienna przechowująca ścieżkę do wybranego pliku
        self.wybrany_plik_llm_path = None

    def on_mode_switch(self):
        self.populate_filters()         # 1. Najpierw odblokuj i zaktualizuj filtry
        self.switch_view("harmonogram") # 2. Dopiero potem przełącz i narysuj siatkę

    
    
    def ga_log_message(self, message):
        """Dopisuje logi do okienka w zakładce GA"""
        # Zabezpieczenie: jeśli okienko jeszcze nie istnieje, wypisz w terminalu
        if not hasattr(self, 'ga_log_history'):
            print(message)
            return
            
        self.ga_log_history.config(state="normal")
        self.ga_log_history.insert(tk.END, f"{message}\n")
        self.ga_log_history.see(tk.END)
        self.ga_log_history.config(state="disabled")

    def wybierz_plik_ga(self):
        """Wybiera plik wejściowy dla Solvera"""
        filepath = filedialog.askopenfilename(
            title="Wybierz wygenerowane preferencje",
            filetypes=[("JSON Files", "*.json"), ("All Files", "*.*")]
        )
        if filepath:
            self.wybrany_plik_ga_path = filepath
            self.lbl_ga_selected_file.config(text=f"Plik: {os.path.basename(filepath)}")
            self.btn_run_ga.config(state="normal")
            self.ga_log_message(f"[INFO] Wybrano plik do optymalizacji: {filepath}")

    def draw_ga_plot(self):
        """Rysuje wykres liniowy na żywo na Canvasie (Adaptacyjny + Kolory + Oś Zero)"""
        self.ga_canvas.delete("all")
        width = self.ga_canvas.winfo_width()
        height = self.ga_canvas.winfo_height()
        
        if width < 10 or height < 10: return # Canvas jeszcze się nie wyrenderował

        pad_x, pad_y = 50, 30
        
        # Rysowanie głównych osi X i Y
        self.ga_canvas.create_line(pad_x, pad_y, pad_x, height - pad_y, fill="#BDC3C7", width=2)
        self.ga_canvas.create_line(pad_x, height - pad_y, width - pad_x, height - pad_y, fill="#BDC3C7", width=2)
        
        if not self.ga_generations or not self.ga_fitness:
            self.ga_canvas.create_text(width//2, height//2, text="Oczekiwanie na dane z algorytmu...", font=("Arial", 12, "italic"), fill="#999")
            return

        # ==========================================
        # SKALOWANIE ADAPTACYJNE
        # ==========================================
        max_gen = max(self.ga_generations) if max(self.ga_generations) > 0 else 1
        
        min_fit = min(self.ga_fitness)
        max_fit = max(self.ga_fitness)
        span = max_fit - min_fit
        
        if span == 0:
            span = abs(max_fit) * 0.2 if max_fit != 0 else 10.0
            
        padded_min = min_fit - (span * 0.15)
        padded_max = max_fit + (span * 0.15)
        padded_span = padded_max - padded_min

        # ==========================================
        # RYSOWANIE OSI ZERO (Granica Hard/Soft)
        # ==========================================
        zero_norm_y = (0.0 - padded_min) / padded_span
        zero_y = (height - pad_y) - zero_norm_y * (height - 2 * pad_y)
        
        # Rysuj linię przerywaną tylko jeśli zero znajduje się w widocznym przedziale wykresu
        if pad_y <= zero_y <= height - pad_y:
            self.ga_canvas.create_line(pad_x, zero_y, width - pad_x, zero_y, fill="#95A5A6", width=1, dash=(4, 4))
            self.ga_canvas.create_text(pad_x - 5, zero_y, text="0", fill="#7F8C8D", font=("Arial", 8, "bold"), anchor="e")

        # Obliczanie współrzędnych wszystkich punktów
        points = []
        for g, f in zip(self.ga_generations, self.ga_fitness):
            x = pad_x + (g / max_gen) * (width - 2 * pad_x)
            norm_y = (f - padded_min) / padded_span
            y = (height - pad_y) - norm_y * (height - 2 * pad_y)
            points.append((x, y))

        # ==========================================
        # RYSOWANIE LINII SEGMENT PO SEGMENTCIE
        # ==========================================
        if len(points) > 1:
            for i in range(len(points) - 1):
                x1, y1 = points[i]
                x2, y2 = points[i+1]
                
                # Sprawdzamy fitness punktu docelowego, żeby ustalić kolor
                fit2 = self.ga_fitness[i+1]
                
                if fit2 < 0:
                    line_color = "#E74C3C" # Czerwony (Hard Constraints)
                else:
                    line_color = "#27AE60" # Zielony (Soft Constraints)
                    
                self.ga_canvas.create_line(x1, y1, x2, y2, fill=line_color, width=2)

        # ==========================================
        # WSKAŹNIK NAJNOWSZEGO PUNKTU
        # ==========================================
        last_x, last_y = points[-1]
        last_fit = self.ga_fitness[-1]
        
        # Kropka i tekst też dostają kolor na podstawie swojej wartości
        point_color = "#E74C3C" if last_fit < 0 else "#27AE60"
        
        self.ga_canvas.create_oval(last_x-4, last_y-4, last_x+4, last_y+4, fill=point_color, outline="")
        self.ga_canvas.create_text(last_x, last_y - 15, text=f"{last_fit:.1f}", fill=point_color, font=("Arial", 10, "bold"))
        self.ga_canvas.create_text(width - pad_x, height - pad_y + 15, text=f"Gen: {max_gen}", fill="#7F8C8D", font=("Arial", 9))
        
        # Wyświetlanie górnej i dolnej granicy osi Y
        self.ga_canvas.create_text(pad_x - 5, pad_y, text=f"{padded_max:.0f}", fill="#95A5A6", font=("Arial", 8), anchor="e")
        self.ga_canvas.create_text(pad_x - 5, height - pad_y, text=f"{padded_min:.0f}", fill="#95A5A6", font=("Arial", 8), anchor="e")
        
    def update_ga_plot_safe(self, gen, fitness):
        """Zapisuje nowy punkt i wymusza odrysowanie (Thread-safe)"""
        self.ga_generations.append(gen)
        self.ga_fitness.append(fitness)
        self.draw_ga_plot()

    def uruchom_algorytm_ga(self):
        """Inicjuje wątek uruchamiający plik C#"""
        if not self.wybrany_plik_ga_path: return
        
        self.btn_run_ga.config(state="disabled")
        self.ga_log_message("[START] Uruchamianie algorytmu genetycznego...")
        
        # Resetowanie wykresu
        self.ga_generations.clear()
        self.ga_fitness.clear()
        self.draw_ga_plot()

        ga_exe_path = r"Backend.Tester\bin\Debug\net10.0\Backend.Tester.exe"

        threading.Thread(
            target=self._watek_procesu_ga,
            args=(ga_exe_path, self.wybrany_plik_ga_path),
            daemon=True
        ).start()

    def _watek_procesu_ga(self, exe_path, input_file):
        """Czyta stdout Solvera linijka po linijce"""
        try:
            process = subprocess.Popen(
                [exe_path, input_file],
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf-8",
                errors="replace",
                creationflags=0x08000000 
            )

            for line in process.stdout:
                clean_line = line.strip()
                if not clean_line: continue
                
                # Przechwytywanie formatu z C#: "PROGRESS|10|845.20|123"
                if clean_line.startswith("PROGRESS|") or clean_line.startswith("PERFECT_SOLUTION|"):
                    parts = clean_line.split("|")
                    if len(parts) >= 3:
                        gen = int(parts[1])
                        fit = float(parts[2].replace(',', '.')) # Zabezpieczenie przed polskim przecinkiem
                        self.root.after(0, self.update_ga_plot_safe, gen, fit)
                
                # Zakończenie pracy
                elif clean_line.startswith("DONE|"):
                    parts = clean_line.split("|")
                    if len(parts) >= 2:
                        out_file = parts[1]
                        self.root.after(0, self.wczytaj_plik_harmonogramu, out_file)
                else:
                    self.root.after(0, self.ga_log_message, clean_line)

            process.wait()
        except FileNotFoundError:
            self.root.after(0, self.ga_log_message, f"[BŁĄD] Nie znaleziono: {exe_path}\n")
        except Exception as e:
            self.root.after(0, self.ga_log_message, f"[BŁĄD SYSTEMU] {str(e)}\n")
        finally:
            self.root.after(0, lambda: self.btn_run_ga.config(state="normal"))
            
    def populate_filters(self):
        """Aktualizuje listy w zależności od widoku i odblokowuje szare filtry"""
        mode = self.view_mode.get()
        
        # WYMUSZAMY stan normalny na czas modyfikacji (inaczej Tkinter ignoruje .set())
        self.combo_room.config(state="normal")
        self.combo_class.config(state="normal")
        self.combo_lecturer.config(state="normal") 
        
        if mode == "schedule":
            # Zabezpieczenie przed złym formatem pliku
            if not isinstance(self.db_schedule, list):
                self.db_schedule = []
                
            rooms = sorted(list(set(item.get("sala", "") for item in self.db_schedule if isinstance(item, dict))))
            lecturers = sorted(list(set(item.get("wykladowca", "") for item in self.db_schedule if isinstance(item, dict))))
            classes = sorted(list(set(item.get("klasa", "") for item in self.db_schedule if isinstance(item, dict))))
            
            self.combo_room["values"] = ["-- Wszystkie --"] + rooms
            self.combo_lecturer["values"] = ["-- Wszystkie --"] + lecturers
            self.combo_class["values"] = ["-- Wszystkie --"] + classes
            
            # Bezpieczny reset wartości
            if self.combo_room.get() not in self.combo_room["values"]:
                self.combo_room.set("-- Wszystkie --")
            if self.combo_lecturer.get() not in self.combo_lecturer["values"]:
                self.combo_lecturer.set("-- Wszystkie --")
            if self.combo_class.get() not in self.combo_class["values"]:
                self.combo_class.set("-- Wszystkie --")
                
            # Po ustawieniu wartości, zmieniamy na 'readonly' (można klikać, nie można pisać)
            self.combo_room.config(state="readonly")
            self.combo_class.config(state="readonly")
            self.combo_lecturer.config(state="readonly")
            
        else: # Widok preferencji (Heatmapa)
            lecturers = sorted(list(self.db_preferences.keys()))
            self.combo_lecturer["values"] = ["-- Wybierz Wykładowcę --"] + lecturers
            
            if lecturers and self.combo_lecturer.get() not in lecturers:
                self.combo_lecturer.set(lecturers[0])
            elif not lecturers:
                self.combo_lecturer.set("-- Brak danych --")
                
            self.combo_room.set("-- Niedostępne w tym widoku --")
            self.combo_class.set("-- Niedostępne w tym widoku --")
            
            # W tym widoku całkowicie blokujemy (szary kolor) salę i kierunek
            self.combo_room.config(state="disabled")
            self.combo_class.config(state="disabled")
            self.combo_lecturer.config(state="readonly")
            
        # ======================================================================
        # ZMIANA: Podpinamy WSZYSTKIE trzy filtry do natychmiastowego odświeżania
        # ======================================================================
        self.combo_lecturer.bind("<<ComboboxSelected>>", lambda e: self.update_schedule_view())
        self.combo_room.bind("<<ComboboxSelected>>", lambda e: self.update_schedule_view())
        self.combo_class.bind("<<ComboboxSelected>>", lambda e: self.update_schedule_view())

    def wybierz_plik_harmonogramu_recznie(self):
        filepath = filedialog.askopenfilename(title="Wybierz wygenerowany plan (JSON)", filetypes=[("JSON Files", "*.json")])
        if filepath: self.wczytaj_plik_harmonogramu(filepath)

    def wybierz_plik_preferencji_recznie(self):
        filepath = filedialog.askopenfilename(title="Wybierz wygenerowane preferencje z LLM (JSON)", filetypes=[("JSON Files", "*.json")])
        if filepath: self.wczytaj_plik_preferencji(filepath)

        
    def log_message(self, message):
        """Pomocnicza funkcja do dopisywania logów w interfejsie"""
        # Zabezpieczenie: jeśli okienko jeszcze nie istnieje, wypisz w terminalu
        if not hasattr(self, 'log_history'):
            print(message)
            return
            
        self.log_history.config(state="normal")
        self.log_history.insert(tk.END, f"{message}\n")
        self.log_history.config(state="disabled")
        self.log_history.see(tk.END)

    

    def wybierz_plik_llm(self):
        filepath = filedialog.askopenfilename(
            title="Wybierz plik wejściowy dla LLM",
            filetypes=[("JSON Files", "*.json"), ("Text Files", "*.txt"), ("All Files", "*.*")]
        )
        if filepath:
            self.wybrany_plik_llm_path = filepath
            self.lbl_selected_file.config(text=f"Wybrany plik: {os.path.basename(filepath)}")
            self.btn_run_csharp.config(state="normal")
            self.log_message(f"[INFO] Wybrano plik: {filepath}")

    def uruchom_modul_csharp(self):
        if not self.wybrany_plik_llm_path:
            return

        self.log_message("[START] Uruchamianie modułu C# w tle...")
        self.btn_run_csharp.config(state="disabled")

        csharp_exe_path = r"Backend.LlmParser\bin\Debug\net10.0\Backend.LlmParser.exe" 
        
        cache_dir = "llm_cache"
        os.makedirs(cache_dir, exist_ok=True)
        output_file = os.path.abspath(os.path.join(cache_dir, "wynik_preferencje.json"))

        # Uruchamiamy proces w osobnym wątku, aby nie zamrażać interfejsu (GUI)
        threading.Thread(
            target=self._watek_procesu_csharp, 
            args=(csharp_exe_path, self.wybrany_plik_llm_path, output_file), 
            daemon=True
        ).start()

    def _watek_procesu_csharp(self, exe_path, input_file, output_file):
        """Ten kod wykonuje się w tle, pozwalając na czytanie logów na żywo"""
        try:
            process = subprocess.Popen(
                [exe_path, input_file, output_file],
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf-8",    # <--- WYMUSZA CZYTANIE W UTF-8
                errors="replace",    # <--- Zastępuje nieznane znaki znakiem zapytania zamiast crashować apkę
                creationflags=0x08000000 
            )

            # Czytamy wyjście na żywo
            for line in process.stdout:
                clean_line = line.strip()
                if clean_line:
                    self.root.after(0, self.log_message, clean_line)

            process.wait()

            if process.returncode == 0:
                self.root.after(0, self.log_message, f"\n[ZAPISANO] Zakończono! Wynik w: {output_file}\n")
                # Po sukcesie automatycznie załaduj plan
                self.root.after(0, self.wczytaj_plik_preferencji, output_file)
            else:
                self.root.after(0, self.log_message, f"\n[BŁĄD] Proces zakończył się niepowodzeniem (kod {process.returncode})\n")

        except FileNotFoundError:
            self.root.after(0, self.log_message, f"[BŁĄD PLIKU] Nie znaleziono pliku C#: {exe_path}\n")
        except Exception as e:
            self.root.after(0, self.log_message, f"[BŁĄD SYSTEMU] {str(e)}\n")
        finally:
            # Na koniec odblokowujemy przycisk (znowu przez root.after)
            self.root.after(0, lambda: self.btn_run_csharp.config(state="normal"))


            
            
    def switch_view(self, view_name):
        self.view_schedule_frame.grid_forget()
        self.view_conflicts_frame.grid_forget()
        self.view_ai_frame.grid_forget()

        for btn in [self.btn_harm, self.btn_conf, self.btn_ai]:
            btn.config(bg="#F4F7FC", fg="#555555", font=("Arial", 9))

        if view_name == "harmonogram":
            self.view_schedule_frame.grid(row=0, column=1, sticky="nsew", padx=20, pady=20)
            self.btn_harm.config(bg="#EBF2FF", fg="#1A53BA", font=("Arial", 9, "bold"))
            self.update_schedule_view()
            
        elif view_name == "konflikty":
            self.view_conflicts_frame.grid(row=0, column=1, sticky="nsew", padx=20, pady=20)
            self.btn_conf.config(bg="#EBF2FF", fg="#1A53BA", font=("Arial", 9, "bold"))
            # ZMIANA: Rysujemy pusty (lub aktualny) wykres GA zamiast starych kolizji
            self.draw_ga_plot() 
            
        elif view_name == "ai_chat":
            self.view_ai_frame.grid(row=0, column=1, sticky="nsew", padx=20, pady=20)
            self.btn_ai.config(bg="#EBF2FF", fg="#1A53BA", font=("Arial", 9, "bold"))

    def obsluz_wysylke_do_llm(self):
        query = self.user_input.get().strip()
        if not query: return
        self.append_to_chat("Użytkownik", query)
        self.user_input.delete(0, tk.END)
        try:
            odpowiedz_modelu = twoja_funkcja_wywolania_llm(query, self.raw_json_data)
            self.append_to_chat("Model LLM", odpowiedz_modelu)
        except Exception as e:
            self.append_to_chat("Błąd Systemu", f"Nie udało się przetworzyć LLM: {e}")

    def append_to_chat(self, sender, text):
        self.chat_history.config(state="normal")
        self.chat_history.insert(tk.END, f"[{sender}]: {text}\n\n")
        self.chat_history.config(state="disabled")
        self.chat_history.see(tk.END)

    def on_filter_submit(self):
        self.update_schedule_view()

    def render_grid_base(self):
        for widget in self.grid_container.winfo_children(): 
            widget.destroy()
            
        self.grid_container.grid_columnconfigure(0, weight=0, minsize=110)
        for idx in range(1, len(self.days) + 1): 
            self.grid_container.grid_columnconfigure(idx, weight=0, minsize=180)

        # Nagłówki dni
        for idx, day in enumerate(self.days):
            lbl = tk.Label(self.grid_container, text=day, font=("Arial", 10, "bold"), bg="#EBF2FF", fg="#1A53BA", pady=6)
            lbl.grid(row=0, column=idx + 1, sticky="ew", padx=2, pady=2)

        # Dynamiczne wiersze godzinowe z pliku
        for r_idx, time_slot in enumerate(self.time_slots):
            self.grid_container.grid_rowconfigure(r_idx + 1, weight=0, minsize=90) # Mniejsze kafelki dla 7 bloków
            lbl_time = tk.Label(self.grid_container, text=time_slot, font=("Arial", 9, "bold"), fg="#555555", bg="#F4F7FC")
            lbl_time.grid(row=r_idx + 1, column=0, sticky="nsew", pady=10)
            

    def update_schedule_view(self):
        """Kieruje rysowaniem w zależności od aktywnego przycisku (Radiobutton)"""
        if self.view_mode.get() == "schedule":
            self.draw_actual_schedule()
        else:
            self.draw_preferences_heatmap()

    def draw_actual_schedule(self):
        """Rysuje faktyczny wygenerowany plan zajęć na podstawie C# GA Solvera"""
        self.render_grid_base()
        self.pref_text_frame.pack_forget() # Ukrywamy pole tekstowe promptu
        
        # Jeśli plik był pusty lub źle wczytany, przerywamy rysowanie
        if not isinstance(self.db_schedule, list):
            return
            
        f_room, f_lecturer, f_class = self.combo_room.get(), self.combo_lecturer.get(), self.combo_class.get()
        
        for lesson in self.db_schedule:
            # Zabezpieczenie przed uszkodzonymi elementami listy
            if not isinstance(lesson, dict): 
                continue
                
            # Filtrowanie
            if f_room and f_room not in ["-- Wszystkie --", "-- Niedostępne w tym widoku --"] and lesson.get("sala") != f_room: continue
            if f_lecturer and f_lecturer not in ["-- Wszystkie --", "-- Wybierz Wykładowcę --"] and lesson.get("wykladowca") != f_lecturer: continue
            if f_class and f_class not in ["-- Wszystkie --", "-- Niedostępne w tym widoku --"] and lesson.get("klasa") != f_class: continue
            
            try:
                col_idx = self.days.index(lesson.get("dzien", "")) + 1
                row_idx = self.time_slots.index(lesson.get("godzina", "")) + 1
            except ValueError: 
                continue
            
            # Rysowanie kafelka lekcji
            card = tk.Frame(self.grid_container, bg="#D1E3F8", bd=0, highlightbackground="#1A53BA", highlightthickness=1)
            card.grid(row=row_idx, column=col_idx, sticky="nsew", padx=3, pady=3)
            
            short_type = f"({lesson['typ'][0]})" if lesson.get("typ") else ""
            tk.Label(card, text=f"{lesson.get('przedmiot', '')} {short_type}", font=("Arial", 9, "bold"), fg="#1A53BA", bg="#D1E3F8", anchor="w", wraplength=160, justify="left").pack(fill="x", padx=4, pady=(2, 0))
            tk.Label(card, text=f"{lesson.get('wykladowca', '')}", font=("Arial", 8), fg="#555555", bg="#D1E3F8", anchor="w").pack(fill="x", padx=4)
            tk.Label(card, text=f"Sala: {lesson.get('sala', '')} | Gr: {lesson.get('klasa', '')}", font=("Arial", 8), fg="#444444", bg="#D1E3F8", anchor="w").pack(fill="x", padx=4, pady=(0, 2))
                
   
    def wczytaj_time_slots(self, filepath="time_slots.json"):
        """Wczytuje definicje godzin z pliku JSON"""
        try:
            with open(filepath, "r", encoding="utf-8") as f:
                data = json.load(f)
            self.time_slots_data = data["slots"] # Pełne dane (startHour, endHour)
            self.time_slots = [f"{s['start']} - {s['end']}" for s in self.time_slots_data]
        except Exception as e:
            self.log_message(f"[BŁĄD] Nie można załadować time_slots.json: {e}")
            self.time_slots = []
            self.time_slots_data = []
            
            
    def wybierz_plik_harmonogramu_recznie(self):
        filepath = filedialog.askopenfilename(title="Wybierz wygenerowany plan (JSON)", filetypes=[("JSON Files", "*.json")])
        if filepath: self.wczytaj_plik_harmonogramu(filepath)

    def wybierz_plik_preferencji_recznie(self):
        filepath = filedialog.askopenfilename(title="Wybierz wygenerowane preferencje z LLM (JSON)", filetypes=[("JSON Files", "*.json")])
        if filepath: self.wczytaj_plik_preferencji(filepath)

    def wczytaj_plik_preferencji(self, filepath):
        if self.load_and_parse_json(filepath):
            self.log_message(f"[Wczytano] Preferencje z: {filepath}")
            self.view_mode.set("preferences") 
            self.on_mode_switch()

    def wczytaj_plik_harmonogramu(self, filepath, silent=False):
        if not os.path.exists(filepath):
            return
        try:
            with open(filepath, "r", encoding="utf-8") as f:
                self.db_schedule = json.load(f)
            
            # =========================================================================
            # ZMIANA: Tłumaczenie 'Instructor X' lub 'I01' na prawdziwe imiona
            # =========================================================================
            real_names = list(self.db_preferences.keys())
            if real_names:
                for lesson in self.db_schedule:
                    wyk = str(lesson.get("wykladowca", ""))
                    
                    # Szukamy cyfr w nazwie (np. "Instructor 1", "I04") 
                    # Zabezpieczamy się przed edycją kogoś, kto już ma prawdziwe imię (dr, prof)
                    match = re.search(r'\d+', wyk)
                    if match and not any(title in wyk.lower() for title in ["prof", "dr", "mgr"]):
                        idx = int(match.group())
                        
                        # Zamiana numeru na indeks listy (I01 lub Instructor 1 to indeks 0)
                        list_idx = (idx - 1) if idx > 0 else 0
                        
                        if 0 <= list_idx < len(real_names):
                            lesson["wykladowca"] = real_names[list_idx]
            # =========================================================================
            
            # Zabezpieczenie na wypadek, gdyby to odpalało się zanim zbuduje się UI
            if hasattr(self, 'ga_log_history'): 
                self.ga_log_message(f"[Wczytano] Harmonogram: {os.path.basename(filepath)}")
                self.view_mode.set("schedule")
                self.on_mode_switch()
                
        except Exception as e:
            if not silent: 
                messagebox.showerror("Błąd", f"Nie można wczytać harmonogramu:\n{e}")
            
    def draw_preferences_heatmap(self):
        """Rysuje mapę ciepła preferencji i wyświetla oryginalny tekst z JSON"""
        self.render_grid_base()
        
        selected_lecturer = self.combo_lecturer.get()
        if selected_lecturer not in self.db_preferences:
            self.pref_text_frame.pack_forget() 
            return 
            
        data_for_lecturer = self.db_preferences[selected_lecturer]
        prefs = data_for_lecturer["parsed"]
        raw_text = data_for_lecturer["text"]
        
        self.lbl_pref_text.config(text=f'"{raw_text}"')
        self.pref_text_frame.pack(fill="x", pady=(0, 10), before=self.grid_container)

        day_map_en_to_pl = {"Mon": "Poniedziałek", "Tue": "Wtorek", "Wed": "Środa", "Thu": "Czwartek", "Fri": "Piątek"}
        day_map_pl_to_en = {v: k for k, v in day_map_en_to_pl.items()}

        for r_idx, slot_data in enumerate(self.time_slots_data):
            for c_idx, day_pl in enumerate(self.days):
                day_en = day_map_pl_to_en[day_pl]
                
                slot_start = slot_data["startHour"]
                slot_end = slot_data["endHour"]
                
                # Sprawdź, czy slot pokrywa się z ZAKAZANYMI (Czerwony)
                is_forbidden = False
                for forbidden in prefs.get("forbidden_slots", []):
                    if forbidden["day"] == day_en:
                        if slot_start < forbidden["to"] and forbidden["from"] < slot_end:
                            is_forbidden = True
                            break
                
                # Sprawdź, czy slot w pełni mieści się w PREFEROWANYCH (Zielony)
                is_preferred = False
                if day_en in prefs.get("preferred_days", []):
                    pref_start = prefs.get("preferred_hours_start", 0)
                    pref_end = prefs.get("preferred_hours_end", 24)
                    if slot_start >= pref_start and slot_end <= pref_end:
                        is_preferred = True

                # Przypisz style
                if is_forbidden:
                    bg_color, border_color, text_label, fg_color = "#FADBD8", "#E74C3C", "Niedostępny\n(Zakaz)", "#C0392B"
                elif is_preferred:
                    bg_color, border_color, text_label, fg_color = "#D4EFDF", "#27AE60", "Preferowany", "#1E8449"
                else:
                    bg_color, border_color, text_label, fg_color = "#FCF3CF", "#F1C40F", "W razie potrzeby", "#B7950B"

                # Kafelki
                card = tk.Frame(self.grid_container, bg=bg_color, bd=0, highlightbackground=border_color, highlightthickness=2)
                card.grid(row=r_idx + 1, column=c_idx + 1, sticky="nsew", padx=4, pady=4)
                
                tk.Label(card, text=text_label, font=("Arial", 10, "bold"), fg=fg_color, bg=bg_color).pack(expand=True)

if __name__ == "__main__":
    
    root = tk.Tk()
    app = ScheduleApp(root)
    root.mainloop()