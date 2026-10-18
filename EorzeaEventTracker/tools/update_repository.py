"""Merge a released EET manifest into a custom Dalamud repository index."""
import argparse
import json
from pathlib import Path


def update(index_path, manifest_path, repository, tag):
    index = json.loads(Path(index_path).read_text(encoding='utf-8-sig'))
    manifest = json.loads(Path(manifest_path).read_text(encoding='utf-8-sig'))
    if manifest.get('InternalName') != 'EorzeaEventTracker':
        raise ValueError('Unexpected plugin manifest')
    if tag != 'eet-v' + manifest['AssemblyVersion'].removesuffix('.0'):
        raise ValueError('Release tag does not match assembly version')
    link = f'https://github.com/{repository}/releases/download/{tag}/EorzeaEventTracker.zip'
    manifest.update(RepoUrl=f'https://github.com/{repository}',
                    DownloadLinkInstall=link, DownloadLinkUpdate=link, DownloadLinkTesting=link)
    existing = next((i for i, item in enumerate(index) if item.get('InternalName') == manifest['InternalName']), None)
    if existing is None:
        index.append(manifest)
    else:
        index[existing] = manifest
    Path(index_path).write_text(json.dumps(index, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--index', default='pluginmaster.json')
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--tag', required=True)
    args = parser.parse_args()
    update(args.index, args.manifest, args.repository, args.tag)
