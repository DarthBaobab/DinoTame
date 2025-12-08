import os
import time
import requests
from bs4 import BeautifulSoup
from PIL import Image, ImageOps

def safe_get(url):
    while True:
        r = requests.get(url, headers=headers)
        if r.status_code == 429 or "ratelimited" in r.text:
            print("⏳ Rate limit erreicht — warte 5 Sekunden…")
            time.sleep(5)
            continue
        return r
headers = {
    "User-Agent": "DinoTameBot/1.0 (https://www.twitch.tv/darth_baobab; darth.baobab@gmail.com)"
}

# Zielseite mit den Kreaturen
wiki_url = "https://ark.wiki.gg/wiki/Creatures"
output_dir_originals = "dino_icons/originals"
output_dir_converted = "dino_icons/white"
os.makedirs(output_dir_originals, exist_ok=True)
os.makedirs(output_dir_converted, exist_ok=True)

# Seite abrufen und parsen
response = safe_get(wiki_url)
soup = BeautifulSoup(response.text, "html.parser")

# Alle Bilder mit Klasse 'dinolink'
for idx, img in enumerate(soup.select("img.dinolink"), start=1):
    thumb_src = img.get("src")
    alt = img.get("alt")
    
    if alt and alt.strip():
        name = alt.strip().replace(".png", "").replace("_", " ")
        # name = alt.strip().replace(".png", "").replace(" ", "_")
    else:
        # Kein alt-Attribut, versuche title oder nimm letzten Teil des src als Name
        title = img.get("title")
        if title and title.strip():
            name = title.strip().replace(".png", "").replace("_", " ")
            # name = title.strip().replace(".png", "").replace(" ", "_")
        else:
            # Fallback: Dateiname aus URL oder Index als Name
            name = os.path.basename(thumb_src).split(".")[0].removeprefix("30px-") if thumb_src else f"image_{idx}"

    # Von Thumbnail zur Original-Bild-URL
    if "/thumb/" in thumb_src:
        #print("DEBUG: Thumb src =", thumb_src)
        original_path = thumb_src.split("/thumb/")[1].split("/")
        #print("DEBUG: original_path =", original_path)
        #folder = "/".join(original_path[:2])
        #print("DEBUG: folder =", folder)
        #filename = original_path[2]
        filename = original_path[0]
        #print("DEBUG: filename =", filename)
        #original_url = f"https://ark.wiki.gg/images/{folder}/{filename}"
        original_url = f"https://ark.wiki.gg/images/{filename}"
    else:
        original_url = "https:" + thumb_src

    # Bild abrufen und speichern
    try:
        img_data = safe_get(original_url).content
        filename = f"{name.replace('_', ' ')}.png"
        with open(os.path.join(output_dir_originals, filename), "wb") as f:
            f.write(img_data)
        print(f"✅ {filename} gespeichert.")
    except Exception as e:
        print(f"❌ Fehler bei {name}: {e}")

    input_path = os.path.join(output_dir_originals, filename)
    output_path = os.path.join(output_dir_converted, filename)

    # Bild laden und in RGBA umwandeln
    image = Image.open(input_path).convert("RGBA")

    # RGB invertieren (schwarz → weiß), Alpha erhalten
    r, g, b, a = image.split()
    inverted = ImageOps.invert(Image.merge("RGB", (r, g, b)))
    white_image = Image.merge("RGBA", (*inverted.split(), a))

    # Nur weiße Pixel behalten, Rest transparent machen
    new_data = []
    for pixel in white_image.getdata():
        r, g, b, alpha = pixel
        if r + g + b > 700:
            new_data.append((255, 255, 255, alpha))  # Weiß behalten
        else:
            new_data.append((255, 255, 255, 0))      # Sonst transparent
    white_image.putdata(new_data)

    white_image.save(output_path)
    print(f"✔️ {filename} konvertiert.")
