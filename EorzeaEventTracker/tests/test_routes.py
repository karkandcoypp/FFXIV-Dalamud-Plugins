import copy
import unittest
from unittest.mock import Mock
from collector.routes import apply_map_routes

ROUTES = {'1197': {'entranceNpcId': 1046442, 'entranceTerritoryId': 144, 'entranceMapId': 196, 'source': 'https://na.finalfantasyxiv.com/official'}}
INSIDE = dict(id='merchant', npc='MGF trader', zone='Blunderville Square', territoryId=1197, mapId=891, x=3.852, y=3.172)
ENTRY = dict(id='npc-1046442-map-196', npc='Wy', zone='The Gold Saucer', territoryId=144, mapId=196, x=6.163, y=7.459)


class EntranceRouteTests(unittest.TestCase):
    def event(self):
        return {'locations': [copy.deepcopy(INSIDE)], 'tracking': {'rewards': [{'id': 1, 'locationId': 'merchant'}]}, 'verified': True}

    def test_inaccessible_zone_routes_to_verified_entrance(self):
        locator = Mock()
        locator.npc.return_value = ENTRY
        event = self.event()
        apply_map_routes(event, locator, ROUTES)
        location = event['locations'][0]
        self.assertEqual((location['territoryId'], location['mapId']), (144, 196))
        self.assertEqual((location['x'], location['y']), (6.163, 7.459))
        self.assertEqual(event['tracking']['rewards'][0]['locationId'], location['id'])
        self.assertEqual(location['navigation']['destination'], INSIDE)
        self.assertIn('entrance', location['npc'])
        self.assertIn('Кнопка «Куда идти» ведёт ко входу', event['translations']['ru'][event['notes'][0]])

    def test_unconfirmed_or_wrong_entrance_hides_link(self):
        for entrance in [None, dict(ENTRY, territoryId=999), dict(ENTRY, mapId=1)]:
            event = self.event()
            locator = Mock()
            locator.npc.return_value = entrance
            apply_map_routes(event, locator, ROUTES)
            self.assertEqual(event['locations'], [])
            self.assertEqual(event['tracking']['rewards'][0]['locationId'], '')
            self.assertFalse(event['verified'])

    def test_normal_zone_is_unchanged_and_no_api_query_is_needed(self):
        event = {'locations': [copy.deepcopy(ENTRY)]}
        locator = Mock()
        apply_map_routes(event, locator, ROUTES)
        self.assertEqual(event['locations'], [ENTRY])
        locator.npc.assert_not_called()

    def test_route_applies_to_any_event_in_the_same_zone(self):
        locator = Mock()
        locator.npc.return_value = ENTRY
        event = self.event()
        event['id'] = 'future-event-without-special-case-code'
        apply_map_routes(event, locator, ROUTES)
        self.assertEqual(event['automation']['navigationRoutes']['routed'], 1)
