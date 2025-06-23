import os
from PIL import Image, ImageOps

# ==== EINSTELLUNGEN ====
input_folder = "dino_icons"     # Ordner mit schwarzen PNGs
output_folder = "dino_icons_white"   # Zielordner für weiße PNGs

# Zielordner erstellen, falls nicht vorhanden
os.makedirs(output_folder, exist_ok=True)

# Alle PNGs im Eingabeordner verarbeiten
for filename in os.listdir(input_folder):
    if not filename.lower().endswith(".png"):
        continue

    input_path = os.path.join(input_folder, filename)
    output_path = os.path.join(output_folder, filename)

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
