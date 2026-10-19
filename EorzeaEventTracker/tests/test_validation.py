import copy
import unittest
import json
import tempfile
from pathlib import Path
from unittest.mock import patch
from collector.validate import Gate, Contradiction, validate, fingerprint, main


LOCATION = dict(id='npc-100-map-3', npc='Merchant', mapId=3, territoryId=133, x=10., y=9.)


class Api:
    version = 'test-version'
    schema = 'test-schema'

    def __init__(self, unavailable=False):
        self.unavailable = unavailable
        self.calls = 0
        self.price = 410
        self.handlers = [200]
        self.quest_reward = 500

    def get(self, route, **params):
        self.calls += 1
        if self.unavailable:
            raise RuntimeError('Source temporarily unavailable')
        if route.startswith('sheet/Item/'):
            return {'fields': dict(Name='Prize', Icon={'id': 600})}
        if route.startswith('sheet/Quest/'):
            return {'fields': {'Name': 'Quest', 'Icon': {'id': 700}, 'Reward@as(raw)': [self.quest_reward], 'ItemCountReward': [1]}}
        if route == 'sheet/ENpcBase/100':
            return {'fields': {'ENpcData@as(raw)': self.handlers}}
        if route == 'search' and params['sheets'] == 'SpecialShop':
            return {'results': [{'row_id': 200}]}
        if route == 'sheet/SpecialShop/200':
            return {'fields': {'UseCurrencyType': 4, 'Item': [{
                'Item@as(raw)': [500, 0], 'ReceiveCount': [1, 0], 'ReceiveHq': [False, False],
                'CostType': [0, 0], 'CollectabilityCost': [0, 0],
                'ItemCost@as(raw)': [41629, 0], 'CurrencyCost': [self.price, 0]}]}}
        raise AssertionError((route, params))


class ValidationChecks(unittest.TestCase):
    def setUp(self):
        self.api = Api()
        self.reward = dict(id=500, name='Prize', icon=600, currency='MGF', price=410, locationId=LOCATION['id'])
        self.quest = dict(id=65537, name='Quest', icon=700, locationId=LOCATION['id'])
        self.feed = {'events': [dict(id='event', verified=True, locations=[LOCATION],
                                    tracking=dict(quests=[self.quest], rewards=[self.reward]))]}

    def test_matching_shop_is_specific_to_declared_merchant(self):
        gate = Gate(self.api)
        with patch.object(gate, 'point', return_value=100):
            gate.reward(self.reward, {LOCATION['id']: LOCATION})
            self.api.price = 999
            with self.assertRaises(Contradiction):
                gate.reward(self.reward, {LOCATION['id']: LOCATION})
            self.api.handlers = [999]
            with self.assertRaises(RuntimeError):
                gate.reward(self.reward, {LOCATION['id']: LOCATION})

    def test_wrong_item_icon_and_quest_reward_block_publication(self):
        gate = Gate(self.api)
        with self.assertRaises(Contradiction):
            gate.reward(dict(self.reward, icon=601), {})
        self.api.quest_reward = 501
        with self.assertRaises(Contradiction):
            gate.reward(dict(self.reward, earnedByQuest=65537), {})

    def test_quest_location_name_map_and_coordinates(self):
        gate = Gate(self.api)
        with patch.object(gate.locator, 'quest', return_value=LOCATION):
            gate.quest(self.quest, {LOCATION['id']: LOCATION})
            for field, wrong in [('npc', 'Other'), ('mapId', 4), ('territoryId', 134), ('x', 11.)]:
                with self.subTest(field=field), self.assertRaises(Contradiction):
                    gate.quest(self.quest, {LOCATION['id']: dict(LOCATION, **{field: wrong})})

    def test_entrance_requires_registry_and_keeps_original_destination(self):
        route = dict(entranceMapId=196, entranceTerritoryId=144, entranceNpcId=101, source='https://official.test/guide')
        entrance = dict(LOCATION, mapId=196, territoryId=144,
                        navigation=dict(mode='entrance', destination=LOCATION, source=route['source']))
        gate = Gate(self.api, {'133': route})
        with patch.object(gate, 'get', return_value={'fields': {'Singular': 'Wy'}}), patch.object(gate, 'point', return_value=101):
            self.assertEqual(gate.destination(entrance), LOCATION)
            entrance['navigation']['source'] = 'https://wrong.test/'
            with self.assertRaises(Contradiction):
                gate.destination(entrance)

    def test_outage_is_partial_and_bounded_not_passed(self):
        api = Api(unavailable=True)
        candidate, report = validate(self.feed, api)
        self.assertFalse(candidate['events'][0]['verified'])
        self.assertEqual(candidate['events'][0]['validation']['status'], 'partial')
        self.assertFalse(report['errors'])
        gate = Gate(api)
        for _ in range(20):
            with self.assertRaises(RuntimeError):
                gate.get('sheet/Item/500')
        self.assertEqual(gate.failures, 3)

    def test_stale_evidence_only_survives_unchanged_relationships(self):
        old = copy.deepcopy(self.feed)
        old['events'][0]['validation'] = dict(fingerprint=fingerprint(old['events'][0]), lastVerifiedAt='2026-01-01T00:00:00Z')
        candidate, _ = validate(self.feed, Api(True), previous=old)
        self.assertEqual(candidate['events'][0]['validation']['status'], 'stale')
        self.feed['events'][0]['tracking']['rewards'][0]['price'] = 999
        candidate, _ = validate(self.feed, Api(True), previous=old)
        self.assertEqual(candidate['events'][0]['validation']['status'], 'partial')
        self.assertIsNone(candidate['events'][0]['validation']['lastVerifiedAt'])

    def test_contradictions_return_errors_and_do_not_mutate_input(self):
        original = copy.deepcopy(self.feed)
        self.feed['events'][0]['tracking']['quests'][0]['name'] = 'Wrong quest'
        _, report = validate(self.feed, self.api)
        self.assertTrue(any('Quest name' in e for e in report['errors']))
        self.assertNotIn('validation', self.feed['events'][0])
        self.assertNotIn('validation', original['events'][0])

    def test_passed_links_do_not_promote_automatic_event(self):
        self.feed['events'][0]['verified'] = False
        with patch.object(Gate, 'quest'), patch.object(Gate, 'reward'):
            candidate, report = validate(self.feed, self.api)
        self.assertEqual(candidate['events'][0]['validation']['status'], 'passed')
        self.assertFalse(candidate['events'][0]['verified'])
        self.assertFalse(report['errors'])

    def test_cli_rejects_candidate_without_replacing_input(self):
        with tempfile.TemporaryDirectory() as folder:
            source, report = Path(folder)/'events.json', Path(folder)/'report.json'
            source.write_text(json.dumps(self.feed), encoding='utf-8')
            original = source.read_bytes()
            with patch('sys.argv', ['validate', '--input', str(source), '--report', str(report)]), patch('collector.validate.GameData', return_value=self.api), patch.object(Gate, 'quest', side_effect=Contradiction('Wrong destination')), patch.object(Gate, 'reward'):
                with self.assertRaises(SystemExit) as error:
                    main()
            self.assertEqual(error.exception.code, 1)
            self.assertEqual(source.read_bytes(), original)
            self.assertTrue(json.loads(report.read_text(encoding='utf-8'))['errors'])


if __name__ == '__main__':
    unittest.main()
