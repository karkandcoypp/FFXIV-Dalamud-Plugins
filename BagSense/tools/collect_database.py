"""Build BagSense's public read-only catalog from a pinned ffxiv-datamining commit.

ToDoQty is deliberately NEVER interpreted as a hand-in item count. Exact quantities
are extracted from explicit literal requests, structured supply records, or a
reviewed named cost/count family in the quest's own script metadata. The evidence is retained for inspection.
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
COLLECTOR_VERSION = 10
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


def requested_instruction(instruction, item_id, ordinary, event):
    if re.fullmatch(r'ITEM\d+', instruction):
        return item_id in event
    return bool(re.fullmatch(r'(?:RITEM\d+|RITEM_ADD_\d+|QUEST_ITEM_?\d*|COST_ITEM(?:_.*)?|QST_ITEM\d+|QST_PRODUCT_ITEM|QST_GATHERING_ITEM_(?:MIN|HRV|FSH))', instruction)) and (item_id in ordinary or item_id in event)


def number(text):
    if text.isdigit():
        return int(text)
    if text in ('a', 'an'):
        return 1
    value = 0
    for word in text.replace('-', ' ').split():
        value = value * 100 if word == 'hundred' else value + WORDS.get(word, 0)
    return value


def quantity(item, journal, requests_only=False):
    forms = [re.escape(item[k].lower()).replace(r'\ ', r'\s+(?:high[- ]quality\s+)?') for k in ('Singular', 'Plural', 'Name') if item.get(k)]
    if not forms:
        return None, 'unknown', ''
    unit = r'(?:one|two|three|four|five|six|seven|eight|nine)'
    basic = r'(?:' + '|'.join(WORDS) + r')(?:[- ]' + unit + r')?'
    n = r'(?:\d+|' + unit + r' hundred(?: (?:and )?' + basic + r')?|' + basic + r'|a|an)'
    pattern = re.compile(r'\b(' + n + r')\s+(?:(high-quality|high quality)\s+)?(?:' + '|'.join(forms) + r')\b', re.I)
    matches = [m for text in journal for m in pattern.finditer(text)
               if not requests_only or (re.search(r'\b(?:need|require|bring|deliver|gather|collect|craft|fashion|prepare|make|obtain|provide|request|present|synthesize|forge|want)\b', text[max(0,m.start()-120):m.start()], re.I)
                   and not re.search(r"(?:not|never|don't|no longer)\s+(?:need|require|want)", text[max(0,m.start()-45):m.start()], re.I))]
    values = {(number(m[1].lower()), 'hq' if re.search(r'high[- ]quality', m[0], re.I) else 'any') for m in matches}
    # Mixed quantities/qualities are ambiguous. Never choose the smallest value.
    if len(values) != 1:
        return None, 'unknown', ''
    count, quality = next(iter(values))
    return (count, quality, matches[0][0]) if 0 < count <= 9999 else (None, 'unknown', '')


def retained_payload(previous, asset, output):
    candidate = previous.get('File') if previous.get('File') != asset else previous.get('PreviousFile')
    if candidate is None:
        legacy = [p.name for p in output.glob('catalog-*.json.gz') if p.name != asset]
        if len(legacy) == 1:
            candidate = legacy[0]
    return candidate if isinstance(candidate, str) and re.fullmatch(r'catalog-[a-f0-9]{16}\.json\.gz', candidate) and (output/candidate).is_file() else None


def collect(output, cache, revision=None, teamcraft_revision=None):
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
        futures = {pool.submit(sheet, name): name for name in ('Item', 'EventItem', 'Quest', 'QuestClassJobSupply', 'ClassJobCategory', 'ClassJob', 'Level', 'Map', 'ENpcResident', 'PlaceName', 'ItemUICategory')}
        for future in concurrent.futures.as_completed(futures):
            sources[futures[future]] = future.result()
    items = {val(i, '#'): i for i in sources['Item'] if i.get('Name')}
    event_items = {val(i, '#'): i for i in sources['EventItem'] if i.get('Name')}
    all_items = items | event_items
    events_by_quest = {}
    for item_id, event in event_items.items():
        if val(event, 'Quest'):
            events_by_quest.setdefault(val(event, 'Quest'), set()).add(item_id)
    supplies = {}
    for supply in sources['QuestClassJobSupply']:
        if val(supply, 'Item') in items and val(supply, 'AmountRequired') > 0:
            supplies.setdefault(int(supply['#'].split('.')[0]), []).append(supply)
    jobs = {val(i, '#'): i.get('Name', '') for i in sources['ClassJobCategory']}
    job_members = {val(i, '#'): [val(j, '#') for j in sources['ClassJob'] if j.get('Abbreviation') and i.get(j['Abbreviation']) == 'True'] for i in sources['ClassJobCategory']}
    maps = {val(i, '#'): i for i in sources['Map']}
    names = {val(i, '#'): i.get('Singular', '') for i in sources['ENpcResident']}
    places = {val(i, '#'): i.get('Name', '') for i in sources['PlaceName']}
    categories = {val(i, '#'): i.get('Name', '') for i in sources['ItemUICategory']}
    levels = {val(i, '#'): i for i in sources['Level']}
    npc_levels = {}
    for level in levels.values():
        if val(level, 'Type') == 8:
            npc_levels.setdefault(val(level, 'Object'), []).append(level)
    def location(level):
        if not level or val(level, 'Type') != 8:
            return None
        m = maps.get(val(level, 'Map'))
        npc = val(level, 'Object')
        if not m or not names.get(npc) or not val(m, '#') or not val(level, 'Territory') or not val(m, 'TerritoryType') or not val(m, 'SizeFactor'):
            return None
        def coord(world, offset):
            return round(41 / (val(m, 'SizeFactor') / 100) * (((float(world) + offset) * val(m, 'SizeFactor') / 100 + 1024) / 2048) + 1, 4)
        x, y = coord(level['X'], val(m, 'OffsetX')), coord(level['Z'], val(m, 'OffsetY'))
        if not (1 <= x < 100 and 1 <= y < 100):
            return None
        return dict(NpcId=npc, Npc=names[npc], Zone=places.get(val(m, 'PlaceName'), ''), Territory=val(m, 'TerritoryType'), Map=val(m, '#'), X=x, Y=y)
    # Literal aliases let explicit hand-in objectives discover items absent from RITEM.
    aliases = {}
    for item_id, item in all_items.items():
        for key in ('Name', 'Singular', 'Plural'):
            form = ' '.join(re.findall(r"[\w'-]+", item.get(key, '').lower()))
            if len(form) >= 6:
                aliases.setdefault(form, set()).add(item_id)
    def objective_items(text):
        if not re.match(r'^(Deliver|Present|Give|Hand over|Submit)\b', text, re.I):
            return set()
        words = re.findall(r"[\w'-]+", text.lower())
        matches = []
        for start in range(1, len(words)):
            for end in range(min(start + 14, len(words)), start, -1):
                form = ' '.join(words[start:end])
                ids = aliases.get(form, set())
                if len(ids) == 1 and (end == len(words) or words[end] not in ('materials', 'material')):
                    matches.append((start, end, next(iter(ids))))
        # A longer specific item phrase wins over a substring such as a base material.
        return {i for start, end, i in matches if not any(a <= start and b >= end and (b-a) > (end-start) for a,b,_ in matches)}
    quests = []
    for q in sources['Quest']:
        if not q.get('Name') or not val(q, 'JournalGenre'):
            continue
        requested = {val(q, f'QuestParams[{i}].ScriptArg') for i in range(50)
                     if requested_instruction(q.get(f'QuestParams[{i}].ScriptInstruction', ''), val(q, f'QuestParams[{i}].ScriptArg'), items, event_items)}
        requested.update(val(s, 'Item') for s in supplies.get(val(q, 'QuestClassJobSupply'), []))
        requested.update(events_by_quest.get(val(q, '#'), set()))
        quests.append((q, requested))
    def quest_data(pair):
        q, requested = pair
        quest_id = val(q, '#')
        path = f"csv/en/quest/{quest_id % 65536 // 100:03}/{q['Id']}.csv"
        texts = list(csv.reader(io.StringIO(download(path))))
        journal = [r[2] for r in texts if len(r) > 2 and re.search(r'_SEQ_\d+$', r[1]) and r[2]]
        todos = {int(r[1].rsplit('_', 1)[1]): r[2] for r in texts if len(r) > 2 and re.search(r'_TODO_\d+$', r[1]) and r[2]}
        discovered = {index: objective_items(text) for index, text in todos.items()}
        requested = requested | set().union(*discovered.values()) if discovered else requested
        steps = []
        for index, text in todos.items():
            locations = [location(levels.get(val(q, f'TodoParams[{index}].ToDoLocation[{i}]'))) for i in range(8)]
            locations = [l for l in locations if l]
            if not locations:
                actors = {val(q, f'QuestParams[{i}].ScriptArg') for i in range(50)
                          if re.fullmatch(r'ACTOR\d+', q.get(f'QuestParams[{i}].ScriptInstruction', ''))}
                actors = [npc for npc in actors if names.get(npc) and re.search(r'\b' + re.escape(names[npc]) + r'\b', text, re.I)]
                if len(actors) == 1:
                    candidates = [location(l) for l in npc_levels.get(actors[0], [])]
                    candidates = [l for l in candidates if l]
                    if len({(l['Map'], l['X'], l['Y']) for l in candidates}) == 1:
                        locations = candidates[:1]
            steps.append(dict(Index=index, CompleteSequence=val(q, f'TodoParams[{index}].ToDoCompleteSeq'), Text=text,
                              Contacts=locations))
        result = []
        for item_id in requested:
            if item_id not in all_items:
                continue
            item = all_items[item_id]
            count, quality, evidence = quantity(item, journal)
            # Link a hand-in only when the objective explicitly names this item and its
            # location is an NPC. Multiple candidates stay distinct instead of guessing.
            forms = [item.get(k, '').lower() for k in ('Singular', 'Plural', 'Name') if item.get(k)]
            candidates = []
            for index, text in todos.items():
                if not re.match(r'^(Deliver|Present|Give|Hand over|Submit)\b', text, re.I) or item_id not in discovered[index]:
                    continue
                locations = next(s['Contacts'] for s in steps if s['Index'] == index)
                candidates.append((index, text, val(q, f'TodoParams[{index}].ToDoCompleteSeq'), locations))
            if len(candidates) == 1:
                index, objective, sequence, locations = candidates[0]
            else:
                index, objective, sequence, locations = None, '', None, []
            # A quantity anywhere in a multi-task journal can describe another stage.
            # Keep it unknown unless a unique hand-in objective was identified.
            if len(candidates) != 1:
                count, quality, evidence = None, 'unknown', ''
            elif count is None:
                count, quality, evidence = quantity(item, [r[2] for r in texts if len(r) > 2 and r[2]], requests_only=True)
                if evidence:
                    evidence = 'Dialogue request: ' + evidence
            use = dict(QuestId=quest_id, ItemId=item_id, QuestName=q['Name'], Repeatable=q.get('IsRepeatable') == 'True',
                               Count=count, Quality=quality, Objective=objective, TodoIndex=index, CompleteSequence=sequence,
                               Contacts=locations, Evidence=evidence, Source=path, EventItem=item_id in event_items, Variants=[])
            # Reviewed family: each COST_ITEM_ATMA_n is consumed in COST_USE_ATMA quantity.
            params = {q.get(f'QuestParams[{i}].ScriptInstruction', ''): val(q, f'QuestParams[{i}].ScriptArg') for i in range(50)}
            if any(re.fullmatch(r'COST_ITEM_ATMA_\d+', k) and v == item_id for k,v in params.items()) and 0 < params.get('COST_USE_ATMA', 0) < 100:
                matches = [step for step in steps if re.match(r'^Deliver\b', step['Text'], re.I) and 'demiatma' in step['Text'].lower()]
                if len(matches) == 1:
                    step = matches[0]
                    use.update(Count=params['COST_USE_ATMA'], Quality='any', Objective=step['Text'], TodoIndex=step['Index'],
                               CompleteSequence=step['CompleteSequence'], Contacts=step['Contacts'], Evidence='COST_ITEM_ATMA_n + COST_USE_ATMA; explicit demiatma delivery objective')
            structured = [s for s in supplies.get(val(q, 'QuestClassJobSupply'), []) if val(s, 'Item') == item_id]
            if structured:
                use['Count'] = max(val(s, 'AmountRequired') for s in structured)
                use['Quality'] = 'hq' if any(s['ItemHQ'] == 'True' for s in structured) else 'any'
                use['Evidence'] = '; '.join(f"QuestClassJobSupply[{s['#']}]: AmountRequired={s['AmountRequired']}, ItemHQ={s['ItemHQ']}" for s in structured)
                use['Source'] = 'csv/en/QuestClassJobSupply.csv'
                use['Variants'] = [dict(Job=jobs.get(val(s, 'ClassJobCategory'), ''), Count=val(s, 'AmountRequired'), Hq=s['ItemHQ'] == 'True',
                                        Jobs=job_members.get(val(s, 'ClassJobCategory'), [])) for s in structured]
                direct = []
                for s in structured:
                    npc = val(s, 'ENpcResident')
                    # Prefer the quest's actual objective placements. Global fallback is
                    # used only when that NPC has one unambiguous location.
                    candidates_for_npc = [l for step in steps for l in step['Contacts'] if l['NpcId'] == npc]
                    if not candidates_for_npc:
                        global_locations = [location(l) for l in npc_levels.get(npc, [])]
                        global_locations = [l for l in global_locations if l]
                        if len({(l['Map'], l['X'], l['Y']) for l in global_locations}) == 1:
                            candidates_for_npc = global_locations[:1]
                    direct.extend(candidates_for_npc)
                if direct:
                    use['Contacts'] = list({(l['NpcId'],l['Map'],l['X'],l['Y']): l for l in direct}.values())
            result.append(use)
        quest = dict(QuestId=quest_id, Name=q['Name'], Repeatable=q.get('IsRepeatable') == 'True', Steps=steps,
                     ItemIds=sorted(requested), Source=path)
        return result, quest
    needs = []
    quest_catalog = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        for records, quest in pool.map(quest_data, quests):
            needs.extend(records)
            quest_catalog.append(quest)
    # Additional source facts from a pinned Teamcraft revision; no player data is sent.
    tc_revision = teamcraft_revision or json.loads(get('https://api.github.com/repos/ffxiv-teamcraft/ffxiv-teamcraft/commits/staging'))['sha']
    if not re.fullmatch('[0-9a-f]{40}', tc_revision): raise ValueError('Expected pinned Teamcraft revision')
    tc_root = 'https://raw.githubusercontent.com/ffxiv-teamcraft/ffxiv-teamcraft/' + tc_revision + '/libs/data/src/lib/json/'
    def tc_sheet(name):
        target = cache / 'teamcraft' / tc_revision / (name + '.json')
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(get(tc_root + name + '.json'))
        return json.loads(target.read_text(encoding='utf-8'))
    tc_names = ['drop-sources','instance-sources','fate-sources','loot-sources','mobs','instances','fates','used-in-quests']
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        tc = dict(zip(tc_names, pool.map(tc_sheet, tc_names)))
    links = []
    def source_link(item_id, kind, ref_id, name, detail=''):
        if item_id in items and name:
            links.append(dict(ItemId=item_id, Kind=kind, Name=name, Id=ref_id, Detail=detail))
    for table, kind, names_table in [('drop-sources','Drop','mobs'),('instance-sources','Duty','instances'),('fate-sources','Fate','fates')]:
        for item_id, references in tc[table].items():
            for ref_id in references:
                entry = tc[names_table].get(str(ref_id), {})
                name = entry.get('name', entry).get('en', '')
                source_link(int(item_id),kind,ref_id,name)
    for item_id, containers in tc['loot-sources'].items():
        for container in containers:
            if container in items:
                source_link(int(item_id),'Container',container,items[container]['Name'])
                source_link(container,'Contains',int(item_id),items.get(int(item_id),{}).get('Name',''))
    quest_rows = {val(q,'#'):q for q,_ in quests}
    existing = {(n['QuestId'],n['ItemId']) for n in needs}
    for item_id, references in tc['used-in-quests'].items():
        item_id=int(item_id)
        if item_id not in items: continue
        for quest_id in references:
            if quest_id not in quest_rows or (quest_id,item_id) in existing: continue
            q=quest_rows[quest_id]
            needs.append(dict(QuestId=quest_id,ItemId=item_id,QuestName=q['Name'],Repeatable=q.get('IsRepeatable')=='True',
                              Count=None,Quality='unknown',Objective='',TodoIndex=None,CompleteSequence=None,Contacts=[],
                              Evidence='Teamcraft input association; quantity and hand-in objective unresolved',Source=tc_revision,EventItem=False,Variants=[]))
            existing.add((quest_id,item_id))
    for item_id in range(47744,47750):
        if item_id in items and items[item_id]['Name'].endswith('Demiatma'):
            source_link(item_id,'Fate',0,"Occult Crescent: South Horn",'FATE / Critical Encounter; Arcane Artistry progression')
    source_link(47744,'Fate',0,'Urqopacha','FATE; Arcane Artistry progression')
    research_bytes = (Path(__file__).parents[1] / 'data' / 'researched-uses.json').read_bytes()
    research_hash = hashlib.sha256(research_bytes).hexdigest()
    researched = json.loads(research_bytes.decode('utf-8'))
    research = [r for r in researched['Research'] if r['ItemId'] in items and r.get('ItemName') == items[r['ItemId']]['Name']]
    need_lookup = {(n['QuestId'],n['ItemId']):n for n in needs}
    for fact in researched.get('QuestRequirements', []):
        pair = (fact['QuestId'],fact['ItemId'])
        if pair[0] not in quest_rows or pair[1] not in items: continue
        if fact.get('ItemName') != items[pair[1]]['Name'] or fact.get('QuestName') != quest_rows[pair[0]]['Name']: continue
        old = need_lookup.get(pair)
        if old is not None:
            if old['Count'] is None and not old.get('Variants'):
                old.update(Count=fact['Count'], Quality=fact['Quality'], Evidence='Wiki required-items record; hand-in stage is only known when separately identified. ' + old['Evidence'])
            continue
        q=quest_rows[pair[0]]
        use=dict(QuestId=pair[0],ItemId=pair[1],QuestName=q['Name'],Repeatable=q.get('IsRepeatable')=='True',Count=fact['Count'],Quality=fact['Quality'],
                 Objective='',TodoIndex=None,CompleteSequence=None,Contacts=[],Evidence='Wiki required-items record; may describe an unlock/exchange prerequisite, exact consumption objective unresolved',
                 Source='https://ffxiv.consolegameswiki.com/wiki/' + urllib.parse.quote(fact['WikiTitle'].replace(' ','_')),EventItem=False,Variants=[])
        needs.append(use);need_lookup[pair]=use
    for quest in quest_catalog:
        quest['ItemIds'] = sorted(set(quest['ItemIds']) | {n['ItemId'] for n in needs if n['QuestId'] == quest['QuestId']})
    catalog_items = [dict(Id=i, Name=r['Name'], Icon=val(r, 'Icon'), Category=categories.get(val(r, 'ItemUICategory'), ''), Description=r.get('Description', '')) for i, r in sorted(items.items())]
    catalog_items.extend(dict(Id=i, Name=r['Name'], Icon=val(r, 'Icon'), Category='Key item', Description='', EventItem=True) for i,r in sorted(event_items.items()))
    if len(catalog_items) < 40000 or len(needs) < 500 or len(quest_catalog) < 5000:
        raise ValueError('Incomplete catalog; refusing publication')
    if len(links) < 10000 or any(l['ItemId'] not in items or not l['Name'] for l in links):
        raise ValueError('Incomplete acquisition graph; refusing publication')
    output.mkdir(parents=True, exist_ok=True)
    previous = json.loads((output / 'manifest.json').read_text(encoding='utf-8')) if (output / 'manifest.json').exists() else {}
    if previous and len(catalog_items) < previous['ItemCount'] * .99:
        raise ValueError('Unexpected item count reduction; refusing publication')
    if previous.get('SourceCount') and len(links) < previous['SourceCount'] * .99:
        raise ValueError('Unexpected acquisition graph shrinkage; refusing publication')
    if previous.get('QuestCount') and len(quest_catalog) < previous['QuestCount'] * .99:
        raise ValueError('Unexpected quest count reduction; refusing publication')
    generated = previous.get('GeneratedAt') if previous.get('SourceCommit') == revision and previous.get('CollectorVersion') == COLLECTOR_VERSION and previous.get('TeamcraftCommit') == tc_revision and previous.get('ResearchSha256') == research_hash else datetime.now(timezone.utc).isoformat()
    document = dict(Schema=1, GeneratedAt=generated, SourceCommit=revision, Items=catalog_items, Needs=sorted(needs, key=lambda r: (r['QuestId'], r['ItemId'])),
                    Quests=sorted(quest_catalog, key=lambda q:q['QuestId']), Sources=links, TeamcraftCommit=tc_revision, Research=research)
    data = gzip.compress(json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode(), mtime=0)
    digest = hashlib.sha256(data).hexdigest()
    # Content-addressed files prevent manifest/payload races through GitHub's CDN.
    asset = f'catalog-{digest[:16]}.json.gz'
    (output / asset).write_bytes(data)
    manifest = dict(Schema=1, CollectorVersion=COLLECTOR_VERSION, GeneratedAt=generated, SourceCommit=revision, File=asset, Sha256=digest, Bytes=len(data),
                    ItemCount=len(catalog_items), QuestCount=len(quest_catalog), QuestItemCount=len(needs), ExactCount=sum(n['Count'] is not None for n in needs), SourceCount=len(links), TeamcraftCommit=tc_revision, ResearchCount=len(research), ResearchSha256=research_hash, PreviousFile=retained_payload(previous, asset, output))
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    # Keep the current and immediately previous content-addressed payload for CDN readers.
    keep = {asset, manifest.get('PreviousFile')}
    for obsolete in output.glob('catalog-*.json.gz'):
        if obsolete.name not in keep and obsolete.resolve().parent == output.resolve():
            obsolete.unlink()
    print(json.dumps(manifest))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--revision')
    parser.add_argument('--teamcraft-revision')
    args = parser.parse_args()
    collect(args.output, args.cache, args.revision, args.teamcraft_revision)
