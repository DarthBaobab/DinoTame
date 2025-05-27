import os
import requests
from bs4 import BeautifulSoup
from urllib.parse import urljoin

# Zielseite mit den Kreaturen
wiki_url = "https://ark.wiki.gg/wiki/Creatures"
output_dir = "ark_dino_icons_fullsize"
os.makedirs(output_dir, exist_ok=True)

# Seite abrufen und parsen
response = requests.get(wiki_url)
soup = BeautifulSoup(response.text, "html.parser")

# Alle Bilder mit Klasse 'dinolink'
for idx, img in enumerate(soup.select("img.dinolink"), start=1):
    thumb_src = img.get("src")
    alt = img.get("alt")
    
    if alt and alt.strip():
        name = alt.strip().replace(".png", "").replace(" ", "_")
    else:
        # Kein alt-Attribut, versuche title oder nimm letzten Teil des src als Name
        title = img.get("title")
        if title and title.strip():
            name = title.strip().replace(".png", "").replace(" ", "_")
        else:
            # Fallback: Dateiname aus URL oder Index als Name
            name = os.path.basename(thumb_src).split(".")[0].removeprefix("30px-") if thumb_src else f"image_{idx}"

    # Von Thumbnail zur Original-Bild-URL
    if "/thumb/" in thumb_src:
        original_path = thumb_src.split("/thumb/")[1].split("/")
        folder = "/".join(original_path[:2])
        filename = original_path[2]
        original_url = f"https://ark.wiki.gg/images/{folder}/{filename}"
    else:
        original_url = "https:" + thumb_src

    # Bild abrufen und speichern
    try:
        img_data = requests.get(original_url).content
        filename = f"{name}.png"
        with open(os.path.join(output_dir, filename), "wb") as f:
            f.write(img_data)
        print(f"✅ {filename} gespeichert.")
    except Exception as e:
        print(f"❌ Fehler bei {name}: {e}")
