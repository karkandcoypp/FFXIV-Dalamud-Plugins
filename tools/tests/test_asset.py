import json
import os
from pathlib import Path
import shutil
import subprocess
from tempfile import TemporaryDirectory
import unittest
import zipfile


@unittest.skipUnless(shutil.which('pwsh'), 'PowerShell integration runs on Windows release runners')
class AssetTests(unittest.TestCase):
    def scenario(self, scenario):
        with TemporaryDirectory() as directory:
            root=Path(directory); manifest=root/'plugin.json'; archive=root/'EorzeaEventTracker.zip'
            doc={'InternalName':'EorzeaEventTracker','AssemblyVersion':'1.2.3.0','DalamudApiLevel':15}
            manifest.write_text(json.dumps(doc),encoding='utf8')
            with zipfile.ZipFile(archive,'w') as z:z.writestr('EorzeaEventTracker.json',json.dumps(doc));z.writestr('EorzeaEventTracker.dll',b'MZ-test')
            remote=root/'remote.zip'
            if scenario=='bad':doc['AssemblyVersion']='9.0.0.0'
            with zipfile.ZipFile(remote,'w') as z:z.writestr('EorzeaEventTracker.json',json.dumps(doc));z.writestr('EorzeaEventTracker.dll',b'MZ-test')
            script=root/'run.ps1';log=root/'calls.jsonl'
            script.write_text('''$ErrorActionPreference='Stop'
function global:gh {
    $arguments=@($args)
    Add-Content -LiteralPath $env:MOCK_LOG -Value (ConvertTo-Json -Compress -InputObject $arguments)
    if ($arguments[1] -eq 'view') {
        if ($env:MOCK_CASE -eq 'new') { $global:LASTEXITCODE=1; return }
        '{"assets":[{"name":"EorzeaEventTracker.zip"}]}'
    }
    if ($arguments[1] -eq 'download') {
        $folder=$arguments[[Array]::IndexOf($arguments,'--dir')+1]
        Copy-Item -LiteralPath $env:MOCK_REMOTE -Destination (Join-Path $folder 'EorzeaEventTracker.zip')
    }
    $global:LASTEXITCODE=0
}
& ./tools/publish_asset.ps1 -Archive $env:MOCK_ARCHIVE -Manifest $env:MOCK_MANIFEST -Notes README.md
''',encoding='utf8')
            env=dict(os.environ,MOCK_CASE=scenario,MOCK_LOG=str(log),MOCK_REMOTE=str(remote),MOCK_ARCHIVE=str(archive),MOCK_MANIFEST=str(manifest),
                RUNNER_TEMP=str(root),RELEASE_TAG='eet-v1.2.3',RELEASE_REPO='owner/plugins')
            result=subprocess.run(['pwsh','-NoProfile','-File',str(script)],cwd=Path(__file__).resolve().parents[2],env=env,capture_output=True)
            calls=[json.loads(line) for line in log.read_text(encoding='utf-8-sig').splitlines()]
            return result.returncode,calls,result.stderr.decode(errors='replace')

    def test_new_release_requires_existing_tag(self):
        code,calls,error=self.scenario('new');self.assertEqual(code,0,error)
        create=next(c for c in calls if c[1]=='create');self.assertIn('--verify-tag',create);self.assertNotIn('--clobber',create)

    def test_rerun_verifies_existing_package_without_replacement(self):
        code,calls,error=self.scenario('existing');self.assertEqual(code,0,error)
        self.assertEqual([c[1] for c in calls],['view','download'])

    def test_contradictory_existing_release_is_not_replaced(self):
        code,calls,_=self.scenario('bad');self.assertNotEqual(code,0)
        self.assertFalse(any(c[1] in ('upload','create') for c in calls))
