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
