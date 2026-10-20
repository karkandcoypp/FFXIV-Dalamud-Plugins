"""Validate the installable BagSense ZIP and, optionally, the public Dalamud index."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def verify(package, tag, repository, index=None, checksum=None):
    package = Path(package)
    with zipfile.ZipFile(package) as archive:
        if sorted(archive.namelist()) != ['BagSense.dll', 'BagSense.json']:
            raise ValueError('Unexpected package contents')
        if archive.testzip() is not None:
            raise ValueError('ZIP CRC check failed')
        if not archive.read('BagSense.dll').startswith(b'MZ'):
            raise ValueError('Plugin DLL is not a PE assembly')
        manifest = json.loads(archive.read('BagSense.json').decode('utf-8-sig'))
    if manifest.get('InternalName') != 'BagSense' or manifest.get('DalamudApiLevel') != 15:
        raise ValueError('Plugin identity or Dalamud API mismatch')
    version = manifest['AssemblyVersion']
    expected_tag = 'bs-v' + (version.rsplit('.', 1)[0] if version.endswith('.0') else version)
    if tag != expected_tag:
        raise ValueError('Tag does not match package version')
    if manifest.get('RepoUrl') != f'https://github.com/{repository}':
        raise ValueError('Package repository URL mismatch')
    digest = hashlib.sha256(package.read_bytes()).hexdigest()
    if checksum and Path(checksum).read_text(encoding='utf-8-sig').split()[0].lower() != digest:
        raise ValueError('Published SHA-256 does not match the package')
    if index:
        entries = json.loads(Path(index).read_text(encoding='utf-8-sig'))
        names = [entry['InternalName'] for entry in entries]
        if len(names) != len(set(names)):
            raise ValueError('Duplicate plugins in index')
        entry = next(entry for entry in entries if entry['InternalName'] == 'BagSense')
        for field in ['InternalName', 'AssemblyVersion', 'DalamudApiLevel', 'RepoUrl']:
            if entry.get(field) != manifest.get(field):
                raise ValueError(f'Index / package mismatch: {field}')
        link = f'https://github.com/{repository}/releases/download/{tag}/BagSense.zip'
        for field in ['DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting']:
            if entry.get(field) != link:
                raise ValueError(f'Invalid download URL: {field}')
    print(f'PASS: BagSense {version}, API 15, ZIP CRC, package identity and SHA-256 {digest}')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--zip', required=True)
    parser.add_argument('--tag', required=True)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--index')
    parser.add_argument('--checksum')
    args = parser.parse_args()
    verify(args.zip, args.tag, args.repository, args.index, args.checksum)
