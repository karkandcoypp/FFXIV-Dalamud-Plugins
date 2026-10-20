import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('collector', Path(__file__).parents[1] / 'tools' / 'collect_database.py')
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)


class Quantities(unittest.TestCase):
    item = {'Singular': 'chunk of copper ore', 'Plural': 'chunks of copper ore'}

    def test_literal_journal_number(self):
        self.assertEqual(collector.quantity(self.item, ['Gather ten chunks of copper ore and deliver them.'])[:2], (10, 'any'))

    def test_compound_number(self):
        self.assertEqual(collector.quantity(self.item, ['Gather twenty-five chunks of copper ore.'])[0], 25)

    def test_hundreds(self):
        self.assertEqual(collector.quantity(self.item, ['Gather two hundred and twenty-five chunks of copper ore.'])[0], 225)

    def test_single_item_article(self):
        self.assertEqual(collector.quantity(self.item, ['Deliver a chunk of copper ore.'])[0], 1)

    def test_conflicting_counts_stay_unknown(self):
        self.assertIsNone(collector.quantity(self.item, ['Ten chunks of copper ore.', 'Twenty chunks of copper ore.'])[0])

    def test_counter_is_not_item_count(self):
        self.assertIsNone(collector.quantity(self.item, ['Deliver chunks of copper ore.'])[0])

    def test_other_material_is_not_inferred(self):
        self.assertIsNone(collector.quantity(self.item, ['Deliver ten maple logs.'])[0])

    def test_hq(self):
        self.assertEqual(collector.quantity(self.item, ['Deliver 3 high-quality chunks of copper ore.'])[:2], (3, 'hq'))

    def test_conflicting_quality_is_unknown(self):
        self.assertIsNone(collector.quantity(self.item, ['3 chunks of copper ore.', '3 high-quality chunks of copper ore.'])[0])

    def test_quality_inside_item_phrase(self):
        self.assertEqual(collector.quantity(self.item, ['Deliver ten chunks of high-quality copper ore.'])[:2], (10, 'hq'))

    def test_dialogue_requires_a_request(self):
        self.assertIsNone(collector.quantity(self.item, ['I once saw ten chunks of copper ore.'], requests_only=True)[0])
        self.assertEqual(collector.quantity(self.item, ['Please bring me ten chunks of copper ore.'], requests_only=True)[0], 10)

    def test_negated_request_is_not_required(self):
        self.assertIsNone(collector.quantity(self.item, ['I do not need ten chunks of copper ore.'], requests_only=True)[0])

    def test_item_namespaces_are_separate(self):
        self.assertFalse(collector.requested_instruction('ITEM0', 5106, {5106}, {2000001}))
        self.assertTrue(collector.requested_instruction('ITEM0', 2000001, {5106}, {2000001}))
        self.assertTrue(collector.requested_instruction('QUEST_ITEM1', 5106, {5106}, {2000001}))
        self.assertFalse(collector.requested_instruction('LOC_ITEM1', 5106, {5106}, {2000001}))


if __name__ == '__main__':
    unittest.main()
