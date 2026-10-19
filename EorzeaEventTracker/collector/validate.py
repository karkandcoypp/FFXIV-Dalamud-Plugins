"""Publication gate: validate relationships, not merely the existence of row IDs.

Contradictions stop publication. Unavailable/unsupported evidence stays partial;
it never turns an automatically discovered event into a reviewed event.
"""
import argparse
import copy
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path

try:
    from .enrich import GameData, SHOP_FIELDS
    from .npc import NpcLocator, LEVEL_FIELDS
except ImportError:
    from enrich import GameData, SHOP_FIELDS
    from npc import NpcLocator, LEVEL_FIELDS


class Contradiction(ValueError):
    pass


class Gate:
    def __init__(self, api, routes=None):
        self.api = api
        self.locator = NpcLocator(self)
        self.routes = routes or {}
        self.failures = 0
        self.checked = 0
        self.locations = {}

    def get(self, route, **params):
        if self.failures >= 3:
            raise RuntimeError('Game-data source unavailable; validation circuit is open')
        try:
            result = self.api.get(route, **params)
        except Exception as error:
            if '404' in str(error) and route.startswith('sheet/'):
                raise Contradiction('Referenced game-data row does not exist: ' + route) from error
            self.failures += 1
            raise
        self.checked += 1
        return result

    @staticmethod
    def same(claim, actual):
        if not actual:
            raise RuntimeError('No unique NPC placement is available')
        if (claim['npc'].casefold() != actual['npc'].casefold()
                or claim['mapId'] != actual['mapId'] or claim['territoryId'] != actual['territoryId']
                or abs(claim['x'] - actual['x']) > .15 or abs(claim['y'] - actual['y']) > .15):
            raise Contradiction('NPC/map/coordinates disagree with game data: ' + claim['npc'])

    def point(self, claim, npc_id=None):
        key = json.dumps(claim, sort_keys=True) + ':' + str(npc_id)
        if key in self.locations:
            return self.locations[key]
        if not npc_id:
            match = re.match(r'npc-(\d+)-map-', claim['id'])
            if match:
                npc_id = int(match[1])
            else:
                literal = claim['npc'].replace('\\', '\\\\').replace('"', '\\"')
                result = self.get('search', sheets='ENpcResident', query=f'Singular="{literal}"', fields='Singular', limit=20)
                rows = result.get('results', [])
                if not rows and not result.get('next'):
                    # The index is case sensitive for exact queries; accept only full
                    # case-folded equality below, never a substring/name similarity.
                    result = self.get('search', sheets='ENpcResident', query=f'Singular~"{literal}"', fields='Singular', limit=20)
                    rows = result.get('results', [])
                if result.get('next') or not rows:
                    raise RuntimeError('NPC identity is ambiguous: ' + claim['npc'])
                candidates = []
                contradictions = 0
                exact = 0
                for row in rows:
                    if row['fields'].get('Singular', '').casefold() != claim['npc'].casefold():
                        continue
                    exact += 1
                    try:
                        candidates.append(self.point(claim, row['row_id']))
                    except Contradiction:
                        contradictions += 1
                    except RuntimeError:
                        continue
                if len(candidates) != 1:
                    if exact and contradictions == exact:
                        raise Contradiction('NPC coordinates disagree with all exact-name placements: ' + claim['npc'])
                    raise RuntimeError('NPC identity/placement is ambiguous: ' + claim['npc'])
                self.locations[key] = candidates[0]
                return candidates[0]
        result = self.get('search', sheets='Level', query=f'+Object={npc_id} +Type=8', fields=LEVEL_FIELDS, limit=100)
        if result.get('next'):
            raise RuntimeError('NPC placement query was truncated')
        resolved = False
        for row in result.get('results', []):
            actual = self.locator.level(row['fields'], npc_id)
            resolved = resolved or actual is not None
            try:
                self.same(claim, actual)
            except (Contradiction, RuntimeError):
                continue
            self.locations[key] = npc_id
            return npc_id
        if resolved:
            raise Contradiction('Declared NPC location disagrees with known placements: ' + claim['npc'])
        # Dynamic quest actors do not always have a static Level entry.
        raise RuntimeError('Declared NPC placement could not be confirmed: ' + claim['npc'])

    def destination(self, claim):
        navigation = claim.get('navigation')
        if not navigation:
            return claim
        actual = navigation.get('destination', {})
        route = self.routes.get(str(actual.get('territoryId')))
        if (navigation.get('mode') != 'entrance' or not route
                or claim.get('mapId') != route['entranceMapId']
                or claim.get('territoryId') != route['entranceTerritoryId']
                or navigation.get('source') != route['source']):
            raise Contradiction('Unapproved or inconsistent entrance route: ' + claim.get('id', '?'))
        # Entrance captions may explain the destination; identity comes from the registry's NPC ID.
        name = self.get('sheet/ENpcResident/' + str(route['entranceNpcId']), fields='Singular')['fields']['Singular']
        self.point(dict(claim, npc=name), route['entranceNpcId'])
        return actual

    def quest(self, quest, locations):
        row = self.get('sheet/Quest/' + str(quest['id']),
                       fields='Name,Icon,IssuerStart@as(raw),IssuerLocation@as(raw),Reward@as(raw),ItemCountReward')['fields']
        if quest['name'] != row['Name']:
            raise Contradiction('Quest name does not match its ID: ' + quest['name'])
        if quest.get('icon') and quest['icon'] != row.get('Icon', {}).get('id'):
            raise Contradiction('Quest icon does not match its ID: ' + quest['name'])
        claim = locations.get(quest.get('locationId'))
        if not claim:
            raise RuntimeError('Quest NPC location is missing: ' + quest['name'])
        self.same(self.destination(claim), self.locator.quest(quest['id']))
        return row

    def reward(self, reward, locations):
        row = self.get('sheet/Item/' + str(reward['id']), fields='Name,Icon')['fields']
        if reward['name'] != row['Name'] or reward.get('icon', 0) not in (0, row['Icon']['id']):
            raise Contradiction('Reward name/icon does not match its item ID: ' + reward['name'])
        claim = locations.get(reward.get('locationId'))
        if reward.get('earnedByQuest'):
            qid = reward['earnedByQuest']
            q = self.get('sheet/Quest/' + str(qid), fields='Reward@as(raw),ItemCountReward')['fields']
            if not any(i == reward['id'] and n > 0 for i, n in zip(q.get('Reward@as(raw)', []), q.get('ItemCountReward', []))):
                raise Contradiction('Reward is not guaranteed by the declared quest: ' + reward['name'])
            if not claim:
                raise RuntimeError('Quest reward destination is missing: ' + reward['name'])
            self.same(self.destination(claim), self.locator.quest(qid))
            return
        if not claim:
            raise RuntimeError('Reward merchant is missing: ' + reward['name'])
        npc_id = self.point(self.destination(claim))
        handlers = self.get('sheet/ENpcBase/' + str(npc_id), fields='ENpcData@as(raw)')['fields'].get('ENpcData@as(raw)', [])
        result = self.get('search', sheets='SpecialShop', query=f"Item[].Item[]={reward['id']}", fields='', limit=20)
        if result.get('next'):
            raise RuntimeError('Shop query was truncated')
        shops = {r['row_id'] for r in result.get('results', [])} & set(handlers)
        offers = set()
        unsupported = False
        for shop_id in shops:
            shop = self.get('sheet/SpecialShop/' + str(shop_id), fields=SHOP_FIELDS)['fields']
            if shop.get('UseCurrencyType') != 4:
                unsupported = True
                continue
            for entry in shop.get('Item', []):
                items = entry.get('Item@as(raw)', [])
                if reward['id'] not in items:
                    continue
                idx = items.index(reward['id'])
                if (sum(i != 0 for i in items) != 1 or entry['ReceiveCount'][idx] != 1 or entry['ReceiveHq'][idx]
                        or any(entry['CostType']) or any(entry['CollectabilityCost'])
                        or entry.get('Quest@as(raw)', 0) or entry.get('AchievementUnlock@as(raw)', 0)):
                    unsupported = True
                    continue
                costs = [(i, n) for i, n in zip(entry['ItemCost@as(raw)'], entry['CurrencyCost']) if i or n]
                if len(costs) == 1 and costs[0][0] > 0 and costs[0][1] > 0:
                    offers.add(tuple(costs[0]))
                else:
                    unsupported = True
        # Match TrackedReward.Currency's default when reading legacy feeds.
        currency_label = reward.get('currency', 'MGF')
        currency = reward.get('currencyItemId') or (41629 if currency_label == 'MGF' else 0)
        if currency and not (currency == 41629 and currency_label == 'MGF'):
            name = self.get('sheet/Item/' + str(currency), fields='Name')['fields'].get('Name')
            if not name or currency_label.casefold() != name.casefold():
                raise Contradiction('Currency name disagrees with its item ID: ' + reward['name'])
        if not shops or not offers or not currency or reward.get('price', 0) <= 0:
            raise RuntimeError('Merchant/price relationship is not fully supported or known: ' + reward['name'])
        if (currency, reward['price']) not in offers:
            if unsupported:
                raise RuntimeError('Conditional shop offer cannot be verified: ' + reward['name'])
            raise Contradiction('Price/currency disagrees with this merchant: ' + reward['name'])


def fingerprint(event):
    fields = {k: event.get(k) for k in ('tracking', 'locations')}
    return hashlib.sha256(json.dumps(fields, sort_keys=True, ensure_ascii=False).encode()).hexdigest()


def validate(feed, api, routes=None, previous=None):
    candidate = copy.deepcopy(feed)
    gate = Gate(api, routes)
    old = {e['id']: e for e in (previous or {}).get('events', [])}
    errors = []
    now = datetime.now(timezone.utc).isoformat()
    for event in candidate['events']:
        issues, before, errors_before = [], gate.checked, len(errors)
        tracking = event.get('tracking') or {}
        locations = {l['id']: l for l in event.get('locations', [])}
        if not tracking.get('quests') or not tracking.get('rewards'):
            issues.append('Quest or reward coverage is incomplete')
        for kind in ('quests', 'rewards'):
            for row in tracking.get(kind, []):
                try:
                    if row.get('locationId') and row['locationId'] not in locations:
                        raise Contradiction('Unknown location reference: ' + row['locationId'])
                    (gate.quest if kind == 'quests' else gate.reward)(row, locations)
                except Contradiction as error:
                    errors.append(event['id'] + ': ' + str(error))
                except Exception as error:
                    issues.append(str(error)[:1000])
        used = {r.get('locationId') for kind in ('quests', 'rewards') for r in tracking.get(kind, [])}
        for location_id, location in locations.items():
            if location_id in used:
                continue
            try:
                gate.point(gate.destination(location))
            except Contradiction as error:
                errors.append(event['id'] + ': ' + str(error))
            except Exception as error:
                issues.append(str(error)[:1000])
        digest = fingerprint(event)
        prior = old.get(event['id'], {}).get('validation', {})
        status = 'failed' if len(errors) > errors_before else 'partial' if issues else 'passed'
        last_verified = now if status == 'passed' else None
        if status == 'partial' and prior.get('fingerprint') == digest and prior.get('lastVerifiedAt'):
            last_verified = prior['lastVerifiedAt']
            status = 'stale'
        event['validation'] = dict(status=status, checkedAt=now, lastVerifiedAt=last_verified,
                                   fingerprint=digest, checks=gate.checked-before, issues=list(dict.fromkeys(issues))[:200])
        if status != 'passed':
            event['verified'] = False
    return candidate, dict(checkedAt=now, errors=errors, gameVersion=api.version, schema=api.schema,
                           events={e['id']: e['validation'] for e in candidate['events']})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True, type=Path)
    parser.add_argument('--previous', type=Path)
    parser.add_argument('--report', type=Path, default=Path('config/validation-report.json'))
    parser.add_argument('--check-only', action='store_true')
    args = parser.parse_args()
    feed = json.loads(args.input.read_text(encoding='utf-8'))
    previous = json.loads(args.previous.read_text(encoding='utf-8')) if args.previous and args.previous.exists() else None
    routes = json.loads((Path(__file__).resolve().parent.parent/'config/map-routes.json').read_text(encoding='utf-8'))
    candidate, report = validate(feed, GameData(), routes, previous)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    for error in report['errors']:
        print('ERROR: ' + error)
    print('Relationship validation: ' + ', '.join(k+': '+v['status'] for k, v in report['events'].items()))
    if report['errors']:
        raise SystemExit(1)
    if not args.check_only:
        temporary = args.input.with_suffix('.validation.tmp')
        temporary.write_text(json.dumps(candidate, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
        temporary.replace(args.input)


if __name__ == '__main__':
    main()
