import copy
import json
import unittest
from argparse import Namespace
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch, Mock
from bs4 import BeautifulSoup
from collector.enrich import GameData, enrich, db_name, official_links
from collector.collect import run, extract, special_links

URL = 'https://na.finalfantasyxiv.com/lodestone/special/test/'
ITEM = 'https://na.finalfantasyxiv.com/lodestone/playguide/db/item/abc/'


def entry(currency=41629, cost=410, quest=0, count=1):
    return {'Item@as(raw)': [41560, 0], 'ItemCost@as(raw)': [currency, 0, 0],
            'CurrencyCost': [cost, 0, 0], 'ReceiveCount': [count, 1], 'ReceiveHq': [False, False],
            'CostType': [0, 0, 0], 'CollectabilityCost': [0, 0, 0],
            'Quest@as(raw)': quest, 'AchievementUnlock@as(raw)': 0}


class Shops(GameData):
    def __init__(self, entries, mode=4, gil=False, truncated=False):
        self.entries, self.mode, self.gil, self.truncated = entries, mode, gil, truncated

    def get(self, route, **params):
        if route == 'search':
            if params['sheets'] == 'GilShopItem':
                return {'results': [{'row_id': 1}] if self.gil else []}
            return dict(results=[{'row_id': 1}], **({'next': 'more'} if self.truncated else {}))
        if route.startswith('sheet/SpecialShop'):
            return {'fields': {'UseCurrencyType': self.mode, 'Item': self.entries}}
        return {'fields': {'Name': 'MGF'}}


class EnrichmentTests(unittest.TestCase):
    def test_single_exchange_and_identical_duplicates(self):
        self.assertEqual(Shops([entry(), entry()]).price(41560),
                         dict(price=410, currencyItemId=41629, currency='MGF'))

    def test_ambiguous_and_complex_exchanges_are_unknown(self):
        complex_entry = entry()
        complex_entry['ItemCost@as(raw)'][1] = 1
        complex_entry['CurrencyCost'][1] = 100
        for api in [Shops([entry(), entry(cost=500)]), Shops([entry(quest=1)]),
                    Shops([entry(count=2)]), Shops([complex_entry]), Shops([entry()], mode=1),
                    Shops([entry()], gil=True), Shops([entry()], truncated=True)]:
            self.assertIsNone(api.price(41560))

    def test_ambiguous_exact_names_are_rejected(self):
        api = GameData()
        api.get = Mock(return_value={'results': [{'fields': {'Name': 'X'}}, {'fields': {'Name': 'X'}}]})
        self.assertIsNone(api.exact('Item', 'X', 'Name'))
        api.get.return_value = {'results': [{'fields': {'Name': 'Y'}}]}
        self.assertIsNone(api.exact('Item', 'X', 'Name'))
        api.get.return_value = {'results': [{'fields': {'Name': 'X'}}], 'next': 'more'}
        self.assertIsNone(api.exact('Item', 'X', 'Name'))

    def test_api_version_is_pinned_and_cached(self):
        response = Mock(status_code=200, content=b'{}')
        response.json.return_value = {'version': 'v1', 'schema': 's1', 'fields': {}}
        session = Mock()
        session.get.return_value = response
        api = GameData(session)
        with patch('collector.enrich.time.sleep'):
            api.get('sheet/Item/1', fields='Name')
            api.get('sheet/Item/1', fields='Name')
            api.get('sheet/Item/2', fields='Name')
        self.assertEqual(session.get.call_count, 2)
        self.assertEqual(session.get.call_args.kwargs['params']['version'], 'v1')
        response.json.return_value = {'version': 'v2', 'schema': 's1'}
        with patch('collector.enrich.time.sleep'), self.assertRaises(RuntimeError):
            api.get('sheet/Item/3')

    def test_only_official_database_links_and_decoded_names(self):
        soup = BeautifulSoup('<a href="'+ITEM+'">x</a><a href="https://evil.test/lodestone/playguide/db/item/abc/">x</a>', 'html.parser')
        self.assertEqual(official_links(soup, URL, 'item'), [ITEM])
        soup = BeautifulSoup('<title>Eorzea Database: All Saints&amp;#39; Wake | FINAL FANTASY XIV</title>', 'html.parser')
        self.assertEqual(db_name(soup, 'item'), "All Saints' Wake")

    def test_event_enrichment_publishes_proven_facts(self):
        soup = BeautifulSoup('<meta property="og:image" content="https://lds-img.finalfantasyxiv.com/test.jpg">'
                             '<h2 class="quest-title">Quest</h2><a href="'+ITEM+'"></a>', 'html.parser')
        api = Mock(version='v1', schema='s1')
        def exact(sheet, name, fields):
            if sheet == 'Item':
                return {'row_id': 10, 'fields': {'Name': 'Prize', 'Icon': {'id': 20}}}
            return {'row_id': 71003, 'fields': {'Name': 'Quest', 'Icon': {'id': 30},
                    'Reward@as(raw)': [10], 'ItemCountReward': [1]}}
        api.exact.side_effect = exact
        api.get.return_value = {'results': []}
        event = {'id': 'abc', 'url': URL, 'notes': ['Dates'], 'verified': False}
        page = lambda url: BeautifulSoup('<title>Eorzea Database: Prize | FINAL FANTASY XIV</title>', 'html.parser')
        enrich(event, soup, page, api)
        self.assertEqual(event['tracking']['rewards'][0]['earnedByQuest'], 71003)
        self.assertEqual(event['tracking']['quests'][0]['id'], 71003)
        self.assertEqual(event['imageUrl'], 'https://lds-img.finalfantasyxiv.com/test.jpg')
        self.assertFalse(event['verified'])
        self.assertNotIn('completionQuestId', event['tracking'])
        api.price.assert_not_called()

    def test_missing_game_data_leaves_partial_event(self):
        api = Mock(version=None, schema=None)
        api.exact.side_effect = RuntimeError('offline')
        event = {'id': 'abc', 'url': URL, 'notes': []}
        soup = BeautifulSoup('<a href="'+ITEM+'"></a>', 'html.parser')
        enrich(event, soup, lambda url: BeautifulSoup('<title>Eorzea Database: Prize | FINAL FANTASY XIV</title>', 'html.parser'), api)
        self.assertFalse(event['verified'])
        self.assertEqual(event['tracking']['rewards'], [])
        self.assertTrue(any('unavailable' in i for i in event['notes']))

    def test_topic_follows_single_official_special_and_keeps_stable_id(self):
        topic = 'https://na.finalfantasyxiv.com/lodestone/topics/detail/abc/'
        html = '<title>Event</title><p>October 15, 2026 at 1:00 a.m. to November 2, 2026 at 6:59 a.m. Pacific</p>'
        with patch('collector.collect.page', side_effect=lambda url, session=None:
                   BeautifulSoup('<a href="'+URL+'">Read on</a>' if url == topic else html, 'html.parser')):
            self.assertEqual(extract(topic)['id'], extract(URL)['id'])

    def test_known_active_event_survives_discovery_and_source_failures(self):
        with TemporaryDirectory() as folder:
            target = Path(folder) / 'events.json'
            old = {'id': 'known', 'title': 'Known', 'url': URL, 'startsAt': '2026-01-01T00:00:00Z',
                   'endsAt': '2099-01-01T00:00:00Z', 'tracking': {'rewards': [{'id': 10, 'price': 500}]}}
            target.write_text(json.dumps({'events': [old]}), encoding='utf-8')
            args = Namespace(output=str(target), overrides=folder+'/none', url=[URL], no_discover=True, limit=1)
            with patch('collector.collect.extract', side_effect=RuntimeError('offline')):
                run(args)
            self.assertEqual(json.loads(target.read_text(encoding='utf-8'))['events'], [old])

    def test_partial_api_failure_preserves_previous_reward_evidence(self):
        with TemporaryDirectory() as folder:
            target = Path(folder) / 'events.json'
            old = {'id': 'known', 'title': 'Known', 'url': URL, 'startsAt': '2026-01-01T00:00:00Z',
                   'endsAt': '2099-01-01T00:00:00Z', 'tracking': {'rewards': [{'id': 10, 'price': 500}]}}
            target.write_text(json.dumps({'events': [old]}), encoding='utf-8')
            fresh = dict(old, tracking={'rewards': []}, automation={'issues': ['Game data unavailable']})
            args = Namespace(output=str(target), overrides=folder+'/none', url=[URL], no_discover=True, limit=1)
            with patch('collector.collect.extract', return_value=fresh):
                run(args)
            result = json.loads(target.read_text(encoding='utf-8'))['events'][0]
            self.assertEqual(result['tracking'], old['tracking'])
            self.assertEqual(result['automation']['status'], 'stale')
            self.assertFalse(result['verified'])


class PageLayoutTests(unittest.TestCase):
    def test_short_link_only_accepts_official_destination_and_ignores_sidebar(self):
        soup = BeautifulSoup('<div class="news__detail__wrapper"><a href="https://sqex.to/abc">Special site</a></div>'
                             '<aside><a href="https://na.finalfantasyxiv.com/lodestone/special/other/">Ad</a></aside>', 'html.parser')
        response = Mock(status_code=302, headers={'Location': URL})
        session = Mock()
        session.get.return_value = response
        self.assertEqual(special_links(soup, URL, session), [URL])
        self.assertFalse(session.get.call_args.kwargs['allow_redirects'])
        response.headers = {'Location': 'https://evil.test/private'}
        self.assertEqual(special_links(soup, URL, session), [])
        self.assertEqual(session.get.call_count, 2)

    def test_timeless_special_enriches_announcement_dates(self):
        topic = 'https://na.finalfantasyxiv.com/lodestone/topics/detail/abc/'
        announcement = '<title>Event</title><a href="'+URL+'">Special</a><p>October 15, 2026 at 1:00 a.m. to November 2, 2026 at 6:59 a.m. Pacific</p>'
        special = '<title>Permanent special</title><a href="'+ITEM+'">Reward</a>'
        with patch('collector.collect.page', side_effect=lambda url, session=None:
                   BeautifulSoup(announcement if url == topic else special, 'html.parser')), patch('collector.collect.enrich') as enriched:
            enriched.side_effect = lambda event, *args: event.update(automation={'sources': []})
            event = extract(topic, game_data=Mock())
            self.assertEqual(event['url'], topic)
            self.assertEqual(event['endsAt'], '2026-11-02T14:59:00Z')
            self.assertEqual(enriched.call_args.args[1].title.get_text(), 'Permanent special')

    def test_more_than_two_timestamps_are_not_guessed(self):
        html = '<title>Event</title><p>October 15, 2026 at 1:00 a.m. to November 2, 2026 at 6:59 a.m. Pacific. November 4, 2026 at 1:00 a.m.</p>'
        with patch('collector.collect.page', return_value=BeautifulSoup(html, 'html.parser')):
            self.assertIsNone(extract(URL))
