import json
import glob
import os

# Liste der Map-Dateien und ihre Namen
map_files = {
    "The Island": "The Island.txt",
    "Scorched Earth": "Scorched Earth.txt",
    "The Center": "The Center.txt",
    "Aberration": "Aberration.txt",
    "Extinction": "Extinction.txt",
    "Astraeos": "Astraeos.txt"
    # Füge weitere Maps hier hinzu, falls vorhanden
}

# Map: Dino-Name -> Liste der Maps
dino_to_maps = {}

for map_name, filename in map_files.items():
    if not os.path.exists(filename):
        continue
    with open(filename, encoding="utf-8") as f:
        for line in f:
            dino = line.strip()
            if not dino or dino.startswith("//"):
                continue
            # Entferne evtl. "Aberrant " etc. Präfixe für Zuordnung, falls gewünscht
            dino_to_maps.setdefault(dino, []).append(map_name)

# Dinos.json laden
with open("Dinos.json", encoding="utf-8") as f:
    dinos = json.load(f)

# Für jeden Dino das maps-Array setzen
for dino in dinos:
    name = dino["name"]
    # Finde alle Maps, auf denen dieser Dino vorkommt
    maps = dino_to_maps.get(name, [])
    dino["maps"] = maps
    # Entferne das alte "map"-Feld, falls vorhanden
    if "map" in dino:
        del dino["map"]

# Neue Datei speichern
with open("Dinos_out.json", "w", encoding="utf-8") as f:
    json.dump(dinos, f, ensure_ascii=False, indent=2)

print("Fertig! Die neue Datei heißt Dinos_out.json")