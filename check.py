"""Releve l'etat des projets GTA VI -> PC et n'affiche que ce qui a change depuis le dernier passage.

Usage : python -I check.py
Etat conserve dans state.json (a cote du script). Aucune authentification : flux Atom et fichiers bruts
de GitHub, plus deux recherches publiques.
"""
import json
import os
import re
import sys
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
STATE_FILE = os.path.join(HERE, "state.json")
ATOM = "{http://www.w3.org/2005/Atom}"
MAX_COMMITS = 12

# depot -> (branche, fichier d'etat, fonction d'extraction)
REPOS = {
    "Marvisak/VIctory": "main",
    "ivall/xcl": "main",
    "brcly/TuxBox": "main",
}
SEARCHES = [
    "xbox series compatibility layer windows",
    "gta vi xbox pc",
]
# Projets deja examines : ne sont plus signales comme nouveaux.
KNOWN = {
    "marvisak/victory", "ivall/xcl", "brcly/tuxbox", "projectleonida/leonida", "r0y00/xbox2windows",
    "freminet/scarlettx", "sammwyy/xvalkyrie", "jadeowaowa/scarlett-layer", "trinitronclient/zenith-x",
    "t-crypt/bathyal", "dom-in/leonida", "etoile-bleu/leonida-reviews",
    "ahmet-dedeler/awesome-gta6-reverse-engineering",
}
MIN_STARS = 25


def fetch(url):
    req = urllib.request.Request(url, headers={"User-Agent": "recomp-watch"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return r.read().decode("utf-8", "replace")


def victory_status(text):
    m = re.search(r"^## Status.*?(?=^Tested on)", text, re.S | re.M)
    return m.group(0).strip() if m else "(section Status introuvable)"


def line_with(text, needle):
    for line in text.splitlines():
        if needle in line:
            return line.strip()
    return "(ligne introuvable : %s)" % needle


STATUS = {
    "Marvisak/VIctory": ("README.md", victory_status),
    "ivall/xcl": ("README.md", lambda t: line_with(t, "newer retail game")),
    "brcly/TuxBox": ("docs/PROGRESS.md", lambda t: line_with(t, "ProjectScarlett (GTA VI")),
}


def main():
    # La console Windows est en cp1252 : les README contiennent des emojis et des fleches.
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    try:
        with open(STATE_FILE, encoding="utf-8") as f:
            state = json.load(f)
    except (OSError, ValueError):
        state = {}
    first_run = not state
    seen = state.setdefault("last_commit", {})
    statuses = state.setdefault("status", {})
    known = set(state.setdefault("known", [])) | KNOWN

    print("Releve du", datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
          "| precedent :", state.get("checked_at", "aucun"))

    for repo, branch in REPOS.items():
        print("\n== %s" % repo)
        try:
            feed = ET.fromstring(fetch("https://github.com/%s/commits/%s.atom" % (repo, branch)))
            entries = []
            for e in feed.findall(ATOM + "entry"):
                entries.append((e.findtext(ATOM + "updated", ""),
                                (e.findtext(ATOM + "author/" + ATOM + "name") or "?")[:16],
                                " ".join((e.findtext(ATOM + "title") or "").split())[:130]))
            last = seen.get(repo, "")
            new = [x for x in entries if x[0] > last]
            if entries:
                seen[repo] = max(x[0] for x in entries)
            if first_run:
                print("commits : premier passage, dernier a %s" % seen.get(repo, "?")[5:16])
            elif not new:
                print("commits : aucun nouveau")
            else:
                more = " (ou plus : le flux est limite)" if len(new) == len(entries) else ""
                print("commits : %d nouveau(x)%s" % (len(new), more))
                for when, who, title in new[:MAX_COMMITS]:
                    print("  %s | %s | %s" % (when[5:16], who, title))
                if len(new) > MAX_COMMITS:
                    print("  ... et %d autres" % (len(new) - MAX_COMMITS))
        except Exception as exc:  # reseau, format du flux
            print("commits : ERREUR %s" % exc)

        path, extract = STATUS[repo]
        try:
            current = extract(fetch("https://raw.githubusercontent.com/%s/%s/%s" % (repo, branch, path)))
            if statuses.get(repo) == current:
                print("etat GTA VI : inchange")
            else:
                print("etat GTA VI : %s" % ("releve initial" if repo not in statuses else "CHANGE"))
                print(current)
                statuses[repo] = current
        except Exception as exc:
            print("etat GTA VI : ERREUR %s" % exc)

    print("\n== autres projets")
    found = {}
    for q in SEARCHES:
        url = ("https://api.github.com/search/repositories?q=%s&sort=updated&per_page=30"
               % urllib.parse.quote(q + " pushed:>2026-11-20 stars:>=%d" % MIN_STARS))
        try:
            for r in json.loads(fetch(url)).get("items", []):
                found[r["full_name"].lower()] = r
        except Exception as exc:
            print("recherche '%s' : ERREUR %s" % (q, exc))
    fresh = [r for name, r in found.items() if name not in known]
    if not fresh:
        print("aucun nouveau depot (>= %d etoiles)" % MIN_STARS)
    for r in sorted(fresh, key=lambda r: -r["stargazers_count"]):
        print("NOUVEAU %d etoiles | %s | cree %s | %s" % (
            r["stargazers_count"], r["full_name"], r["created_at"][:10], (r["description"] or "")[:140]))
        known.add(r["full_name"].lower())

    state["known"] = sorted(known - KNOWN)
    state["checked_at"] = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    with open(STATE_FILE, "w", encoding="utf-8") as f:
        json.dump(state, f, indent=1)


if __name__ == "__main__":
    sys.exit(main())
