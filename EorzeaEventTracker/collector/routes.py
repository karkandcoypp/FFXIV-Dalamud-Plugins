"""Publish entrance destinations for known areas that cannot be opened remotely."""
import json
import math
from pathlib import Path


def load_routes():
    path = Path(__file__).resolve().parents[1] / 'config' / 'map-routes.json'
    return json.loads(path.read_text(encoding='utf-8'))


def display(value):
    # FFXIV displays map coordinates by truncating to one decimal.
    return f'{math.floor(value * 10) / 10:.1f}'


def apply_map_routes(event, locator, routes=None):
    routes = load_routes() if routes is None else routes
    replacements, output, notes = {}, {}, []
    russian = event.setdefault('translations', {}).setdefault('ru', {})
    count = 0
    unavailable = []
    for location in event.get('locations', []):
        route = routes.get(str(location['territoryId']))
        if not route:
            output[location['id']] = location
            continue
        entrance = None
        try:
            entrance = locator.npc(route['entranceNpcId'])
        except Exception:
            pass
        if (not entrance or entrance['territoryId'] != route['entranceTerritoryId']
                or entrance['mapId'] != route['entranceMapId']
                or entrance['territoryId'] == location['territoryId']):
            replacements[location['id']] = ''
            unavailable.append(location['npc'])
            continue
        target = dict(entrance)
        label = entrance['npc'] + ' — entrance to ' + location['zone']
        target['npc'] = label
        target['navigation'] = dict(mode='entrance', destination=location, source=route['source'])
        # Keep merchant and quest targets distinct even if they share a physical entrance.
        target['id'] = entrance['id'] + '-entry-' + str(location['mapId'])
        output[target['id']] = target
        replacements[location['id']] = target['id']
        russian[label] = entrance['npc'] + ' — вход в ' + russian.get(location['zone'], location['zone'])
        message = (f"To reach {location['npc']} in {location['zone']} (X:{display(location['x'])} Y:{display(location['y'])}), "
                   f"speak with {entrance['npc']} in {entrance['zone']} (X:{display(entrance['x'])} Y:{display(entrance['y'])}). "
                   'The map button points to the entrance, not the merchant inside.')
        russian[message] = (f"Чтобы попасть к {russian.get(location['npc'], location['npc'])} в "
                            f"{russian.get(location['zone'], location['zone'])} (X:{display(location['x'])} Y:{display(location['y'])}), "
                            f"поговорите с {entrance['npc']} в {russian.get(entrance['zone'], entrance['zone'])} "
                            f"(X:{display(entrance['x'])} Y:{display(entrance['y'])}). "
                            'Кнопка «Куда идти» ведёт ко входу, а не к торговцу внутри.')
        notes.append(message)
        count += 1
    for kind in ('quests', 'rewards'):
        for target in event.get('tracking', {}).get(kind, []):
            if target.get('locationId') in replacements:
                target['locationId'] = replacements[target['locationId']]
    event['locations'] = list(output.values())
    if unavailable:
        message = 'Entrance route could not be confirmed; the inaccessible map link is hidden.'
        russian[message] = 'Не удалось подтвердить маршрут ко входу; ссылка на недоступную карту скрыта.'
        notes.append(message)
        event['verified'] = False
    if replacements:
        event['notes'] = list(dict.fromkeys(notes + event.get('notes', [])))[:200]
        event.setdefault('automation', {})['navigationRoutes'] = dict(routed=count, unavailable=unavailable)
    return event
