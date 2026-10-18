import unittest
from tempfile import TemporaryDirectory
from pathlib import Path
from unittest.mock import Mock
from collector.translate import RussianTranslator, strings, keep_english_names


class EnglishNameTests(unittest.TestCase):
    def test_names_do_not_use_translation_requests(self):
        with TemporaryDirectory() as folder:
            session = Mock()
            event = {'title': 'Event', 'tracking': {'rewards': [{'name': 'Prize'}]}, 'locations': [{'npc': 'Lewena', 'zone': 'The Gold Saucer'}],
                     'translations': {'ru': {'Event': 'Событие', 'The Gold Saucer': 'Золотое блюдце'}}}
            translator = RussianTranslator(Path(folder)/'cache.json', session=session)
            translator.apply(event)
            self.assertEqual(event['translations']['ru']['Prize'], 'Prize')
            self.assertEqual(event['translations']['ru']['Lewena'], 'Lewena')
            self.assertNotIn('Prize', strings(event))
            session.get.assert_not_called()

    def test_existing_translated_names_are_replaced_in_prose(self):
        event = {'tracking': {'rewards': [{'name': 'Prize'}]}, 'locations': [{'npc': 'Lewena'}],
                 'translations': {'ru': {'Prize': 'Приз', 'Lewena': 'Левена', 'Description': 'Левена выдаёт Приз.'}}}
        keep_english_names(event)
        self.assertEqual(event['translations']['ru']['Description'], 'Lewena выдаёт Prize.')
        self.assertEqual(event['translations']['ru']['Lewena'], 'Lewena')
