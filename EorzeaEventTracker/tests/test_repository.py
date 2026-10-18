import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from tools.update_repository import update


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
