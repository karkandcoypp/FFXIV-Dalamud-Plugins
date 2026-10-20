"""Build BagSense's public read-only catalog from a pinned ffxiv-datamining commit.

ToDoQty is deliberately NEVER interpreted as a hand-in item count. Exact quantities
are extracted only from an unambiguous number + the item's literal singular/plural
in the quest's own journal text. The evidence is retained for inspection.
"""
import argparse
import concurrent.futures
import csv
from datetime import datetime, timezone
import gzip
import hashlib
import io
import json
from pathlib import Path
import re
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = 'https://raw.githubusercontent.com/xivapi/ffxiv-datamining/'
WORDS = dict(zip('one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty thirty forty fifty sixty seventy eighty ninety'.split(),
                 [*range(1, 21), 30, 40, 50, 60, 70, 80, 90]))


def get(url):
    for attempt in range(4):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'BagSense-catalog/1'}), timeout=90) as response:
                return response.read()
        except (urllib.error.URLError, TimeoutError):
            if attempt == 3:
                raise
            time.sleep(2 ** attempt)


def number(text):
    if text.isdigit():
        return int(text)
    if text in ('a', 'an'):
        return 1
    value = 0
    for word in text.replace('-', ' ').split():
        value = value * 100 if word == 'hundred' else value + WORDS.get(word, 0)
    return value


def quantity(item, journal):
    forms = [re.escape(item[k].lower()) for k in ('Singular', 'Plural') if item.get(k)]
    if not forms:
        return None, 'unknown', ''
    unit = r'(?:one|two|three|four|five|six|seven|eight|nine)'
    basic = r'(?:' + '|'.join(WORDS) + r')(?:[- ]' + unit + r')?'
    n = r'(?:\d+|' + unit + r' hundred(?: (?:and )?' + basic + r')?|' + basic + r'|a|an)'
    pattern = re.compile(r'\b(' + n + r')\s+(?:(high-quality|high quality)\s+)?(?:' + '|'.join(forms) + r')\b', re.I)
    matches = [m for text in journal for m in pattern.finditer(text)]
    values = {(number(m[1].lower()), 'hq' if m[2] else 'any') for m in matches}
    # Mixed quantities/qualities are ambiguous. Never choose the smallest value.
    if len(values) != 1:
        return None, 'unknown', ''
    count, quality = next(iter(values))
    return (count, quality, matches[0][0]) if 0 < count <= 9999 else (None, 'unknown', '')


def collect(output, cache, revision=None):
    cache.mkdir(parents=True, exist_ok=True)
    if revision is None:
        revision = json.loads(get('https://api.github.com/repos/xivapi/ffxiv-datamining/commits/master'))['sha']
    if not re.fullmatch('[0-9a-f]{40}', revision):
        raise ValueError('Expected a pinned source commit')
    def download(path):
        target = cache / revision / path
        if not target.exists():
            data = get(ROOT + revision + '/' + urllib.parse.quote(path, safe='/'))
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        return target.read_text(encoding='utf-8-sig')
    def sheet(name):
        return list(csv.DictReader(io.StringIO(download('csv/en/' + name + '.csv'))))
    def val(row, key):
        return int(row.get(key, '0') or 0)
    sources = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        futures = {pool.submit(sheet, name): name for name in ('Item', 'Quest', 'Level', 'Map', 'ENpcResident', 'PlaceName', 'ItemUICategory')}
        for future in concurrent.futures.as_completed(futures):
            sources[futures[future]] = future.result()
    items = {val(i, '#'): i for i in sources['Item'] if i.get('Name')}
    maps = {val(i, '#'): i for i in sources['Map']}
    names = {val(i, '#'): i.get('Singular', '') for i in sources['ENpcResident']}
    places = {val(i, '#'): i.get('Name', '') for i in sources['PlaceName']}
    categories = {val(i, '#'): i.get('Name', '') for i in sources['ItemUICategory']}
    levels = {val(i, '#'): i for i in sources['Level']}
    def location(level):
        if not level or val(level, 'Type') != 8:
            return None
        m = maps.get(val(level, 'Map'))
        npc = val(level, 'Object')
        if not m or not names.get(npc) or val(m, 'TerritoryType') != val(level, 'Territory') or not val(m, 'SizeFactor'):
            return None
        def coord(world, offset):
            return round(41 / (val(m, 'SizeFactor') / 100) * (((float(world) + offset) * val(m, 'SizeFactor') / 100 + 1024) / 2048) + 1, 4)
        x, y = coord(level['X'], val(m, 'OffsetX')), coord(level['Z'], val(m, 'OffsetY'))
        if not (1 <= x < 100 and 1 <= y < 100):
            return None
        return dict(NpcId=npc, Npc=names[npc], Zone=places.get(val(m, 'PlaceName'), ''), Territory=val(level, 'Territory'), Map=val(m, '#'), X=x, Y=y)
    quests = []
    for q in sources['Quest']:
        if not q.get('Name') or not val(q, 'JournalGenre'):
            continue
        requested = sorted({val(q, f'QuestParams[{i}].ScriptArg') for i in range(50)
                            if re.fullmatch(r'RITEM\d+', q.get(f'QuestParams[{i}].ScriptInstruction', ''))})
        if any(i in items for i in requested):
            quests.append((q, requested))
    def quest_data(pair):
        q, requested = pair
        quest_id = val(q, '#')
        path = f"csv/en/quest/{quest_id % 65536 // 100:03}/{q['Id']}.csv"
        texts = list(csv.reader(io.StringIO(download(path))))
        journal = [r[2] for r in texts if len(r) > 2 and re.search(r'_SEQ_\d+$', r[1]) and r[2]]
        todos = {int(r[1].rsplit('_', 1)[1]): r[2] for r in texts if len(r) > 2 and re.search(r'_TODO_\d+$', r[1]) and r[2]}
        result = []
        for item_id in requested:
            if item_id not in items:
                continue
            item = items[item_id]
            count, quality, evidence = quantity(item, journal)
            # Link a hand-in only when the objective explicitly names this item and its
            # location is an NPC. Multiple candidates stay distinct instead of guessing.
            forms = [item.get(k, '').lower() for k in ('Singular', 'Plural', 'Name') if item.get(k)]
            candidates = []
            for index, text in todos.items():
                if not re.match(r'^(Deliver|Present|Give|Hand over)\b', text, re.I) or not any(
                        re.search(r'\b' + re.escape(form) + r'\b(?!s? materials?\b)', text, re.I) for form in forms):
                    continue
                locations = [location(levels.get(val(q, f'TodoParams[{index}].ToDoLocation[{i}]'))) for i in range(8)]
                locations = [l for l in locations if l]
                candidates.append((index, text, val(q, f'TodoParams[{index}].ToDoCompleteSeq'), locations))
            if len(candidates) == 1:
                index, objective, sequence, locations = candidates[0]
            else:
                index, objective, sequence, locations = None, '', None, []
            # A quantity anywhere in a multi-task journal can describe another stage.
            # Keep it unknown unless a unique hand-in objective was identified.
            if len(candidates) != 1:
                count, quality, evidence = None, 'unknown', ''
            result.append(dict(QuestId=quest_id, ItemId=item_id, QuestName=q['Name'], Repeatable=q.get('IsRepeatable') == 'True',
                               Count=count, Quality=quality, Objective=objective, TodoIndex=index, CompleteSequence=sequence,
                               Contacts=locations, Evidence=evidence, Source=path))
        return result
    needs = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        for records in pool.map(quest_data, quests):
            needs.extend(records)
    catalog_items = [dict(Id=i, Name=r['Name'], Icon=val(r, 'Icon'), Category=categories.get(val(r, 'ItemUICategory'), ''), Description=r.get('Description', '')) for i, r in sorted(items.items())]
    if len(catalog_items) < 40000 or len(needs) < 500:
        raise ValueError('Incomplete catalog; refusing publication')
    output.mkdir(parents=True, exist_ok=True)
    previous = json.loads((output / 'manifest.json').read_text(encoding='utf-8')) if (output / 'manifest.json').exists() else {}
    if previous and len(catalog_items) < previous['ItemCount'] * .99:
        raise ValueError('Unexpected item count reduction; refusing publication')
    generated = previous.get('GeneratedAt') if previous.get('SourceCommit') == revision else datetime.now(timezone.utc).isoformat()
    document = dict(Schema=1, GeneratedAt=generated, SourceCommit=revision, Items=catalog_items, Needs=sorted(needs, key=lambda r: (r['QuestId'], r['ItemId'])))
    data = gzip.compress(json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode(), mtime=0)
    digest = hashlib.sha256(data).hexdigest()
    # Content-addressed files prevent manifest/payload races through GitHub's CDN.
    asset = f'catalog-{digest[:16]}.json.gz'
    (output / asset).write_bytes(data)
    manifest = dict(Schema=1, GeneratedAt=generated, SourceCommit=revision, File=asset, Sha256=digest, Bytes=len(data),
                    ItemCount=len(catalog_items), QuestItemCount=len(needs), ExactCount=sum(n['Count'] is not None for n in needs))
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    # Keep the current and immediately previous content-addressed payload for CDN readers.
    keep = {asset, previous.get('File')}
    for obsolete in output.glob('catalog-*.json.gz'):
        if obsolete.name not in keep and obsolete.resolve().parent == output.resolve():
            obsolete.unlink()
    print(json.dumps(manifest))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--revision')
    args = parser.parse_args()
    collect(args.output, args.cache, args.revision)
