import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class LocalizationTests(unittest.TestCase):
    def test_translation_placeholders_match_and_values_are_not_empty(self):
        entries = json.loads((ROOT / 'locales/ru.json').read_text(encoding='utf-8'))
        for source, translated in entries.items():
            with self.subTest(source=source):
                self.assertTrue(translated.strip())
                self.assertEqual(sorted(re.findall(r'\{\d+\}', source)), sorted(re.findall(r'\{\d+\}', translated)))

    def test_current_event_guides_are_translated(self):
        entries = json.loads((ROOT / 'locales/ru.json').read_text(encoding='utf-8'))
        overrides = json.loads((ROOT / 'config/overrides.json').read_text(encoding='utf-8'))
        for event in overrides.values():
            strings = [event['title'], event['description'], *event['requirements'], *event['steps'], *event['rewards'], *event['notes']]
            for text in strings:
                with self.subTest(text=text): self.assertIn(text, event.get('translations', {}).get('ru', entries))

    def test_literal_ui_translation_keys_exist(self):
        entries = json.loads((ROOT / 'locales/ru.json').read_text(encoding='utf-8'))
        window = (ROOT / 'EorzeaEventTracker/EventWindow.cs').read_text(encoding='utf-8')
        window += (ROOT / 'EorzeaEventTracker/CompactWidget.cs').read_text(encoding='utf-8')
        for text in re.findall(r'(?:\b[TF]|L\.(?:Text|Format))\("([^"\n]+)"', window):
            with self.subTest(text=text): self.assertIn(text, entries)
