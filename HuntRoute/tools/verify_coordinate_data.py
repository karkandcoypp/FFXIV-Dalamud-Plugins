"""Verify audited coordinates and optional upstream B-rank source without network access."""
import argparse
import hashlib
import json
from pathlib import Path
import unicodedata

ROOT = Path(__file__).resolve().parents[1]
def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))
def normalized(value):
    return unicodedata.normalize("NFC", value).casefold()
def verify(source=None):
    audit = read(ROOT / "data/coordinate-audit.json")
    daily = read(ROOT / "data/daily-locations.json")
    weekly = read(ROOT / "data/elite-spawns.json")
    if len(audit["Daily"]) != 476 or len(weekly) != 77:
        raise ValueError("Unexpected audited coverage")
    seen = set()
    for item in audit["Daily"]:
        key = str(item["Id"])
        if key in seen or daily.get(key) != item["Position"]:
            raise ValueError("Daily coordinates differ from the reviewed reference: " + key)
        seen.add(key)
        if not item["ObservedMap"].startswith("https://www.ffxivhunt.com/hunt_static_maps/"):
            raise ValueError("Missing observed map reference")
        if item["ReferenceDistanceFraction"] > .04 and item["Review"] != "circular spawn area checked visually":
            raise ValueError("Unreviewed map outlier")
    for key, item in weekly.items():
        digest = hashlib.sha256(json.dumps(item["Points"], separators=(",", ":")).encode()).hexdigest()
        if digest != audit["WeeklyPointHashes"].get(key):
            raise ValueError("Weekly points differ from reviewed target source: " + key)
    if source:
        raw = source.read_bytes()
        if hashlib.sha256(raw).hexdigest() != audit["WeeklySourceFileSHA256"]:
            raise ValueError("Upstream snapshot changed; review its differences before importing")
        reference = {(normalized(m["name"]), normalized(m["zone"])): m for m in json.loads(raw) if m["rank"] == "B"}
        for key, item in weekly.items():
            candidate = reference[(normalized(item["Name"]), normalized(item["Zone"]))]
            if item["Points"] != candidate["spawns"]:
                raise ValueError("Wrong target-specific point assignment: " + key)
    print("PASS: 476 reviewed daily anchors; 77 target-specific B-rank sets / 922 point entries")
if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--huntmaps", type=Path)
    verify(parser.parse_args().huntmaps)
