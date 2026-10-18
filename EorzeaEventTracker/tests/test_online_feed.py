import json
import unittest
from pathlib import Path
from argparse import Namespace
from tempfile import TemporaryDirectory
from unittest.mock import patch
from collector.collect import run

ROOT = Path(__file__).resolve().parents[1]


class OnlineFeedTests(unittest.TestCase):
    def test_collector_publishes_tracking_art_and_translations(self):
        with TemporaryDirectory() as folder:
            overrides = json.loads((ROOT / 'config/overrides.json').read_text(encoding='utf-8'))
            url, detail = next(iter(overrides.items()))
            fake_event = dict(id=detail['tracking']['eventId'], title='Event', startsAt='2026-10-15T08:00:00Z', endsAt='2026-11-02T14:59:00Z')
            with patch('collector.collect.extract', return_value=fake_event):
                run(Namespace(no_translate=True, overrides=str(ROOT / 'config/overrides.json'), url=[], no_discover=True, limit=1, output=str(Path(folder) / 'events.json')))
            event = json.loads((Path(folder) / 'events.json').read_text(encoding='utf-8'))['events'][0]
            self.assertEqual(event['tracking'], detail['tracking'])
            self.assertEqual(event['translations'], detail['translations'])
            self.assertEqual(event['imageUrl'], detail['imageUrl'])
            self.assertEqual(event.get('locations', []), detail['locations'])

    def test_current_feed_contains_complete_online_definitions(self):
        feed = json.loads((ROOT / 'config/events.json').read_text(encoding='utf-8'))
        mapped = [event for event in feed['events'] if event.get('tracking')]
        self.assertGreaterEqual(len(mapped), 2)
        self.assertGreaterEqual(sum(len(e['tracking']['rewards']) for e in mapped), 31)
        for event in mapped:
            self.assertEqual(event['id'], event['tracking']['eventId'])
            locations = {l['id'] for l in event.get('locations', [])}
            for target in event['tracking']['quests'] + event['tracking']['rewards']:
                if event.get('verified') or target.get('locationId'):
                    self.assertIn(target['locationId'], locations)
            self.assertTrue(event['imageUrl'].startswith('https://lds-img.finalfantasyxiv.com/'))
            if event.get('verified') and event.get('translations', {}).get('ru'):
                self.assertIn(event['title'], event['translations']['ru'])
