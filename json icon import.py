import os
import json

# Pfad zu deinem JSON-Dokument
json_pfad = "Dinos_icon.json"

# Pfad zum Icon-Ordner (wo z. B. "Achatina.png" liegt)
icon_ordner = "dino_icons"  # Passe diesen Pfad an

# JSON-Datei laden
with open(json_pfad, "r", encoding="utf-8") as f:
    dinos = json.load(f)

# Für jeden Dino prüfen, ob ein Icon existiert, und ggf. hinzufügen
for dino in dinos:
    icon_dateiname = f"{dino['name']}.png"
    icon_pfad = os.path.join(icon_ordner, icon_dateiname)
    
    if os.path.isfile(icon_pfad):
        dino["icon"] = icon_dateiname
    else:
        dino["icon"] = None
        
# Ergebnis wieder speichern
with open(json_pfad, "w", encoding="utf-8") as f:
    json.dump(dinos, f, indent=2, ensure_ascii=False)

print("Fertig! Icons wurden eingetragen, wenn vorhanden.")
