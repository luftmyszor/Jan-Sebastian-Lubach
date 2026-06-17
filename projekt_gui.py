import json
import os
import tkinter as tk
from tkinter import messagebox, ttk


# =========================================================================
# MIEJSCE NA TWOJE IMPORTY I DEKLARACJE MODELU LLM
# =========================================================================
def twoja_funkcja_wywolania_llm(pytanie_uzytkownika, surowe_dane_json):
    """W tej funkcji umieść swój działający kod LLM"""
    prompt_systemowy = (
        "Jesteś asystentem uczelnianym. Odpowiedz na pytanie na podstawie poniższego pliku JSON:\n\n"
        f"{surowe_dane_json}"
    )
    
    # Zastąp ten testowy tekst swoim kodem!
    odpowiedz_testowa = (
        f"Otrzymałem pytanie: '{pytanie_uzytkownika}'.\n"
        "Zastąp ten tekst w linii 21 swoim kodem, aby wyświetlić prawdziwą odpowiedź modelu!"
    )
    return odpowiedz_testowa


class ScheduleApp:

    def __init__(self, root):
        self.root = root
        self.root.title("Projekt informatyka - Harmonogram z własnym LLM")
        
        # Szerokość 1350, aby ładnie pomieścić stałe kolumny planu bez ścisku
        self.root.geometry("1350x750")
        self.root.configure(bg="#F4F7FC")

        # Godziny zajęć i dni tygodnia
        self.days = ["Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek"]
        self.time_slots = [
            "08:00 - 09:30",
            "09:45 - 11:15",
            "11:30 - 13:30",  # Przerwa obiadowa
            "14:00 - 15:30",
            "15:45 - 17:15",
        ]

        # Zmienna przechowująca surowy tekst JSON do wysłania do LLM
        self.raw_json_data = ""

        # --- WCZYTYWANIE PLIKU JSON Z WPISANEJ ŚCIEŻKI ---
        self.db_schedule = self.load_and_parse_json(r"C:\Users\Aneta\Desktop\projekt\dane_nowe.json")

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
        """Wczytuje plik JSON i zapisuje surowy string dla potrzeb LLM"""
        if not os.path.exists(filepath):
            messagebox.showerror("Błąd", f"Nie znaleziono pliku JSON pod ścieżką:\n{filepath}")
            return []
        try:
            with open(filepath, "r", encoding="utf-8") as file:
                self.raw_json_data = file.read()
                file.seek(0)
                data = json.load(file)

            parsed_schedule = []
            rooms_by_type = {}
            for room in data["rooms"]:
                rooms_by_type.setdefault(room["type"], []).append(room["name"])

            groups = {g["id"]: g["name"] for g in data["student_groups"]}
            group_slot_counters = {}

            for idx, course in enumerate(data["courses"]):
                group_id = course["group_id"]
                group_name = groups.get(group_id, group_id)

                if group_id not in group_slot_counters:
                    group_slot_counters[group_id] = 0

                slot_index = group_slot_counters[group_id]
                day_idx = slot_index % len(self.days)
                time_idx = (slot_index // len(self.days)) % len(self.time_slots)

                if time_idx == 2:
                    slot_index += len(self.days)
                    day_idx = slot_index % len(self.days)
                    time_idx = (slot_index // len(self.days)) % len(self.time_slots)

                group_slot_counters[group_id] = slot_index + 1
                day = self.days[day_idx]
                time_str = self.time_slots[time_idx]

                lecturer_name = "Nieprzypisany"
                for inst in data["instructors"]:
                    if course["subject_id"] in inst["subjects"]:
                        lecturer_name = inst["name"]
                        break

                room_type = course["required_room_type"]
                available_rooms = rooms_by_type.get(room_type, ["Sala Ogólna"])
                room_name = available_rooms[idx % len(available_rooms)]

                parsed_schedule.append({
                    "klasa": group_name,
                    "wykladowca": lecturer_name,
                    "przedmiot": course["name"],
                    "typ": course["type"].upper(),
                    "dzien": day,
                    "godzina": time_str,
                    "sala": room_name,
                })
            return parsed_schedule
        except Exception as e:
            messagebox.showerror("Błąd", f"Problem przy przetwarzaniu pliku JSON: {e}")
            return []

    def setup_ui(self):
        self.root.grid_columnconfigure(0, weight=0, minsize=320)
        self.root.grid_columnconfigure(1, weight=1)
        self.root.grid_rowconfigure(0, weight=1)

        # ==========================================
        # PASEK BOCZNY (SIDEBAR)
        # ==========================================
        sidebar = ttk.Frame(self.root, style="Sidebar.TFrame")
        sidebar.grid(row=0, column=0, sticky="nsew", padx=15, pady=15)

        tk.Label(sidebar, text="ZALOGOWANY:", font=("Arial", 8, "bold"), fg="#888888", bg="white").pack(anchor="w", padx=20, pady=(15, 0))
        tk.Label(sidebar, text="Jan Kowalski", font=("Arial", 13, "bold"), fg="#2C3E50", bg="white").pack(anchor="w", padx=20, pady=(0, 15))

        # Zakładki nawigacji
        nav_frame = tk.Frame(sidebar, bg="white")
        nav_frame.pack(fill="x", padx=20, pady=5)

        self.btn_harm = tk.Button(nav_frame, text="📅 Plan", font=("Arial", 9), bg="#F4F7FC", fg="#555555", relief="flat", padx=5, pady=5, command=lambda: self.switch_view("harmonogram"), cursor="hand2")
        self.btn_harm.pack(side="left", fill="x", expand=True, padx=(0, 2))

        self.btn_conf = tk.Button(nav_frame, text="📊 Kolizje", font=("Arial", 9), bg="#F4F7FC", fg="#555555", relief="flat", padx=5, pady=5, command=lambda: self.switch_view("konflikty"), cursor="hand2")
        self.btn_conf.pack(side="left", fill="x", expand=True, padx=(0, 2))

        self.btn_ai = tk.Button(nav_frame, text="🤖 Asystent LLM", font=("Arial", 9), bg="#F4F7FC", fg="#555555", relief="flat", padx=5, pady=5, command=lambda: self.switch_view("ai_chat"), cursor="hand2")
        self.btn_ai.pack(side="left", fill="x", expand=True)

        # Filtry globalne
        lbl_filters = tk.Label(sidebar, text="Filtry globalne", font=("Arial", 11, "bold"), fg="#2C3E50", bg="white")
        lbl_filters.pack(anchor="w", padx=20, pady=(25, 5))

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
        # Widok 1: Harmonogram
        self.view_schedule_frame = tk.Frame(self.root, bg="#F4F7FC")
        self.grid_container = tk.Frame(self.view_schedule_frame, bg="#F4F7FC")
        self.grid_container.pack(fill="both", expand=True)

        # Widok 2: Wykresy Kolizji
        self.view_conflicts_frame = tk.Frame(self.root, bg="#F4F7FC")
        chart_panel = tk.Frame(self.view_conflicts_frame, bg="white", relief="flat")
        chart_panel.pack(fill="both", expand=True, padx=10, pady=10)
        self.canvas_chart = tk.Canvas(chart_panel, bg="#F8FAFC", height=420, bd=0, highlightthickness=0)
        self.canvas_chart.pack(fill="x", padx=20, pady=10)

        # Widok 3: Okno Czat LLM
        self.view_ai_frame = tk.Frame(self.root, bg="#F4F7FC")
        lbl_ai_title = tk.Label(self.view_ai_frame, text="INTERFEJS ZAPYTANIA DO MODELU LLM", font=("Arial", 20, "bold"), fg="#1A53BA", bg="#F4F7FC")
        lbl_ai_title.pack(anchor="w", pady=(0, 10))

        self.chat_history = tk.Text(self.view_ai_frame, bg="white", fg="#2C3E50", font=("Arial", 10), state="disabled", wrap="word", bd=0)
        self.chat_history.pack(fill="both", expand=True, padx=5, pady=5)

        input_frame = tk.Frame(self.view_ai_frame, bg="#F4F7FC")
        input_frame.pack(fill="x", pady=5)

        self.user_input = tk.Entry(input_frame, font=("Arial", 11), bd=1, relief="solid")
        self.user_input.pack(side="left", fill="x", expand=True, ipady=6, padx=(0, 5))
        self.user_input.bind("<Return>", lambda event: self.obsluz_wysylke_do_llm())

        btn_send = tk.Button(input_frame, text="Wyślij do LLM 🚀", bg="#1A53BA", fg="white", font=("Arial", 10, "bold"), relief="flat", command=self.obsluz_wysylke_do_llm, cursor="hand2", padx=15)
        btn_send.pack(side="right")

        self.append_to_chat("System", "Okno czatu gotowe. Wpisz poniżej pytanie, a system przekaże je do Twojego kodu LLM.")

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
            self.draw_large_conflict_graphs()
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
        self.draw_large_conflict_graphs()

    def render_grid_base(self):
        """Buduje ramy kalendarza o STAŁYCH wielkościach kolumn i wierszy"""
        for widget in self.grid_container.winfo_children(): 
            widget.destroy()
            
        # Kolumna 0 (godziny) ma stałą szerokość 110 pikseli
        self.grid_container.grid_columnconfigure(0, weight=0, minsize=110)
        
        # Kolumny 1-5 (dni tygodnia) mają zablokowaną szerokość 180 pikseli
        for idx in range(1, len(self.days) + 1): 
            self.grid_container.grid_columnconfigure(idx, weight=0, minsize=180)

        # Nagłówki dni tygodnia
        for idx, day in enumerate(self.days):
            lbl = tk.Label(self.grid_container, text=day, font=("Arial", 10, "bold"), bg="#EBF2FF", fg="#1A53BA", pady=6)
            lbl.grid(row=0, column=idx + 1, sticky="ew", padx=2, pady=2)

        # Wiersze godzinowe
        for r_idx, time_slot in enumerate(self.time_slots):
            # Definiujemy stałą wysokość wiersza dla zajęć (110 pikseli)
            self.grid_container.grid_rowconfigure(r_idx + 1, weight=0, minsize=110)
            
            lbl_time = tk.Label(self.grid_container, text=time_slot, font=("Arial", 9, "bold"), fg="#555555", bg="#F4F7FC")
            lbl_time.grid(row=r_idx + 1, column=0, sticky="nsew", pady=10)
            
            if time_slot == "11:30 - 13:30":
                lbl_break = tk.Label(self.grid_container, text="Przerwa obiadowa", font=("Arial", 9, "italic"), fg="#7F8C8D", bg="#EAEDED")
                lbl_break.grid(row=r_idx + 1, column=1, columnspan=len(self.days), sticky="nsew", pady=4, padx=2)

    def populate_filters(self):
        """Uzupełnia listy rozwijane (Comboboxy) w pasku bocznym"""
        rooms = sorted(list(set(item["sala"] for item in self.db_schedule)))
        lecturers = sorted(list(set(item["wykladowca"] for item in self.db_schedule)))
        classes = sorted(list(set(item["klasa"] for item in self.db_schedule)))
        self.combo_room["values"] = ["-- Wszystkie --"] + rooms
        self.combo_lecturer["values"] = ["-- Wszystkie --"] + lecturers
        self.combo_class["values"] = ["-- Wszystkie --"] + classes
        if classes:
            self.combo_class.set(classes[0])
            self.combo_room.set("-- Wszystkie --")
            self.combo_lecturer.set("-- Wszystkie --")

    def update_schedule_view(self):
        """Generuje kafelki zajęć, wymuszając niepodatność na długość tekstu"""
        self.render_grid_base()
        f_room, f_lecturer, f_class = self.combo_room.get(), self.combo_lecturer.get(), self.combo_class.get()
        
        for lesson in self.db_schedule:
            if f_room and f_room != "-- Wszystkie --" and lesson["sala"] != f_room: continue
            if f_lecturer and f_lecturer != "-- Wszystkie --" and lesson["wykladowca"] != f_lecturer: continue
            if f_class and f_class != "-- Wszystkie --" and lesson["klasa"] != f_class: continue
            try:
                col_idx = self.days.index(lesson["dzien"]) + 1
                row_idx = self.time_slots.index(lesson["godzina"]) + 1
            except ValueError: continue
            
            # Tworzymy sztywną ramkę (Frame) o stałych wymiarach
            card = tk.Frame(
                self.grid_container, 
                bg="#D1E3F8", 
                bd=0, 
                highlightbackground="#1A53BA", 
                highlightthickness=1,
                width=176,
                height=102
            )
            card.grid_propagate(False)  # Blokada rozpychania przez napisy
            card.grid(row=row_idx, column=col_idx, sticky="nsew", padx=3, pady=3)

            short_type = f"({lesson['typ'][0]})" if lesson["typ"] else ""
            
            # Parametr wraplength automatycznie łamie za długi tekst
            tk.Label(card, text=f"{lesson['przedmiot']} {short_type}", font=("Arial", 9, "bold"), fg="#1A53BA", bg="#D1E3F8", anchor="w", wraplength=165, justify="left").pack(fill="x", padx=6, pady=(5, 1))
            tk.Label(card, text=lesson["typ"], font=("Arial", 8), fg="#555555", bg="#D1E3F8", anchor="w").pack(fill="x", padx=6)
            tk.Label(card, text=f"{lesson['wykladowca']}\nSala: {lesson['sala']}", font=("Arial", 8), fg="#444444", bg="#D1E3F8", anchor="w", wraplength=165, justify="left").pack(fill="x", padx=6, pady=(2, 5))

    def draw_large_conflict_graphs(self):
        self.canvas_chart.delete("all")
        self.canvas_chart.create_text(150, 30, text="Liczba drobnych konfliktów w dniach tygodnia", font=("Arial", 11, "bold"), fill="#2C3E50")
        bars = [40, 140, 90, 30, 110]
        for idx, val in enumerate(bars):
            x0, y0, x1, y1 = 50 + (idx * 50), 320 - val, 50 + (idx * 50) + 35, 320
            self.canvas_chart.create_rectangle(x0, y0, x1, y1, fill="CornflowerBlue", outline="")
            self.canvas_chart.create_text(x0 + 17, 335, text=self.days[idx][:3] + ".", font=("Arial", 9), fill="#555")
            self.canvas_chart.create_text(x0 + 17, y0 - 10, text=str(val // 10), font=("Arial", 9, "bold"), fill="#1A53BA")
        self.canvas_chart.create_line(35, 320, 310, 320, fill="#BDC3C7", width=2)
        
        center_x, center_y, radius = 560, 175, 110
        self.canvas_chart.create_text(center_x, 30, text="Skala czynników dopasowania preferencji (w %)", font=("Arial", 11, "bold"), fill="#2C3E50")
        for r_factor in [0.4, 0.7, 1.0]:
            curr_rad = radius * r_factor
            points = [(center_x, center_y - curr_rad), (center_x + curr_rad * 0.86, center_y - curr_rad * 0.5), (center_x + curr_rad * 0.86, center_y + curr_rad * 0.5), (center_x, center_y + curr_rad), (center_x - curr_rad * 0.86, center_y + curr_rad * 0.5), (center_x - curr_rad * 0.86, center_y - curr_rad * 0.5)]
            self.canvas_chart.create_polygon(points, fill="", outline="#E2E8F0", width=1)
        axes = [(center_x, center_y - radius), (center_x + radius * 0.86, center_y - radius * 0.5), (center_x + radius * 0.86, center_y + radius * 0.5), (center_x, center_y + radius), (center_x - radius * 0.86, center_y + radius * 0.5), (center_x - radius * 0.86, center_y - radius * 0.5)]
        for ax in axes: self.canvas_chart.create_line(center_x, center_y, ax[0], ax[1], fill="#E2E8F0")
        
        # Nowe nazwy na wykresie radarowym
        labels = [("Obłożenie sal", center_x, center_y - radius - 15), ("Profesorowie", center_x + radius + 20, center_y - radius * 0.5), ("Okienka", center_x + radius + 20, center_y + radius * 0.5), ("Grupy", center_x, center_y + radius + 15), ("Godziny", center_x - radius - 30, center_y + radius * 0.5), ("Zajętość", center_x - radius - 30, center_y - radius * 0.5)]
        for txt, lx, ly in labels: self.canvas_chart.create_text(lx, ly, text=txt, font=("Arial", 9), fill="#7F8C8D")
        radar_poly = [(center_x, center_y - radius * 0.4), (center_x + radius * 0.8, center_y - radius * 0.4), (center_x + radius * 0.6, center_y + radius * 0.5), (center_x, center_y + radius * 0.9), (center_x - radius * 0.4, center_y + radius * 0.3), (center_x - radius * 0.7, center_y - radius * 0.3)]
        self.canvas_chart.create_polygon(radar_poly, fill="#1A53BA", outline="#1A53BA", stipple="gray25")


if __name__ == "__main__":
    root = tk.Tk()
    app = ScheduleApp(root)
    root.mainloop()