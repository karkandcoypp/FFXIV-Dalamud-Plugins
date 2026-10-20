import hashlib
import importlib.util
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('verify_bagsense', Path(__file__).resolve().parents[1] / 'tools/verify_package.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.folder = TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = Path(self.folder.name)
        self.manifest = {'InternalName': 'BagSense', 'AssemblyVersion': '0.1.0.0',
                         'DalamudApiLevel': 15, 'RepoUrl': 'https://github.com/owner/plugins'}
        self.package = self.root / 'BagSense.zip'
        self.write_package()

    def write_package(self, extra=False):
        with zipfile.ZipFile(self.package, 'w') as archive:
            archive.writestr('BagSense.dll', b'MZtest assembly placeholder')
            archive.writestr('BagSense.json', json.dumps(self.manifest))
            if extra:
                archive.writestr('../outside.txt', b'invalid')

    def test_valid_package_and_checksum(self):
        checksum = self.root / 'BagSense.zip.sha256'
        checksum.write_text(hashlib.sha256(self.package.read_bytes()).hexdigest() + '  BagSense.zip\n')
        self.assertEqual(module.verify(self.package, 'bs-v0.1.0', 'owner/plugins', checksum=checksum), self.manifest)

    def test_wrong_tag_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package, 'bs-v0.2.0', 'owner/plugins')

    def test_unexpected_archive_entry_is_rejected(self):
        self.write_package(extra=True)
        with self.assertRaises(ValueError):
            module.verify(self.package, 'bs-v0.1.0', 'owner/plugins')

    def test_mismatched_checksum_is_rejected(self):
        checksum = self.root / 'bad.sha256'
        checksum.write_text('0' * 64)
        with self.assertRaises(ValueError):
            module.verify(self.package, 'bs-v0.1.0', 'owner/plugins', checksum=checksum)

    def test_index_links_and_duplicate_entries_are_checked(self):
        entry = dict(self.manifest)
        for field in ['DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting']:
            entry[field] = 'https://github.com/owner/plugins/releases/download/bs-v0.1.0/BagSense.zip'
        index = self.root / 'pluginmaster.json'
        index.write_text(json.dumps([entry]))
        module.verify(self.package, 'bs-v0.1.0', 'owner/plugins', index=index)
        entry['DownloadLinkUpdate'] = 'https://example.invalid/old.zip'
        index.write_text(json.dumps([entry]))
        with self.assertRaises(ValueError):
            module.verify(self.package, 'bs-v0.1.0', 'owner/plugins', index=index)
        index.write_text(json.dumps([entry, entry]))
        with self.assertRaises(ValueError):
            module.verify(self.package, 'bs-v0.1.0', 'owner/plugins', index=index)
