import requests
from bs4 import BeautifulSoup
import os
import re
from urllib.parse import urljoin

BASE_URL_ASA = "https://wikily.gg/ark-survival-ascended/maps/"
BASE_URL_ASE = "https://ark-unity.com/ark-survival-ascended/maps/"
OUTPUT_DIR = "wikily_maps"

os.makedirs(OUTPUT_DIR, exist_ok=True)

def get_maps():
    """Liest alle Maps von der Wikily-Übersicht ein."""
    resp = requests.get(BASE_URL_ASA)
    soup = BeautifulSoup(resp.text, "html.parser")

    map_links = []

    for a in soup.find_all("a", href=True):
        href = a["href"]
        #if "/ark-survival-ascended/maps/" in a["href"] and a["href"].count("/") > 4:
        #    full = "https://wikily.gg" + a["href"]
        #    map_links.append(full)
        if "/ark-survival-ascended/maps/" in href and href.count("/") > 4:
            # absolute URL bauen (funktioniert auch für externe Weiterleitungen)
            full_url = urljoin(BASE_URL_ASA, href)
            map_links.append(full_url)
            print(f"Gefundene Map-URL: {full_url}")

    resp = requests.get(BASE_URL_ASE)
    soup = BeautifulSoup(resp.text, "html.parser")

    for a in soup.find_all("a", href=True):
        href = a["href"]
        #if "/ark-survival-ascended/maps/" in a["href"] and a["href"].count("/") > 4:
        #    full = "https://ark-unity.com" + a["href"]
        #    map_links.append(full)
        if href.startswith("/ark-survival-ascended/maps/") and href != "/ark-survival-ascended/maps/":
            # absolute URL bauen (funktioniert auch für externe Weiterleitungen)
            full_url = urljoin(BASE_URL_ASE, href)
            map_links.append(full_url)
            print(f"Gefundene Map-URL: {full_url}")

    # Duplikate entfernen
    return sorted(set(map_links))


def scrape_map2(url):
    if "wikily.gg" in url:
        """Liest alle Creature-Namen von einer Map-Seite aus."""
        resp = requests.get(url)
        soup = BeautifulSoup(resp.text, "html.parser")

        creatures = []

        for div in soup.find_all("div", class_=lambda c: c and "cursor-pointer" in c):
            text = div.get_text(strip=True)
            if text and text.lower() != "none":
                creatures.append(text)

        return creatures
    elif "ark-unity.com" in url:
        """Liest alle Creature-Namen von einer Map-Seite aus."""
        resp = requests.get(url)
        soup = BeautifulSoup(resp.text, "html.parser")

        creatures = []

        select = soup.find("select", id="spawn-map-select")
        if not select:
            print("Kein Dropdown mit Creatures gefunden!")
            exit()

        # Alle option-Tags außer der ersten
        creatures = [opt["value"] for opt in select.find_all("option")[1:]]

        return creatures

def scrape_map(url):
    print(f"[INFO] Scrape Map: {url}")

    try:
        resp = requests.get(url, timeout=20)  # Requests folgt Redirects automatisch
        resp.raise_for_status()
        final_url = resp.url
        soup = BeautifulSoup(resp.text, "html.parser")

        creatures = []

        # --- Wikily.gg Dropdown ---
        if "wikily.gg" in final_url:
            for div in soup.find_all("div", class_=lambda c: c and "cursor-pointer" in c):
                text = div.get_text(strip=True)
                if text and text.lower() != "none" and text.lower() != "see dino spawns":
                    creatures.append(text)
            return creatures

        # --- Ark-Unity Seite (falls Dropdown anders) ---
        elif "ark-unity.com" in final_url:
            # Beispiel: Unity-Seite mit div-Klassen
            select = soup.find("select", id="spawn-map-select")
            if not select:
                print("Kein Dropdown mit Creatures gefunden!")
                exit()

            # Alle option-Tags außer der ersten
            creatures = [opt["value"] for opt in select.find_all("option")[1:]]

            return creatures
        else:
            print(f"[WARN] Unbekannte Seite: {final_url}")
            return []
    except Exception as e:
        print(f"[ERROR] Fehler bei {url}: {e}")
        return []


def save_to_txt(map_name, creatures):
    filename = os.path.join(OUTPUT_DIR, f"{map_name}.txt")
    with open(filename, "w", encoding="utf-8") as f:
        for creature in creatures:
            f.write(creature + "\n")
    print(f"✓ {map_name}.txt gespeichert ({len(creatures)} Kreaturen)")


# ------------ MAIN SCRIPT ---------------

all_maps = get_maps()

for map_url in all_maps:
    # Map-Name extrahieren
    if "wikily.gg" in map_url:
        map_name = map_url.rstrip("/").split("/")[-1] + "_ASA"
    elif "ark-unity.com" in map_url:
        map_name = map_url.rstrip("/").split("/")[-1] + "_ASE"

    try:
        creatures = scrape_map(map_url)
        save_to_txt(map_name, creatures)
    except Exception as e:
        print(f"Fehler bei {map_name}: {e}")
