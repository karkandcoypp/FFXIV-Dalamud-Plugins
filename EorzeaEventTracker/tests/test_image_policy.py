import json
import unittest
from pathlib import Path
from unittest.mock import Mock, patch
from bs4 import BeautifulSoup
from collector.image_policy import is_image_url
from collector.enrich import enrich


class ImagePolicyTests(unittest.TestCase):
    def test_shared_client_image_contract(self):
        cases = json.loads((Path(__file__).parent / 'fixtures/image-urls.json').read_text(encoding='utf-8'))
        for case in cases:
            with self.subTest(case=case['name']):
                self.assertEqual(is_image_url(case['url']), case['allowed'])

    def test_enrichment_accepts_both_official_hosts_and_rejects_untrusted_art(self):
        for host in ['lds-img.finalfantasyxiv.com', 'img.finalfantasyxiv.com', 'img.finalfantasyxiv.com.evil.test']:
            with self.subTest(host=host):
                url = 'https://' + host + '/event.png'
                event = dict(id='event', url='https://na.finalfantasyxiv.com/lodestone/special/test/')
                soup = BeautifulSoup('<meta property="og:image" content="' + url + '">', 'html.parser')
                with patch('collector.enrich.enrich_locations', return_value=dict(issues=[])):
                    enrich(event, soup, Mock(), Mock())
                self.assertEqual(event.get('imageUrl'), url if is_image_url(url) else None)
