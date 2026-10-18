"""Resolve map links from explicit quest/shop relationships, never from NPC name similarity."""
import math

LEVEL_FIELDS = 'X,Z,Type,Object@as(raw),Map@as(raw),Territory@as(raw)'
MAP_FIELDS = 'SizeFactor,OffsetX,OffsetY,TerritoryType@as(raw),PlaceName.Name'


def map_coordinate(world, offset, scale):
    values = (float(world), float(offset), float(scale))
    if not all(math.isfinite(v) for v in values) or values[2] <= 0:
        raise ValueError('Invalid map transform')
    factor = values[2] / 100
    return (41 / factor * (((values[0] + values[1]) * factor + 1024) / 2048)) + 1


class NpcLocator:
    def __init__(self, api):
        self.api = api
        self.cache = {}

    def level(self, level, npc_id):
        map_id, territory = level.get('Map@as(raw)', 0), level.get('Territory@as(raw)', 0)
        if (level.get('Type') != 8 or level.get('Object@as(raw)') != npc_id
                or not 0 < map_id <= 100000 or not 0 < territory <= 100000):
            return None
        mapped = self.api.get('sheet/Map/' + str(map_id), fields=MAP_FIELDS)['fields']
        if mapped.get('TerritoryType@as(raw)') != territory:
            return None
        x = map_coordinate(level['X'], mapped['OffsetX'], mapped['SizeFactor'])
        y = map_coordinate(level['Z'], mapped['OffsetY'], mapped['SizeFactor'])
        if not (0 <= x <= 100 and 0 <= y <= 100):
            return None
        name = self.api.get('sheet/ENpcResident/' + str(npc_id), fields='Singular')['fields'].get('Singular', '').strip()
        zone = mapped.get('PlaceName', {}).get('fields', {}).get('Name', '').strip()
        if not name or not zone:
            return None
        return dict(id=f'npc-{npc_id}-map-{map_id}', npc=name, zone=zone,
                    territoryId=territory, mapId=map_id, x=round(x, 3), y=round(y, 3))

    def quest(self, quest_id):
        key = ('quest', quest_id)
        if key not in self.cache:
            q = self.api.get('sheet/Quest/' + str(quest_id), fields='IssuerStart@as(raw),IssuerLocation@as(raw)')['fields']
            npc, level_id = q.get('IssuerStart@as(raw)', 0), q.get('IssuerLocation@as(raw)', 0)
            result = None
            if npc and level_id:
                level = self.api.get('sheet/Level/' + str(level_id), fields=LEVEL_FIELDS)['fields']
                result = self.level(level, npc)
            self.cache[key] = result
        return self.cache[key]

    def npc(self, npc_id):
        key = ('npc', npc_id)
        if key not in self.cache:
            result = self.api.get('search', sheets='Level', query=f'+Object={npc_id} +Type=8',
                                  fields=LEVEL_FIELDS, limit=20)
            locations = {}
            if not result.get('next'):
                for row in result.get('results', []):
                    location = self.level(row['fields'], npc_id)
                    if location:
                        pos = (location['mapId'], location['territoryId'], location['x'], location['y'])
                        locations[pos] = location
                    else:
                        # An unresolved placement may be a different instance; do not ignore it.
                        locations[('unknown', row['row_id'])] = None
            self.cache[key] = next(iter(locations.values())) if len(locations) == 1 else None
        return self.cache[key]

    def merchant(self, item_id):
        shops = []
        for sheet, query in [('SpecialShop', f'Item[].Item[]={item_id}'), ('GilShopItem', f'Item={item_id}')]:
            result = self.api.get('search', sheets=sheet, query=query, fields='', limit=20)
            if result.get('next'):
                return None
            shops.extend(row['row_id'] for row in result.get('results', []))
        npcs = set()
        for shop in set(shops):
            result = self.api.get('search', sheets='ENpcBase', query=f'ENpcData[]={shop}',
                                  fields='ENpcData@as(raw)', limit=2)
            rows = result.get('results', [])
            if result.get('next') or not rows:
                return None
            for row in rows:
                if shop not in row['fields'].get('ENpcData@as(raw)', []):
                    return None
                npcs.add(row['row_id'])
        return self.npc(next(iter(npcs))) if len(npcs) == 1 else None


def enrich_locations(event, api):
    locator = NpcLocator(api)
    existing = {loc['id']: loc for loc in event.get('locations', [])}
    issues = []
    mapped = 0

    def attach(target, location):
        nonlocal mapped
        if not location:
            issues.append('NPC destination is ambiguous or not mapped: ' + target['name'])
            return
        # A moved placement with the same NPC/map must not overwrite another destination.
        key = location['id']
        if key in existing and existing[key] != location:
            location = dict(location, id=key + '-' + str(len(existing)))
        if len(existing) >= 32 and location['id'] not in existing:
            issues.append('NPC destination limit reached.')
            return
        existing[location['id']] = location
        target['locationId'] = location['id']
        mapped += 1

    tracking = event.get('tracking', {})
    for quest in tracking.get('quests', []):
        if quest.get('locationId'):
            continue
        try:
            attach(quest, locator.quest(quest['id']))
        except Exception:
            issues.append('NPC source unavailable: ' + quest['name'])
    quest_destinations = {q['id']: q.get('locationId') for q in tracking.get('quests', [])}
    for reward in tracking.get('rewards', []):
        if reward.get('locationId'):
            continue
        # Guaranteed prizes are collected through the quest; its issuer is a safe starting point.
        if reward.get('earnedByQuest'):
            location_id = quest_destinations.get(reward['earnedByQuest'])
            if location_id:
                reward['locationId'] = location_id
            continue
        try:
            attach(reward, locator.merchant(reward['id']))
        except Exception:
            issues.append('NPC source unavailable: ' + reward['name'])
    event['locations'] = list(existing.values())
    return dict(mapped=mapped, locations=len(existing), issues=list(dict.fromkeys(issues)))


def retain_failed_locations(event, previous):
    """Keep a last known destination only on a source error, never on a genuine ambiguity."""
    failures = {issue.removeprefix('NPC source unavailable: ') for issue in
                event.get('automation', {}).get('issues', []) if issue.startswith('NPC source unavailable: ')}
    if not failures or not previous:
        return
    old_locations = {loc['id']: loc for loc in previous.get('locations', [])}
    locations = {loc['id']: loc for loc in event.get('locations', [])}
    retained = 0
    for kind in ('quests', 'rewards'):
        old_targets = {target['id']: target for target in previous.get('tracking', {}).get(kind, [])}
        for target in event.get('tracking', {}).get(kind, []):
            old = old_targets.get(target['id'], {})
            loc = old_locations.get(old.get('locationId'))
            if target.get('locationId') or target.get('name') not in failures or not loc:
                continue
            if loc['id'] in locations and locations[loc['id']] != loc:
                loc = dict(loc, id='retained-' + loc['id'])
            if len(locations) >= 32 and loc['id'] not in locations:
                continue
            locations[loc['id']] = loc
            target['locationId'] = loc['id']
            retained += 1
    event['locations'] = list(locations.values())
    if retained:
        event.setdefault('automation', {}).setdefault('npc', {})['retained'] = retained
        event['notes'] = list(dict.fromkeys(event.get('notes', []) +
            ['NPC source temporarily unavailable; some destinations use their last confirmed position.']))[:200]
