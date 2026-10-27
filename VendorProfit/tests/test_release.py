import json, pathlib, sys, tempfile, unittest
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]/'tools'))
from update_repository import update

class ReleaseTests(unittest.TestCase):
    def test_vendorprofit_feed_preserves_existing_plugins(self):
        with tempfile.TemporaryDirectory() as folder:
            root=pathlib.Path(folder); index=root/'pluginmaster.json'; manifest=root/'VendorProfit.json'
            previous=[{'InternalName':'CraftProfile','AssemblyVersion':'0.1.0.0'}, {'InternalName':'BagSense','AssemblyVersion':'0.8.0.0'}]
            index.write_text(json.dumps(previous)); manifest.write_text(json.dumps({'InternalName':'VendorProfit','AssemblyVersion':'0.1.2.0','DalamudApiLevel':15}))
            for _ in range(2): update(index,manifest,'owner/repo','vp-v0.1.2')
            data=json.loads(index.read_text()); self.assertEqual(data[:2],previous); self.assertEqual(len(data),3)
            self.assertTrue(data[2]['DownloadLinkInstall'].endswith('/vp-v0.1.2/VendorProfit.zip'))
            with self.assertRaises(ValueError): update(index,manifest,'owner/repo','cp-v0.1.0')

    def test_no_purchase_or_node_mutation(self):
        source=pathlib.Path(__file__).resolve().parents[1]/'VendorProfit'
        text='\n'.join(p.read_text(encoding='utf8') for p in source.glob('*.cs'))
        for forbidden in ('SendPurchaseRequest', 'FireCallback(', 'SetItemHighlightedState(', 'SendInput', 'ReadProcessMemory'):
            self.assertNotIn(forbidden,text)
