import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class TrackingCatalogTests(unittest.TestCase):
    def test_current_events_have_catalog_art_and_consistent_quest_rewards(self):
        catalog = json.loads((ROOT / 'config/tracking.json').read_text(encoding='utf-8'))
        feed = json.loads((ROOT / 'config/events.json').read_text(encoding='utf-8'))
        self.assertEqual(len(catalog), 2)
        feed_ids = {event['id'] for event in feed['events']}
        for event in catalog:
            self.assertIn(event['eventId'], feed_ids)
            self.assertTrue((ROOT / 'assets' / event['asset']).is_file())
            quest_ids = {quest['id'] for quest in event['quests']}
            self.assertEqual(len(event['rewards']), len({r['id'] for r in event['rewards']}))
            for reward in event['rewards']:
                self.assertGreater(reward['id'], 0)
                self.assertGreater(reward['icon'], 0)
                if reward.get('earnedByQuest'):
                    self.assertIn(reward['earnedByQuest'], quest_ids)

    def test_wake_costume_is_guaranteed_by_final_quest(self):
        catalog = json.loads((ROOT / 'config/tracking.json').read_text(encoding='utf-8'))
        costume = [r for e in catalog for r in e['rewards'] if r['id'] in (52402, 52403)]
        self.assertEqual(len(costume), 2)
        self.assertTrue(all(r['earnedByQuest'] == 71004 for r in costume))
