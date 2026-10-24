"""Validate installable packages and temporary index metadata for every repository plugin."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import tempfile
import zipfile
from update_repository import update


def verify(archive, manifest, repository, tag, index=None):
    expected = json.loads(Path(manifest).read_text(encoding='utf-8-sig'))
    name = expected['InternalName']
    with tempfile.TemporaryDirectory() as folder:
        candidate = Path(folder) / 'pluginmaster.json'
        candidate.write_text('[]', encoding='utf-8')
        update(candidate, manifest, repository, tag)  # Validates plugin identity and tag/version agreement.
        entry = json.loads(candidate.read_text(encoding='utf-8'))[0]
    with zipfile.ZipFile(archive) as package:
        names = package.namelist()
        if len(names) != len(set(names)) or any('..' in PurePosixPath(n).parts or PurePosixPath(n).is_absolute() or '\\' in n or ':' in n for n in names):
            raise ValueError('Unsafe or duplicate archive entries')
        if any(e.file_size > 100_000_000 for e in package.infolist()) or package.testzip() is not None:
            raise ValueError('Corrupt or oversized archive')
        actual = json.loads(package.read(name + '.json'))
        for key in ('InternalName', 'AssemblyVersion', 'DalamudApiLevel'):
            if actual.get(key) != expected.get(key):
                raise ValueError('Package manifest mismatch: ' + key)
        if not actual.get('DalamudApiLevel') or not package.read(name + '.dll').startswith(b'MZ'):
            raise ValueError('Missing plugin DLL/API')
    if index:
        entries = json.loads(Path(index).read_text(encoding='utf-8-sig'))
        own = [e for e in entries if e.get('InternalName') == name]
        if len(own) != 1 or any(own[0].get(k) != entry[k] for k in ('AssemblyVersion', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')):
            raise ValueError('Package / repository index mismatch')
    print('PASS: ' + name + ' ' + expected['AssemblyVersion'] + ' ZIP, manifest, DLL and index; SHA256 ' + hashlib.sha256(Path(archive).read_bytes()).hexdigest())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--zip', required=True, type=Path)
    parser.add_argument('--manifest', required=True, type=Path)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--tag', required=True)
    parser.add_argument('--index', type=Path)
    args = parser.parse_args()
    verify(args.zip, args.manifest, args.repository, args.tag, args.index)
