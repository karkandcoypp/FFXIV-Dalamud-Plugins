import unittest
from datetime import datetime
from collector.collect import parse_stamp
from collector.collect import extract, run
from unittest.mock import patch, Mock
from bs4 import BeautifulSoup
from pathlib import Path
from tempfile import TemporaryDirectory
from argparse import Namespace

class DateTests(unittest.TestCase):
    def test_pdt_to_utc(self):
        self.assertEqual(parse_stamp('Thursday, August 27, 2026 at 12:00 a.m.', 'America/Los_Angeles'), '2026-08-27T07:00:00Z')

    def test_pst_to_utc(self):
        self.assertEqual(parse_stamp('Sunday, December 6, 2026 at 1:00 a.m.', 'America/Los_Angeles'), '2026-12-06T09:00:00Z')

    def test_uppercase_dotted_time(self):
        self.assertEqual(parse_stamp('August 27, 2026 at 12:00 A.M.', 'America/Los_Angeles'), '2026-08-27T07:00:00Z')

    def test_uncertain_dates_are_skipped(self):
        with patch('collector.collect.page', return_value=BeautifulSoup('<h1>Event</h1>No dates', 'html.parser')):
            self.assertIsNone(extract('https://na.finalfantasyxiv.com/lodestone/special/test'))

    def test_image_heading_uses_page_title(self):
        html = '<title>All Saints Wake | FINAL FANTASY XIV, The Lodestone</title><h1><img></h1><p>October 15, 2026 at 1:00 a.m. until November 2, 2026 at 6:59 a.m. Pacific</p>'
        with patch('collector.collect.page', return_value=BeautifulSoup(html, 'html.parser')):
            event = extract('https://na.finalfantasyxiv.com/lodestone/special/test')
            self.assertEqual(event['title'], 'All Saints Wake')
            self.assertEqual(event['endsAt'], '2026-11-02T14:59:00Z')

    def test_network_failure_preserves_feed(self):
        with TemporaryDirectory() as folder:
            output = Path(folder) / 'events.json'
            output.write_text('previous feed', encoding='utf-8')
            args = Namespace(no_translate=True, overrides=str(Path(folder) / 'none'), url=['https://na.finalfantasyxiv.com/test'], no_discover=True, limit=1, output=str(output))
            with patch('collector.collect.extract', side_effect=RuntimeError('offline')):
                with self.assertRaises(RuntimeError): run(args)
            self.assertEqual(output.read_text(encoding='utf-8'), 'previous feed')

if __name__ == '__main__':
    unittest.main()
