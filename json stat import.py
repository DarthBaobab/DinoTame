import json
import requests
from bs4 import BeautifulSoup
from tqdm import tqdm
import time

def get_dino_stats(dino_name):
    # Formatieren für URL
    base_url = "https://ark.wiki.gg/wiki/"
    page_name = dino_name.replace(" ", "_").replace("'", "%27")
    url = base_url + page_name

    try:
        response = requests.get(url, timeout=10)
        response.raise_for_status()
    except:
        return None  # Seite nicht erreichbar

    soup = BeautifulSoup(response.text, "html.parser")
    tables = soup.find_all("table", class_="wikitable")

    health = damage = None
    for table in tables:
        if 'Base Stats and Growth' in table.get("data-description", ""):
            rows = table.find_all("tr")
            for row in rows:
                cols = row.find_all("td")
                if len(cols) >= 2:
                    attr = cols[0].get_text(strip=True)
                    value = cols[1].get_text(strip=True).replace(",", "")
                    value = value.split("/")[0].strip()   # Nur den ersten Wert nehmen, falls mehrere vorhanden sind
                    if attr == "Health":
                        try:
                            health = int(float(value))
                        except:
                            pass
                    elif attr == "Melee Damage":
                        try:
                            damage = int(float(value))
                        except:
                            pass
            #print(f"Gefunden: {dino_name} - Gesundheit: {health}, Schaden: {damage}")
            break  # Wir haben die passende Tabelle gefunden
    return {"health": health, "damage": damage}

# Pfad zur JSON-Datei
input_file = "Dinos.json"
output_file = "Dinos_filled.json"

with open(input_file, "r", encoding="utf-8") as f:
    dinos = json.load(f)

for dino in tqdm(dinos, desc="Verarbeite Dinos"):
    name = dino["name"]
    if dino["health"] == 0 or dino["damage"] == 0:
        stats = get_dino_stats(name)
        print(f"stats für {name}: {stats}")
        if not stats:
            #print(f"Keine Daten gefunden für: {name}")
            continue
        if stats and stats["health"] is not None and stats["damage"] is not None:
            dino["health"] = stats["health"]
            dino["damage"] = stats["damage"]
        else:
            print(f"Keine Werte für: {name}")
        time.sleep(1.5)  # Verzögerung, um nicht blockiert zu werden

with open(output_file, "w", encoding="utf-8") as f:
    json.dump(dinos, f, indent=2, ensure_ascii=False)

print("✅ Fertig! Gefüllte Datei gespeichert als:", output_file)
