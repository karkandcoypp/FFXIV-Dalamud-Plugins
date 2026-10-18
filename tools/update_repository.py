"""Merge a released plugin manifest into the shared Dalamud repository index."""
import argparse
import json
from pathlib import Path


def update(index_path, manifest_path, repository, tag):
    index = json.loads(Path(index_path).read_text(encoding='utf-8-sig'))
    manifest = json.loads(Path(manifest_path).read_text(encoding='utf-8-sig'))
    prefixes = {'EorzeaEventTracker': 'eet-v', 'Dalamud.FullscreenCutscenes.Community': 'uc-v'}
    name = manifest.get('InternalName')
    if name not in prefixes:
        raise ValueError('Unexpected plugin manifest')
    version = manifest['AssemblyVersion']
    tag_version = version.rsplit('.', 1)[0] if version.endswith('.0') else version
    if tag != prefixes[name] + tag_version:
        raise ValueError('Release tag does not match assembly version')
    asset = 'EorzeaEventTracker.zip' if name == 'EorzeaEventTracker' else 'UltrawideCutscenes.zip'
    link = f'https://github.com/{repository}/releases/download/{tag}/{asset}'
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
