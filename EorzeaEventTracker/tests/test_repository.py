import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
import importlib.util

_spec = importlib.util.spec_from_file_location('repository_index', Path(__file__).resolve().parents[2] / 'tools/update_repository.py')
_module = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_module)
update = _module.update


class RepositoryTests(unittest.TestCase):
    def test_bagsense_release_preserves_all_plugins_and_updates_without_duplicates(self):
        with TemporaryDirectory() as folder:
            index = Path(folder) / 'pluginmaster.json'
            manifest = Path(folder) / 'BagSense.json'
            previous = [
                {'InternalName': 'EorzeaEventTracker', 'AssemblyVersion': '0.25.1.0'},
                {'InternalName': 'Dalamud.FullscreenCutscenes.Community', 'AssemblyVersion': '1.1.0.3'},
                {'InternalName': 'EorzeaCompass', 'AssemblyVersion': '0.5.0.0'},
            ]
            index.write_text(json.dumps(previous), encoding='utf-8')
            manifest.write_text(json.dumps({'InternalName': 'BagSense', 'AssemblyVersion': '0.1.0.0'}), encoding='utf-8')
            for _ in range(2):
                update(index, manifest, 'owner/plugins', 'bs-v0.1.0')
            result = json.loads(index.read_text(encoding='utf-8'))
            self.assertEqual(result[:3], previous)
            self.assertEqual(len(result), 4)
            expected = 'https://github.com/owner/plugins/releases/download/bs-v0.1.0/BagSense.zip'
            for field in ['DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting']:
                self.assertEqual(result[3][field], expected)
            with self.assertRaises(ValueError):
                update(index, manifest, 'owner/plugins', 'bs-v0.2.0')
            self.assertEqual(json.loads(index.read_text(encoding='utf-8')), result)
            manifest.write_text(json.dumps({'InternalName': 'BagSense', 'AssemblyVersion': '0.1.1.0'}), encoding='utf-8')
            update(index, manifest, 'owner/plugins', 'bs-v0.1.1')
            result = json.loads(index.read_text(encoding='utf-8'))
            self.assertEqual(result[:3], previous)
            self.assertEqual(len(result), 4)
            self.assertEqual(result[3]['AssemblyVersion'], '0.1.1.0')
            self.assertTrue(result[3]['DownloadLinkUpdate'].endswith('/bs-v0.1.1/BagSense.zip'))

    def test_compass_release_preserves_both_plugins_and_is_idempotent(self):
        with TemporaryDirectory() as folder:
            index = Path(folder) / 'pluginmaster.json'
            manifest = Path(folder) / 'EorzeaCompass.json'
            previous = [
                {'InternalName': 'EorzeaEventTracker', 'AssemblyVersion': '0.25.1.0'},
                {'InternalName': 'Dalamud.FullscreenCutscenes.Community', 'AssemblyVersion': '1.1.0.3'},
            ]
            index.write_text(json.dumps(previous), encoding='utf-8')
            manifest.write_text(json.dumps({'InternalName': 'EorzeaCompass', 'AssemblyVersion': '0.1.0.0'}), encoding='utf-8')
            for _ in range(2):
                update(index, manifest, 'owner/plugins', 'ec-v0.1.0')
            result = json.loads(index.read_text(encoding='utf-8'))
            self.assertEqual(result[:2], previous)
            self.assertEqual(len(result), 3)
            expected = 'https://github.com/owner/plugins/releases/download/ec-v0.1.0/EorzeaCompass.zip'
            for field in ['DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting']:
                self.assertEqual(result[2][field], expected)
            with self.assertRaises(ValueError):
                update(index, manifest, 'owner/plugins', 'eet-v0.1.0')
            self.assertEqual(json.loads(index.read_text(encoding='utf-8')), result)

    def test_merge_preserves_existing_plugin_and_does_not_duplicate_tracker(self):
        with TemporaryDirectory() as folder:
            index = Path(folder) / 'pluginmaster.json'
            manifest = Path(folder) / 'EorzeaEventTracker.json'
            previous = {'InternalName': 'Dalamud.FullscreenCutscenes.Community', 'AssemblyVersion': '1.1.0.2'}
            index.write_text(json.dumps([previous]), encoding='utf-8')
            manifest.write_text(json.dumps({'InternalName': 'EorzeaEventTracker', 'AssemblyVersion': '0.1.2.0'}), encoding='utf-8')
            for _ in range(2): update(index, manifest, 'owner/repo', 'eet-v0.1.2')
            result = json.loads(index.read_text(encoding='utf-8'))
            self.assertEqual(result[0], previous)
            self.assertEqual(len(result), 2)
            self.assertTrue(result[1]['DownloadLinkInstall'].endswith('/eet-v0.1.2/EorzeaEventTracker.zip'))

    def test_ultrawide_release_preserves_tracker(self):
        with TemporaryDirectory() as folder:
            index = Path(folder) / 'pluginmaster.json'
            manifest = Path(folder) / 'cutscenes.json'
            tracker = {'InternalName': 'EorzeaEventTracker', 'AssemblyVersion': '0.3.1.0'}
            index.write_text(json.dumps([tracker]), encoding='utf-8')
            manifest.write_text(json.dumps({'InternalName': 'Dalamud.FullscreenCutscenes.Community', 'AssemblyVersion': '1.1.0.3'}), encoding='utf-8')
            update(index, manifest, 'owner/plugins', 'uc-v1.1.0.3')
            result = json.loads(index.read_text(encoding='utf-8'))
            self.assertEqual(result[0], tracker)
            self.assertTrue(result[1]['DownloadLinkInstall'].endswith('/uc-v1.1.0.3/UltrawideCutscenes.zip'))
