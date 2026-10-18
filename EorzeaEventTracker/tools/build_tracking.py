"""Resolve event tracking IDs from English game sheets; run explicitly when updating the catalog."""
import hashlib
import json
from pathlib import Path
import requests

ROOT = Path(__file__).resolve().parents[1]
API = 'https://v2.xivapi.com/api'


def row(sheet, name):
    response = requests.get(API + '/search', params={'sheets': sheet, 'query': f'Name={json.dumps(name)}', 'fields': 'Name,Icon'}, timeout=30)
    response.raise_for_status()
    rows = [r for r in response.json()['results'] if r['fields']['Name'] == name]
    if len(rows) != 1:
        raise ValueError(f'Expected one exact {sheet} match: {name}; got {len(rows)}')
    result = rows[0]
    return dict(id=result['row_id'], name=name, icon=result['fields'].get('Icon', {}).get('id', 0))


def main():
    overrides = json.loads((ROOT / 'config/overrides.json').read_text(encoding='utf-8'))
    wake_url, fall_url = overrides.keys()
    wake_names = ['Grisly Bear Head', 'Grisly Bear Suit', 'Smiling Wakeshroom', 'Staring Wakeshroom', 'Wakesoul Buggy',
                  "All Saints' Wake MMXXV Advertisement", "All Saints' Wake MMXVI Advertisement", "Tabletop All Saints' Wake MMXXV Advertisement"]
    fall_prices = {
        'Gentlebean Knit Cap': 410, 'Gentlebean Parka': 410, 'Gentlebean Bottoms': 410, 'Gentlebean Shoes': 410,
        "Matte Winner's Crown": 410, "Glossy Winner's Crown": 410, 'Rhiyes Identification Key': 410,
        'Ballroom Etiquette - Embracing Blunders': 410, 'Pink Bean': 350, 'Pegwin': 350,
        'Blunderville Crown': 220, 'Blunderville Flag': 220, 'Blunderville Donut Bumper': 220,
        'Blunderville Padded Wall': 220, 'Blunderville Rainbow': 220, 'Blunderville Hammer': 220,
        'Everybody Falls (Fall Guys Theme) Orchestrion Roll': 220, "Blunderful Framer's Kit": 200,
        "Blunderous Framer's Kit": 200, "Blunder-villed Framer's Kit": 200, 'Standard Rationing Manual': 200,
        'Gold Saucer Aetheryte Ticket': 200, 'Wide Spectrum #1 Dye': 150}
    events = []
    for url, asset, quests, rewards in [
        (wake_url, 'saints.jpg', ['We Scare Bears', 'Grim and Grisly Games'], {name: 0 for name in wake_names}),
        (fall_url, 'fallguys.jpg', ['It Could Happen to You', 'Just Crowning Around'], fall_prices),
    ]:
        items = [dict(row('Item', name), price=price) for name, price in rewards.items()]
        resolved_quests = [row('Quest', name) for name in quests]
        if asset == 'saints.jpg':
            response = requests.get(API + f"/sheet/Quest/{resolved_quests[-1]['id']}", params={'fields': 'Reward,ItemCountReward'}, timeout=30)
            response.raise_for_status()
            fields = response.json()['fields']
            guaranteed = {r['row_id'] for r, count in zip(fields['Reward'], fields['ItemCountReward']) if count > 0}
            for item in items:
                if item['id'] in guaranteed:
                    item['earnedByQuest'] = resolved_quests[-1]['id']
        events.append(dict(eventId=hashlib.sha256(url.encode()).hexdigest()[:16], asset=asset, completionQuestId=resolved_quests[-1]['id'], quests=resolved_quests, rewards=items))
    (ROOT / 'config/tracking.json').write_text(json.dumps(events, indent=2) + '\n', encoding='utf-8')
    print('Resolved', sum(len(e['rewards']) for e in events), 'rewards and', sum(len(e['quests']) for e in events), 'quests.')


if __name__ == '__main__':
    main()
