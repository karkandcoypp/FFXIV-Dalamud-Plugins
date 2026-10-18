import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import Mock, patch
from collector.npc import NpcLocator, map_coordinate, enrich_locations, retain_failed_locations
from collector.translate import RussianTranslator, MACHINE_NOTE, PENDING_NOTE


class MapApi:
    def __init__(self, multiple=False, mismatch=False):
        self.multiple, self.mismatch = multiple, mismatch
    def get(self, route, **params):
        if route.startswith('sheet/Quest/'):
            return {'fields': {'IssuerStart@as(raw)': 1001, 'IssuerLocation@as(raw)': 100}}
        if route.startswith('sheet/Level/'):
            return {'fields': self.level()}
        if route.startswith('sheet/Map/'):
            return {'fields': {'TerritoryType@as(raw)': 999 if self.mismatch else 133,
                    'SizeFactor': 200, 'OffsetX': 0, 'OffsetY': 0,
                    'PlaceName': {'fields': {'Name': 'Old Gridania'}}}}
        if route.startswith('sheet/ENpcResident/'):
            return {'fields': {'Singular': 'Event NPC'}}
        if route == 'search':
            if params['sheets'] == 'Level':
                rows = [{'row_id': 100, 'fields': self.level()}]
                if self.multiple:
                    rows.append({'row_id': 101, 'fields': dict(self.level(), X=50)})
                return {'results': rows}
            if params['sheets'] == 'SpecialShop':
                return {'results': [{'row_id': 1770724}]}
            if params['sheets'] == 'GilShopItem':
                return {'results': []}
            if params['sheets'] == 'ENpcBase':
                return {'results': [{'row_id': 1001, 'fields': {'ENpcData@as(raw)': [1770724]}}]}
        raise AssertionError(route)
    def level(self):
        return {'X': -50, 'Z': -88.3956, 'Type': 8, 'Object@as(raw)': 1001,
                'Map@as(raw)': 3, 'Territory@as(raw)': 133}


class NpcTests(unittest.TestCase):
    def test_world_coordinates_match_dalamud_formula(self):
        self.assertAlmostEqual(map_coordinate(-50, 0, 200), 10.2490234375)
        self.assertAlmostEqual(map_coordinate(-88.3956, 0, 200), 9.4803615, places=5)
        self.assertAlmostEqual(map_coordinate(10, 100, 100), 23.7021484375)
        for scale in [0, float('nan'), float('inf')]:
            with self.assertRaises(ValueError): map_coordinate(0, 0, scale)

    def test_quest_issuer_gets_matching_map_and_territory(self):
        location = NpcLocator(MapApi()).quest(71003)
        self.assertEqual((location['mapId'], location['territoryId']), (3, 133))
        self.assertEqual(location['npc'], 'Event NPC')
        self.assertIsNone(NpcLocator(MapApi(mismatch=True)).quest(71003))

    def test_unique_merchant_has_location_multiple_placements_do_not(self):
        self.assertIsNotNone(NpcLocator(MapApi()).merchant(10))
        self.assertIsNone(NpcLocator(MapApi(multiple=True)).merchant(10))

    def test_wrong_object_or_type_is_rejected(self):
        locator = NpcLocator(MapApi())
        self.assertIsNone(locator.level(dict(MapApi().level(), Type=9), 1001))
        self.assertIsNone(locator.level(MapApi().level(), 2002))

    def test_truncated_placements_never_pick_first(self):
        api = MapApi()
        original = api.get
        api.get = lambda route, **params: dict(original(route, **params), next='more') if route == 'search' and params['sheets'] == 'Level' else original(route, **params)
        self.assertIsNone(NpcLocator(api).merchant(10))

    def test_location_references_and_guaranteed_reward_start_are_published(self):
        event = {'tracking': {'quests': [{'id': 71003, 'name': 'Quest'}],
                             'rewards': [{'id': 10, 'name': 'Prize'}, {'id': 11, 'name': 'Quest prize', 'earnedByQuest': 71003}]}}
        enrich_locations(event, MapApi())
        ids = {loc['id'] for loc in event['locations']}
        for target in event['tracking']['quests'] + event['tracking']['rewards']:
            self.assertIn(target['locationId'], ids)


def response(text='Приз', source='Prize', status=200, quota=False):
    r = Mock(status_code=200, content=b'{}')
    r.json.return_value = {'responseStatus': status, 'quotaFinished': quota,
        'responseData': {'translatedText': text, 'match': .8},
        'matches': [{'segment': source, 'translation': text, 'source': 'en-GB', 'target': 'ru-RU'}]}
    return r


class TranslationTests(unittest.TestCase):
    def translator(self, folder, response_value=None, **kwargs):
        session = Mock()
        session.get.return_value = response_value or response()
        return RussianTranslator(Path(folder)/'cache.json', session=session, **kwargs), session

    @patch('collector.translate.time.sleep')
    def test_translation_is_cached_across_restarts(self, sleep):
        with TemporaryDirectory() as folder:
            translator, session = self.translator(folder)
            self.assertEqual(translator.translate('Prize'), 'Приз')
            translator.save()
            restarted, second = self.translator(folder)
            self.assertEqual(restarted.translate('Prize'), 'Приз')
            second.get.assert_not_called()
            self.assertEqual(session.get.call_count, 1)

    @patch('collector.translate.time.sleep')
    def test_fuzzy_match_and_service_messages_are_rejected(self, sleep):
        with TemporaryDirectory() as folder:
            translator, _ = self.translator(folder, response(source='Other prize'))
            self.assertIsNone(translator.translate('Prize'))
            translator, _ = self.translator(folder, response(text='YOU USED ALL AVAILABLE FREE TRANSLATIONS', status=403))
            self.assertIsNone(translator.translate('Prize'))
            self.assertTrue(translator.blocked)

    def test_numbers_markup_urls_and_placeholders_are_preserved(self):
        self.assertFalse(RussianTranslator.valid('Level 15', 'Уровень 50'))
        self.assertTrue(RussianTranslator.valid('Level 15', 'Уровень 15'))
        self.assertFalse(RussianTranslator.valid('Prize', '<script>Приз</script>'))
        self.assertFalse(RussianTranslator.valid('Prize', 'Приз https://evil.test'))
        self.assertFalse(RussianTranslator.valid('Get {0} prizes', 'Получить призы'))

    def test_quota_does_not_make_requests(self):
        with TemporaryDirectory() as folder:
            translator, session = self.translator(folder, run_limit=1)
            self.assertIsNone(translator.translate('Prize'))
            session.get.assert_not_called()

    def test_reviewed_translation_wins_and_missing_text_keeps_english(self):
        with TemporaryDirectory() as folder:
            translator, session = self.translator(folder, run_limit=0)
            event = {'title': 'Event', 'notes': [], 'translations': {'ru': {'Event': 'Мой перевод'}}}
            translator.apply(event)
            self.assertEqual(event['translations']['ru']['Event'], 'Мой перевод')
            event = {'title': 'New event', 'notes': []}
            translator.apply(event)
            self.assertNotIn('New event', event['translations']['ru'])
            self.assertIn(PENDING_NOTE, event['notes'])
            session.get.assert_not_called()

    def test_conflicting_reviewed_glossary_is_not_globalized(self):
        with TemporaryDirectory() as folder:
            translator, _ = self.translator(folder, run_limit=0)
            translator.seed([{'translations': {'ru': {'Event': 'Событие'}}}])
            translator.seed([{'translations': {'ru': {'Event': 'Событие'}}}, {'translations': {'ru': {'Event': 'Ивент'}}}])
            self.assertIsNone(translator.translate('Event'))

    @patch('collector.translate.time.sleep')
    def test_automatic_translations_are_labelled(self, sleep):
        with TemporaryDirectory() as folder:
            translator, _ = self.translator(folder)
            event = {'title': 'Prize', 'notes': []}
            translator.apply(event)
            self.assertEqual(event['translations']['ru']['Prize'], 'Приз')
            self.assertIn(MACHINE_NOTE, event['notes'])
            self.assertEqual(event['automation']['russian']['status'], 'complete')


class TranslationBudgetTests(unittest.TestCase):
    def test_daily_budget_survives_restarts(self):
        with TemporaryDirectory() as folder:
            session = Mock()
            session.get.return_value = response()
            translator = RussianTranslator(Path(folder)/'cache.json', session=session, daily_limit=5)
            with patch('collector.translate.time.sleep'):
                self.assertEqual(translator.translate('Prize'), 'Приз')
            translator.save()
            translator = RussianTranslator(Path(folder)/'cache.json', session=session, daily_limit=5)
            self.assertIsNone(translator.translate('Quest'))
            self.assertEqual(session.get.call_count, 1)

    def test_long_text_is_segmented_and_validated(self):
        with TemporaryDirectory() as folder:
            session = Mock()
            def translated(url, **kwargs):
                source = kwargs['params']['q']
                if len(source.encode('utf-8')) > 450:
                    raise AssertionError('oversized translation request')
                return response(text='Перевод ' + source, source=source)
            session.get.side_effect = translated
            translator = RussianTranslator(Path(folder)/'cache.json', session=session, run_limit=2000)
            with patch('collector.translate.time.sleep'):
                self.assertIsNotNone(translator.translate('Seasonal adventure rewards. ' * 25))
            self.assertGreater(session.get.call_count, 1)


class NpcRetentionTests(unittest.TestCase):
    def test_source_failure_retains_previous_location_but_ambiguity_does_not(self):
        location = NpcLocator(MapApi()).quest(71003)
        old = {'tracking': {'quests': [{'id': 71003, 'name': 'Quest', 'locationId': location['id']}]}, 'locations': [location]}
        for issue, expected in [('NPC source unavailable: Quest', True), ('NPC destination is ambiguous or not mapped: Quest', False)]:
            event = {'tracking': {'quests': [{'id': 71003, 'name': 'Quest'}]}, 'automation': {'issues': [issue]}}
            retain_failed_locations(event, old)
            self.assertEqual(bool(event['tracking']['quests'][0].get('locationId')), expected)
