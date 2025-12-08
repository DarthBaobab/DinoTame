import csv
import json
import os

csv_file = 'Dino2025-12-02.csv'
out_file = 'Dino2025-12-02.json'
# Wenn die CSV eine Header-Zeile hat, auf True setzen
headers = 2

def to_number(token):
    if token is None:
        return 0
    t = token.strip().replace(',', '.')
    if t == '' or t == '-':
        return 0
    try:
        if '.' in t:
            return int(max(1, round(float(t))))
        return int(t)
    except:
        return 0

if not os.path.exists(csv_file):
    raise FileNotFoundError(f"{csv_file} nicht gefunden")

rows = []
with open(csv_file, 'r', encoding='ANSI') as f:
    reader = csv.reader(f, delimiter=';')
    for r in reader:
        # leere Zeilen überspringen
        if any(cell.strip() for cell in r):
            rows.append(r)

if headers > 0 and rows:
    for _ in range(headers):
        rows.pop(0)

result = []

for row in rows:
    # Grundannahme zur Spaltenstruktur (anpassbar):
    # 0: Name
    # 1: Varianten
    # 2: Boss
    # 3: Maps (Komma-getrennt)
    # ab 4: Stat-Werte in folgender Reihenfolge:
    # Health (Base, Wild, Tame)
    # Stamina (Base, Wild, Tame)
    # Oxygen (Base, Wild, Tame)
    # Food (Base, Wild, Tame)
    # Weight (Base, Wild, Tame)
    # Damage (Base, Wild, Tame)
    # Speed (Base, Tame)
    # Torpor (Base, Wild)

    name = row[0].strip() if len(row) > 0 else ""
    varianten = row[1].strip() if len(row) > 1 else ""
    boss = row[2].strip() if len(row) > 2 else ""
    maps_str = row[3] if len(row) > 3 else ""
    maps = [m.strip() for m in maps_str.split(',') if m.strip()]

    stats_tokens = row[4:] if len(row) > 4 else []
    # ensure we have tokens to pop safely
    def pop_token():
        return stats_tokens.pop(0) if stats_tokens else ''

    stats = {}

    # Health, Stamina, Oxygen, Food, Weight, Damage -> jeweils 3 Werte
    for stat_name in ["Health", "Stamina", "Oxygen", "Food", "Weight", "Damage"]:
        base = to_number(pop_token())
        wild = to_number(pop_token())
        tame = to_number(pop_token())
        stats[stat_name] = {"Base": base, "Wild": wild, "Tame": tame}

    # Speed -> Base, Tame
    speed_base = to_number(pop_token())
    speed_tame = to_number(pop_token())
    stats["Speed"] = {"Base": speed_base, "Wild": 0, "Tame": speed_tame}

    # Torpor -> Base, Wild
    torpor_base = to_number(pop_token())
    torpor_wild = to_number(pop_token())
    stats["Torpor"] = {"Base": torpor_base, "Wild": torpor_wild, "Tame": 0}

    entry = {
        "Name": name,
        "Varianten": varianten,
        "Boss": boss,
        "Maps": maps,
        "Stats": stats
    }

    #result.append({ name: entry })
    result.append(entry)

with open(out_file, 'w', encoding='utf-8') as f:
    json.dump(result, f, ensure_ascii=False, indent=4)

print(f"Konvertierung abgeschlossen — {len(result)} Einträge geschrieben nach {out_file}")
