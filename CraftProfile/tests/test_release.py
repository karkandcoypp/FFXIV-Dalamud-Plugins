import json, pathlib, sys, tempfile, unittest
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]/'tools'))
from update_repository import update
class ReleaseTests(unittest.TestCase):
 def test_craftprofile_entry_preserves_existing_feed(self):
  with tempfile.TemporaryDirectory(dir=pathlib.Path(__file__).parent) as folder:
   root=pathlib.Path(folder);index=root/'pluginmaster.json';manifest=root/'CraftProfile.json'
   previous=[{'InternalName':'BagSense','AssemblyVersion':'0.8.0.0'},{'InternalName':'EorzeaCompass','AssemblyVersion':'0.5.0.0'}]
   index.write_text(json.dumps(previous));manifest.write_text(json.dumps({'InternalName':'CraftProfile','AssemblyVersion':'0.1.0.0','DalamudApiLevel':15}))
   for _ in range(2):update(index,manifest,'owner/repo','cp-v0.1.0')
   data=json.loads(index.read_text());self.assertEqual(data[:2],previous);self.assertEqual(len(data),3)
   self.assertTrue(data[2]['DownloadLinkInstall'].endswith('/cp-v0.1.0/CraftProfile.zip'))
   with self.assertRaises(ValueError):update(index,manifest,'owner/repo','bs-v0.1.0')
 def test_no_unsafe_automation_or_network_in_plugin(self):
  src=pathlib.Path(__file__).resolve().parents[1]/'CraftProfile'
  text='\n'.join(p.read_text(encoding='utf-8') for p in src.glob('*.cs'))
  for forbidden in ['UseAction(', 'EquipGearset(', 'HttpClient', 'Process.Start', 'SendInput', 'ReadProcessMemory']:
   self.assertNotIn(forbidden,text)
