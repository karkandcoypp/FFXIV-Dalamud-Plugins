import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('collector', Path(__file__).parents[1] / 'tools' / 'collect_database.py')
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)


class Quantities(unittest.TestCase):
    item = {'Singular': 'chunk of copper ore', 'Plural': 'chunks of copper ore'}

    def test_cost_items_are_inputs_not_unrelated_constants(self):
        self.assertTrue(collector.requested_instruction('COST_ITEM_ATMA_1', 47744, {47744: {}}, {}))
        self.assertTrue(collector.requested_instruction('QST_GATHERING_ITEM_MIN', 5106, {5106: {}}, {}))
        self.assertFalse(collector.requested_instruction('ACTION_TIMELINE_ITEM_DRINK', 5106, {5106: {}}, {}))
        self.assertFalse(collector.requested_instruction('LOC_ITEM1', 5106, {5106: {}}, {}))

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


class ResearchedFacts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        import json
        cls.document = json.loads((Path(__file__).parents[1] / 'data' / 'researched-uses.json').read_text(encoding='utf8'))
        cls.items = {r['ItemId']:r for r in cls.document['Research']}

    def test_identity_and_nonempty_records(self):
        self.assertGreater(len(self.items), 2400)
        self.assertEqual(len(self.items), len(self.document['Research']))
        self.assertTrue(all(r['ItemName'] and r['Uses'] for r in self.items.values()))

    def test_atma_price_is_twelve_distinct_atma_plus_weapon(self):
        options = [u for u in self.items[7851]['Uses'] if u['Kind']=='RelicExchange']
        self.assertGreater(len(options), 5)
        for use in options:
            atma = [c for c in use['Costs'] if c['Name'].startswith('Atma of ')]
            self.assertEqual(len(atma), 12)
            self.assertEqual(len({c['ItemId'] for c in atma}), 12)
            self.assertTrue(all(c['Count']==1 for c in atma))
            self.assertTrue(any('Zenith' in c['Name'] for c in use['Costs']))
            self.assertEqual(use['Npcs'], ['Jalzahn'])
            self.assertTrue(use['Repeatable'])

    def test_alexandrite_price_keeps_all_zodiac_components(self):
        for use in self.items[9509]['Uses']:
            if use['Kind']!='RelicExchange':continue
            names={c['Name'] for c in use['Costs']}
            self.assertTrue({'Book of Skylight','Zodium','Zodiac Scroll','Flawless Alexandrite'} <= names)
            self.assertTrue(any('Nexus' in n for n in names))

    def test_fragment_appraisal_is_a_pool_not_a_guaranteed_reward(self):
        use=next(u for u in self.items[30884]['Uses'] if u['Kind']=='FragmentAppraisal')
        self.assertEqual(use['Npcs'], ['Resistance Appraiser'])
        self.assertIn('Resistance rank ≥ 2',use['Requirements'])
        self.assertIn(30925,use['Results'])
        self.assertNotIn('RequiredCount',use)

    def test_disambiguated_logogram_results_resolve_to_game_ids(self):
        use=next(u for u in self.items[24007]['Uses'] if u['Kind']=='LogogramAppraisal')
        self.assertGreater(len(use['Results']), 3)
        self.assertEqual(use['Npcs'], ['Drake'])
        self.assertIn('Pyros',use['Zone'])

    def test_native_virtual_action_is_not_a_permanent_unlock(self):
        use=next(u for u in self.items[30925]['Uses'] if u['Kind']=='LostAction')
        self.assertEqual(use['Evidence'],'Game FilterGroup 39')
        self.assertNotIn('Unlocked',use)
