"""Evidence-based enrichment. Never guesses item IDs, prices or completion objectives."""
import re
from html import unescape
import time
from urllib.parse import urljoin, urlparse
import requests

API = 'https://v2.xivapi.com/api/'
SHOP_FIELDS = ('UseCurrencyType,Item[].Item@as(raw),Item[].ItemCost@as(raw),'
               'Item[].CurrencyCost,Item[].ReceiveCount,Item[].ReceiveHq,Item[].CostType,'
               'Item[].CollectabilityCost,Item[].Quest@as(raw),Item[].AchievementUnlock@as(raw)')
QUEST_FIELDS = 'Name,Icon,Reward@as(raw),ItemCountReward,PreviousQuest@as(raw)'


class GameData:
    def __init__(self, session=None):
        self.session = session or requests.Session()
        self.cache = {}
        self.version = None
        self.schema = None
        self.requests = 0

    def get(self, route, **params):
        key = (route, tuple(sorted(params.items())))
        if key in self.cache:
            return self.cache[key]
        if self.requests >= 700:
            raise RuntimeError('Game data request budget exceeded')
        self.requests += 1
        if self.version:
            params.update(version=self.version, schema=self.schema)
        time.sleep(.08)
        response = self.session.get(API + route, params=params, timeout=15,
                                    headers={'User-Agent': 'EorzeaEventTracker-feed/1.0'}, allow_redirects=False)
        if response.status_code != 200 or len(response.content) > 8_000_000:
            raise RuntimeError('Game data unavailable: ' + str(response.status_code))
        data = response.json()
        if not self.version:
            self.version, self.schema = data['version'], data['schema']
        if data.get('version') != self.version or data.get('schema') != self.schema:
            raise RuntimeError('Game data version changed during collection')
        self.cache[key] = data
        return data

    def exact(self, sheet, name, fields):
        # Quote search literals instead of interpreting page text as a query.
        literal = name.replace('\\', '\\\\').replace('"', '\\"')
        data = self.get('search', sheets=sheet, query='Name="' + literal + '"', fields=fields, limit=2)
        rows = data.get('results', [])
        if data.get('next') or len(rows) != 1 or rows[0]['fields'].get('Name') != name:
            return None
        return rows[0]

    def price(self, item_id):
        data = self.get('search', sheets='SpecialShop', query=f'Item[].Item[]={item_id}', fields='Name', limit=20)
        if data.get('next') or not data.get('results'):
            return None
        offers = set()
        for result in data['results']:
            shop = self.get('sheet/SpecialShop/' + str(result['row_id']), fields=SHOP_FIELDS)['fields']
            # Currency modes with indirect currency IDs are deliberately unsupported.
            if shop.get('UseCurrencyType') != 4:
                return None
            matched = False
            for entry in shop.get('Item', []):
                items = entry.get('Item@as(raw)', [])
                if item_id not in items:
                    continue
                matched = True
                index = items.index(item_id)
                if (sum(i != 0 for i in items) != 1 or entry['ReceiveCount'][index] != 1
                        or entry['ReceiveHq'][index] or any(entry['CostType'])
                        or any(entry['CollectabilityCost']) or entry.get('Quest@as(raw)', 0)
                        or entry.get('AchievementUnlock@as(raw)', 0)):
                    return None
                costs = [(i, n) for i, n in zip(entry['ItemCost@as(raw)'], entry['CurrencyCost']) if i or n]
                if len(costs) != 1 or not 0 < costs[0][0] <= 1_000_000 or not 0 < costs[0][1] <= 2147483647:
                    return None
                offers.add(tuple(costs[0]))
            if not matched:
                return None
        if len(offers) != 1:
            return None
        gil = self.get('search', sheets='GilShopItem', query=f'Item={item_id}',
                       fields='Item@as(raw)', limit=1)
        if gil.get('results') or gil.get('next'):
            return None
        currency, amount = offers.pop()
        row = self.get('sheet/Item/' + str(currency), fields='Name')['fields']
        if not row.get('Name'):
            return None
        return dict(price=amount, currencyItemId=currency, currency=row['Name'])


def official_links(soup, url, kind):
    result = []
    pattern = r'^/lodestone/playguide/db/' + kind + r'/[a-zA-Z0-9]+/?$'
    for a in soup.select('a[href]'):
        target = urljoin(url, a['href']).split('#')[0].split('?')[0]
        parsed = urlparse(target)
        if (parsed.scheme == 'https' and parsed.hostname == 'na.finalfantasyxiv.com'
                and re.match(pattern, parsed.path) and target not in result):
            result.append(target)
    return result


def db_name(soup, kind):
    # Official database titles carry the exact English name; avoid arbitrary headings.
    title = soup.title.get_text(' ', strip=True) if soup.title else ''
    match = re.match(r'Eorzea Database: (.+?) \| FINAL FANTASY XIV', title)
    return unescape(match.group(1)).strip() if match else None


def enrich(event, soup, page, api):
    issues = ['Automatic details may be incomplete. Main objective and NPC destinations are not inferred.']
    sources = [event['url']]
    rewards, quests = {}, {}
    reward_links = official_links(soup, event['url'], 'item')
    if len(reward_links) > 100:
        issues.append('Too many item links; only the first 100 were inspected.')
    for link in reward_links[:100]:
        try:
            name = db_name(page(link), 'item')
            row = api.exact('Item', name, 'Name,Icon') if name else None
            if not row or not 0 < row['row_id'] <= 1_000_000:
                issues.append('Unresolved or ambiguous reward: ' + (name or link))
                continue
            fields = row['fields']
            rewards[row['row_id']] = dict(id=row['row_id'], name=name, icon=fields['Icon']['id'], price=0)
            sources.append(link)
        except Exception:
            issues.append('Reward source unavailable: ' + link)
    # Prefer explicit quest links and headings identified as quest titles by the page itself.
    names = []
    headings = list(soup.select('[class*="quest"][class*="title"]'))
    for h in soup.select('h4'):
        context = h.find_previous(['h2', 'h3'])
        if context and 'quest' in context.get_text(' ', strip=True).lower():
            headings.append(h)
    for heading in headings:
        name = heading.get_text(' ', strip=True)
        if name and name not in names:
            names.append(name)
    for link in official_links(soup, event['url'], 'quest')[:16]:
        try:
            name = db_name(page(link), 'quest')
            if name and name not in names:
                names.append(name)
        except Exception:
            issues.append('Quest source unavailable: ' + link)
    queue = names[:16]
    for name in queue:
        try:
            row = api.exact('Quest', name, QUEST_FIELDS)
            if not row or not 65536 <= row['row_id'] <= 131071:
                issues.append('Unresolved or ambiguous quest: ' + name)
                continue
            # Follow only a unique direct continuation, bounded to avoid arbitrary quest graphs.
            for _ in range(8):
                qid, fields = row['row_id'], row['fields']
                if qid in quests:
                    break
                quests[qid] = dict(id=qid, name=fields['Name'], icon=fields['Icon']['id'])
                for item_id, count in zip(fields.get('Reward@as(raw)', []), fields.get('ItemCountReward', [])):
                    if count > 0 and item_id in rewards:
                        rewards[item_id]['earnedByQuest'] = qid
                children = api.get('search', sheets='Quest', query=f'PreviousQuest[]={qid}', fields=QUEST_FIELDS, limit=2)
                candidates = children.get('results', [])
                if children.get('next') or len(candidates) > 1:
                    issues.append('Branching quest chain: ' + fields['Name'])
                    break
                if not candidates:
                    break
                row = candidates[0]
                previous = [i for i in row['fields'].get('PreviousQuest@as(raw)', []) if i]
                if previous != [qid] or not 65536 <= row['row_id'] <= 131071:
                    issues.append('Quest continuation has additional prerequisites.')
                    break
        except Exception:
            issues.append('Quest data unavailable: ' + name)
    for reward in rewards.values():
        if reward.get('earnedByQuest'):
            continue
        try:
            price = api.price(reward['id'])
            if price:
                reward.update(price)
            else:
                issues.append('Price unknown or exchange is ambiguous: ' + reward['name'])
        except Exception:
            issues.append('Shop data unavailable: ' + reward['name'])
    if not reward_links:
        issues.append('No explicit reward database links found; image-only rewards need review.')
    if not quests:
        issues.append('No quest could be identified unambiguously.')
    meta = soup.find('meta', property='og:image')
    image = meta.get('content', '') if meta else ''
    parsed = urlparse(image)
    if parsed.scheme == 'https' and parsed.hostname in ('lds-img.finalfantasyxiv.com', 'img.finalfantasyxiv.com'):
        event['imageUrl'] = image
    event['tracking'] = dict(eventId=event['id'], quests=list(quests.values()), rewards=list(rewards.values()))
    event['rewards'] = [r['name'] for r in rewards.values()]
    event['verified'] = False  # Extracted facts do not prove that the entire event has been mapped.
    event['notes'] = list(dict.fromkeys(event.get('notes', [])[:1] + issues))[:200]
    event['automation'] = dict(status='partial', sources=sources, gameVersion=api.version,
                               schema=api.schema, issues=list(dict.fromkeys(issues)))
    message = issues[0]
    event.setdefault('translations', {}).setdefault('ru', {})[message] = (
        'Автоматически собранные сведения могут быть неполными. Основная цель и точки NPC не определяются автоматически.')
    return event
