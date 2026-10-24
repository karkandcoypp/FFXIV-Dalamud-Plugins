"""Merge a released plugin manifest into the shared Dalamud repository index."""
import argparse
import json
import re
from pathlib import Path


def update(index_path, manifest_path, repository, tag):
    index = json.loads(Path(index_path).read_text(encoding='utf-8-sig'))
    manifest = json.loads(Path(manifest_path).read_text(encoding='utf-8-sig'))
    if not isinstance(index, list) or any(not isinstance(item, dict) or not item.get('InternalName') for item in index):
        raise ValueError('Invalid repository index')
    if len({item['InternalName'] for item in index}) != len(index):
        raise ValueError('Duplicate plugin entries')
    prefixes = {'EorzeaEventTracker': 'eet-v', 'Dalamud.FullscreenCutscenes.Community': 'uc-v',
                'EorzeaCompass': 'ec-v', 'BagSense': 'bs-v', 'CraftProfile': 'cp-v'}
    name = manifest.get('InternalName')
    if name not in prefixes:
        raise ValueError('Unexpected plugin manifest')
    version = manifest['AssemblyVersion']
    if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+', version):
        raise ValueError('Invalid assembly version')
    tag_version = version.rsplit('.', 1)[0] if version.endswith('.0') else version
    if tag != prefixes[name] + tag_version:
        raise ValueError('Release tag does not match assembly version')
    asset = {'EorzeaEventTracker': 'EorzeaEventTracker.zip',
             'Dalamud.FullscreenCutscenes.Community': 'UltrawideCutscenes.zip',
             'EorzeaCompass': 'EorzeaCompass.zip', 'BagSense': 'BagSense.zip', 'CraftProfile': 'CraftProfile.zip'}[name]
    link = f'https://github.com/{repository}/releases/download/{tag}/{asset}'
    manifest.update(RepoUrl=f'https://github.com/{repository}',
                    DownloadLinkInstall=link, DownloadLinkUpdate=link, DownloadLinkTesting=link)
    existing = next((i for i, item in enumerate(index) if item.get('InternalName') == manifest['InternalName']), None)
    if existing is None:
        index.append(manifest)
    else:
        old = index[existing].get('AssemblyVersion', '')
        if re.fullmatch(r'\d+\.\d+\.\d+\.\d+', old) and tuple(map(int, old.split('.'))) > tuple(map(int, version.split('.'))):
            print('Skipping superseded release: ' + tag)
            return False
        index[existing] = manifest
    target = Path(index_path)
    temporary = target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(index, indent=2, ensure_ascii=False) + '\n', encoding='utf-8', newline='\n')
    temporary.replace(target)
    return True


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--index', default='pluginmaster.json')
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--tag', required=True)
    args = parser.parse_args()
    update(args.index, args.manifest, args.repository, args.tag)
