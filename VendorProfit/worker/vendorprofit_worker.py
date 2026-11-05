"""Standalone Universalis-only watcher. No game/process/Dalamud access. Python 3.10+."""
import argparse
import datetime as dt
import email.utils
import json
import os
from pathlib import Path
import time
import urllib.error
import urllib.request

VERSION = "0.5.3"


class Cooldown(Exception):
    def __init__(self,message,delay=120):
        super().__init__(message)
        self.delay=max(0,delay)


def utc():
    return dt.datetime.now(dt.timezone.utc)


def iso(value):
    return value.isoformat(timespec="seconds").replace("+00:00", "Z")


def atomic(path, document):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(document, ensure_ascii=False), encoding="utf-8")
    os.replace(temporary, path)


def positive(value):
    return type(value) is int and 0 < value <= 4_294_967_295


def get(path):
    for attempt in range(3):
        try:
            request = urllib.request.Request("https://universalis.app/api/v2/" + path,
                headers={"User-Agent": "VendorProfitWorker/0.5.3 (+https://github.com/karkandcoypp/FFXIV-Dalamud-Plugins)"})
            with urllib.request.urlopen(request, timeout=25) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            if error.code == 429:
                if attempt == 2:
                    raise Cooldown("Universalis rate limit; pass stopped") from error
                retry = error.headers.get("Retry-After", "2")
                try:
                    delay = float(retry)
                except ValueError:
                    delay = (email.utils.parsedate_to_datetime(retry) - utc()).total_seconds()
                if delay > 60:
                    raise Cooldown("Universalis requested a long cooldown",delay) from error
                time.sleep(max(0, delay))
            elif error.code >= 500 and attempt < 2:
                time.sleep(2 ** attempt)
            else:
                raise
        except (urllib.error.URLError, TimeoutError):
            if attempt == 2:
                raise
            time.sleep(2 ** attempt)


def parse_book(book, item, request, now):
    if not isinstance(book, dict) or book.get("itemID") != item["Id"] or book.get("worldID", request["World"]) != request["World"]:
        return [], True
    stamp = book.get("lastUploadTime")
    if type(stamp) is not int or stamp <= 0:
        return [], True
    try:
        updated = dt.datetime.fromtimestamp(stamp / 1000, dt.timezone.utc)
    except (ValueError, OverflowError, OSError):
        return [], True
    if not now - dt.timedelta(minutes=request["MaximumAgeMinutes"]) <= updated <= now + dt.timedelta(minutes=2):
        return [], True
    listings = book.get("listings")
    if not isinstance(listings, list):
        return [], True
    output = []
    for listing in listings:
        if not isinstance(listing, dict):
            continue
        price, quantity = listing.get("pricePerUnit"), listing.get("quantity")
        if not positive(price) or not positive(quantity) or quantity > item["Stack"] or listing.get("onMannequin") is not False:
            continue
        hq = listing.get("hq")
        if type(hq) is not bool or hq and not item["CanHq"]:
            continue
        try:
            listing_id = int(listing["listingID"])
        except (KeyError, TypeError, ValueError):
            continue
        if not 0 < listing_id <= 18_446_744_073_709_551_615:
            continue
        subtotal = price * quantity
        if listing.get("total", subtotal) != subtotal:
            continue
        tax = listing.get("tax")
        estimated = type(tax) is not int or not 0 <= tax <= 4_294_967_295
        if estimated:
            tax = subtotal // 20
        if tax > 4_294_967_295:
            continue
        cost = subtotal + tax
        npc = (item["Npc"] * 11 + 9) // 10 if hq else item["Npc"]
        revenue, profit = npc * quantity, npc * quantity - cost
        if cost > request["MaximumLot"] or profit < request["MinimumProfit"] or profit * 100 < cost * request["MinimumRoi"]:
            continue
        output.append({"Item": item, "Quote": {"Lot": {
            "Id": listing_id, "Item": item["Id"], "Retainer": str(listing.get("retainerName", "")),
            "Price": price, "Quantity": quantity, "Tax": tax, "Hq": hq, "Mannequin": False,
            "Damaged": False, "Conservative": False}, "Cost": cost, "Revenue": revenue},
            "Updated": iso(updated), "EstimatedTax": estimated, "World": request["World"], "WorldName": request.get("WorldName", "")})
    return output, len(listings) >= 100


def validate(request):
    if request.get("Schema") != 1 or not positive(request.get("World")):
        raise ValueError("No valid scanner request/world; open plugin settings to export it")
    if type(request.get("Chaos",False)) is not bool:
        raise ValueError("Invalid Chaos scope")
    for key, minimum, maximum in [("IntervalSeconds", 60, 1800), ("MaximumAgeMinutes", 5, 1440),
            ("MaximumLot", 1, 10_000_000), ("MinimumProfit", 1, 10_000_000), ("MinimumRoi", 1, 10000)]:
        if type(request.get(key)) is not int or not minimum <= request[key] <= maximum:
            raise ValueError("Invalid request setting: " + key)
    items = request.get("Items")
    if not isinstance(items, list) or not 1 <= len(items) <= 100:
        raise ValueError("Expected 1–100 candidate items")
    if len({item.get("Id") for item in items}) != len(items):
        raise ValueError("Duplicate item IDs")
    for item in items:
        if not all(positive(item.get(k)) for k in ("Id", "Npc", "Stack")) or item.get("Equipment") is not False or type(item.get("CanHq")) is not bool:
            raise ValueError("Invalid/non-consumable candidate")
    return request


def scan_world(request, fetch=get, pause=time.sleep):
    validate(request)
    items = request["Items"]
    suggestions, failed, unknown = [], 0, 0
    cooldown = False
    retry_after = 0
    for offset in range(0, len(items), 20):
        batch = items[offset:offset + 20]
        try:
            root = fetch(f'{request["World"]}/{",".join(str(item["Id"]) for item in batch)}?listings=100&entries=0')
            for item in batch:
                book = root.get("items", {}).get(str(item["Id"])) if "items" in root else root
                found, incomplete = parse_book(book, item, request, utc())
                suggestions.extend(found)
                unknown += int(incomplete)
        except Cooldown as error:
            unknown += len(items) - offset
            cooldown = True
            retry_after = error.delay
            break
        except (urllib.error.URLError, OSError, ValueError, TypeError, KeyError):
            failed += len(batch)
        pause(.3)
    suggestions.sort(key=lambda s: s["Quote"]["Revenue"] - s["Quote"]["Cost"], reverse=True)
    return {"World": request["World"], "Finished": iso(utc()), "Suggestions": suggestions,
        "Checked": len(items), "Unknown": unknown, "Failed": failed,
        "Status": "External Universalis worker; cached offers, not game polling.", "Scope": "External", "Servers": None, "Cooldown":cooldown,"RetryAfterSeconds":retry_after}


def scan(request, fetch=get, pause=time.sleep):
    validate(request)
    if not request.get("Chaos",False):
        return scan_world(request,fetch,pause)
    centers=[c for c in fetch("data-centers") if c.get("name")=="Chaos"]
    if len(centers)!=1:
        raise ValueError("Cannot resolve Chaos")
    ids=list(dict.fromkeys(centers[0]["worlds"]))
    names={w["id"]:w["name"] for w in fetch("worlds")}
    if not 1<=len(ids)<=32 or any(not positive(i) or not names.get(i) for i in ids):
        raise ValueError("Invalid Chaos worlds")
    suggestions,servers=[],[]
    cooldown=False
    retry_after=0
    for world in ids:
        if cooldown:
            servers.append(dict(World=world,Name=names[world],Checked=0,Unknown=len(request["Items"]),Failed=0))
            continue
        part=scan_world(dict(request,World=world,WorldName=names[world]),fetch,pause)
        suggestions.extend(part["Suggestions"])
        servers.append(dict(World=world,Name=names[world],Checked=part["Checked"],Unknown=part["Unknown"],Failed=part["Failed"]))
        cooldown=part["Cooldown"]
        retry_after=max(retry_after,part["RetryAfterSeconds"])
    suggestions.sort(key=lambda s:s["Quote"]["Revenue"]-s["Quote"]["Cost"],reverse=True)
    return dict(World=request["World"],Finished=iso(utc()),Suggestions=suggestions,Scope="External",Servers=servers,
        Checked=sum(w["Checked"] for w in servers),Unknown=sum(w["Unknown"] for w in servers),Failed=sum(w["Failed"] for w in servers),
        Status=f"External Universalis Chaos: {len(ids)} worlds; cached offers, not game polling.",Cooldown=cooldown,RetryAfterSeconds=retry_after)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", required=True, type=Path)
    parser.add_argument("--once", action="store_true")
    args = parser.parse_args()
    directory = args.directory.resolve()
    directory.mkdir(parents=True, exist_ok=True)
    lock = (directory / "external-worker.lock").open("a+b")
    if os.name == "nt":
        import msvcrt
        lock.seek(0); lock.write(b"1"); lock.flush(); lock.seek(0)
        try:
            msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
        except OSError:
            return 0  # One watcher per directory; never terminate another process.
    stop = directory / "external-worker.stop"
    stop.unlink(missing_ok=True)
    try:
        while not stop.exists():
            interval = 60
            try:
                request = validate(json.loads((directory / "external-request.json").read_text(encoding="utf-8-sig")))
                interval = request["IntervalSeconds"]
                result = scan(request)
                interval=max(interval,result.get("RetryAfterSeconds",0))
                atomic(directory / "external-result.json", result)
                atomic(directory / "external-status.json", {"At": iso(utc()), "Version":VERSION, "State": "Ready", "Message": f'World {request["World"]}; offers {len(result["Suggestions"])}; errors {result["Failed"]}'})
            except Exception as error:
                atomic(directory / "external-status.json", {"At": iso(utc()), "Version":VERSION, "State": "Error", "Message": str(error)[:400]})
                if args.once:
                    raise
            if args.once:
                break
            deadline = time.monotonic() + interval
            while not stop.exists() and time.monotonic() < deadline:
                time.sleep(.5)
    finally:
        atomic(directory / "external-status.json", {"At": iso(utc()), "Version":VERSION, "State": "Stopped", "Message": "Worker stopped"})
        lock.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
