"""Publish only pluginmaster.json, retrying a non-fast-forward without resetting the checkout."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
from update_repository import update


def execute(args, **kwargs):
    return subprocess.run(['git', *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False, **kwargs)


def publish(manifest, repository, tag, attempts=5, run=execute):
    def git(*args, data=None, env=None):
        result = run(list(args), input=data, env=env)
        if result.returncode:
            raise RuntimeError('git ' + args[0] + ': ' + result.stderr.decode(errors='replace'))
        return result.stdout
    with tempfile.TemporaryDirectory(prefix='plugin-index-') as directory:
        root = Path(directory)
        for attempt in range(attempts):
            git('fetch', 'origin', 'main')
            base = git('rev-parse', 'origin/main').decode().strip()
            previous = git('show', base + ':pluginmaster.json')
            index = root / 'pluginmaster.json'
            index.write_bytes(previous)
            update(index, manifest, repository, tag)
            candidate = index.read_bytes()
            if candidate == previous:
                print('Repository index already current (or release superseded)')
                return
            staging = root / ('git-index-' + str(attempt))
            env = dict(os.environ, GIT_INDEX_FILE=str(staging))
            git('read-tree', base, env=env)
            blob = git('hash-object', '-w', '--stdin', data=candidate).decode().strip()
            git('update-index', '--add', '--cacheinfo', '100644,' + blob + ',pluginmaster.json', env=env)
            tree = git('write-tree', env=env).decode().strip()
            commit = git('-c', 'user.name=github-actions[bot]', '-c', 'user.email=41898282+github-actions[bot]@users.noreply.github.com',
                         'commit-tree', tree, '-p', base, data=('Release ' + tag + '\n').encode()).decode().strip()
            pushed = run(['push', 'origin', commit + ':refs/heads/main'])
            if pushed.returncode == 0:
                print('Published index: ' + commit)
                return
            print('Index push did not succeed; refreshing main before retry ' + str(attempt + 2))
        raise RuntimeError('Repository index publication failed after bounded retries')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', required=True, type=Path)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--tag', required=True)
    args = parser.parse_args()
    publish(args.manifest, args.repository, args.tag)
