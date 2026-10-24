import json
import os
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from publish_index import publish
from update_repository import update
from verify_release import verify


class PublicationTests(unittest.TestCase):
    def git(self, cwd, *args):
        result = subprocess.run(['git', *args], cwd=cwd, capture_output=True, check=False)
        if result.returncode: raise RuntimeError(result.stderr.decode(errors='replace'))
        return result.stdout.decode().strip()

    def seed(self, root):
        remote = root / 'origin.git'
        self.git(root, 'init', '--bare', '--initial-branch=main', str(remote))
        checkout = root / 'checkout'
        self.git(root, 'clone', str(remote), str(checkout))
        (checkout/'pluginmaster.json').write_text(json.dumps([{'InternalName':'BagSense','AssemblyVersion':'0.8.0.0'},
            {'InternalName':'EorzeaEventTracker','AssemblyVersion':'0.25.2.0'}]), encoding='utf8')
        self.git(checkout, 'add', '.')
        self.git(checkout, '-c','user.name=Tests','-c','user.email=tests@example.com','commit','-m','Seed')
        self.git(checkout,'push','origin','main')
        manifest = root/'BagSense.json'
        manifest.write_text(json.dumps({'InternalName':'BagSense','AssemblyVersion':'0.9.0.0','DalamudApiLevel':15}), encoding='utf8')
        return remote, checkout, manifest

    def test_concurrent_index_update_retries_and_preserves_other_files_and_checkout(self):
        with TemporaryDirectory() as directory:
            root=Path(directory); remote, checkout, manifest=self.seed(root)
            head=self.git(checkout,'rev-parse','HEAD'); index=(checkout/'.git/index').read_bytes(); pushes=[]
            def runner(args, **kwargs):
                if args[0]=='push':
                    pushes.append(args)
                    if len(pushes)==1:
                        other=root/'competitor';self.git(root,'clone',str(remote),str(other))
                        entries=json.loads((other/'pluginmaster.json').read_text());entries.append({'InternalName':'EorzeaCompass','AssemblyVersion':'0.6.0.0'})
                        (other/'pluginmaster.json').write_text(json.dumps(entries),encoding='utf8')
                        (other/'new-data.json').write_text('{"preserve":true}',encoding='utf8')
                        self.git(other,'add','.');self.git(other,'-c','user.name=Other','-c','user.email=other@example.com','commit','-m','Concurrent release')
                        self.git(other,'push','origin','main')
                return subprocess.run(['git',*args],cwd=checkout,stdout=subprocess.PIPE,stderr=subprocess.PIPE,**kwargs)
            publish(manifest,'owner/plugins','bs-v0.9.0',run=runner)
            self.assertEqual(len(pushes),2)
            result=json.loads(self.git(remote,'show','main:pluginmaster.json'))
            self.assertEqual({e['InternalName']:e['AssemblyVersion'] for e in result},
                {'BagSense':'0.9.0.0','EorzeaEventTracker':'0.25.2.0','EorzeaCompass':'0.6.0.0'})
            self.assertEqual(self.git(remote,'show','main:new-data.json'),'{"preserve":true}')
            self.assertEqual(self.git(checkout,'rev-parse','HEAD'),head)
            self.assertEqual((checkout/'.git/index').read_bytes(),index)
            published=self.git(remote,'rev-parse','main')
            publish(manifest,'owner/plugins','bs-v0.9.0',run=runner)
            self.assertEqual(self.git(remote,'rev-parse','main'),published)

    def test_failed_pushes_are_bounded_and_do_not_publish(self):
        with TemporaryDirectory() as directory:
            root=Path(directory);remote,checkout,manifest=self.seed(root);before=self.git(remote,'rev-parse','main');pushes=[]
            def runner(args,**kwargs):
                if args[0]=='push':
                    pushes.append(args);return subprocess.CompletedProcess(args,1,b'',b'denied')
                return subprocess.run(['git',*args],cwd=checkout,stdout=subprocess.PIPE,stderr=subprocess.PIPE,**kwargs)
            with self.assertRaises(RuntimeError):publish(manifest,'owner/plugins','bs-v0.9.0',attempts=3,run=runner)
            self.assertEqual(len(pushes),3);self.assertEqual(self.git(remote,'rev-parse','main'),before)

    def test_old_release_cannot_downgrade_index_and_duplicates_are_rejected(self):
        with TemporaryDirectory() as directory:
            root=Path(directory);index=root/'pluginmaster.json';manifest=root/'BagSense.json'
            manifest.write_text(json.dumps({'InternalName':'BagSense','AssemblyVersion':'0.8.0.0'}),encoding='utf8')
            current=[{'InternalName':'BagSense','AssemblyVersion':'0.9.0.0'}];index.write_text(json.dumps(current),encoding='utf8');original=index.read_bytes()
            self.assertFalse(update(index,manifest,'owner/plugins','bs-v0.8.0'));self.assertEqual(index.read_bytes(),original)
            index.write_text(json.dumps(current*2),encoding='utf8')
            with self.assertRaises(ValueError):update(index,manifest,'owner/plugins','bs-v0.8.0')

    def test_all_four_package_identities_and_wrong_manifest_or_tag(self):
        for name,tag in [('BagSense','bs-v1.2.3'),('EorzeaEventTracker','eet-v1.2.3'),('EorzeaCompass','ec-v1.2.3'),('Dalamud.FullscreenCutscenes.Community','uc-v1.2.3')]:
            with self.subTest(plugin=name),TemporaryDirectory() as directory:
                root=Path(directory);manifest=root/'plugin.json';archive=root/'plugin.zip';index=root/'pluginmaster.json'
                doc={'InternalName':name,'AssemblyVersion':'1.2.3.0','DalamudApiLevel':15};manifest.write_text(json.dumps(doc),encoding='utf8');index.write_text('[]',encoding='utf8')
                with zipfile.ZipFile(archive,'w') as z:z.writestr(name+'.json',json.dumps(doc));z.writestr(name+'.dll',b'MZ-test')
                update(index,manifest,'owner/plugins',tag);verify(archive,manifest,'owner/plugins',tag,index)
                with self.assertRaises(ValueError):verify(archive,manifest,'owner/plugins',tag+'-wrong',index)
                doc['AssemblyVersion']='1.2.4.0';manifest.write_text(json.dumps(doc),encoding='utf8')
                with self.assertRaises(ValueError):verify(archive,manifest,'owner/plugins',tag.replace('1.2.3','1.2.4'))

    def test_unsafe_archive_entries_are_rejected(self):
        with TemporaryDirectory() as directory:
            root=Path(directory);manifest=root/'manifest.json';archive=root/'plugin.zip'
            manifest.write_text(json.dumps({'InternalName':'BagSense','AssemblyVersion':'1.0.0.0'}),encoding='utf8')
            with zipfile.ZipFile(archive,'w') as z:z.writestr('../escape','bad')
            with self.assertRaises(ValueError):verify(archive,manifest,'owner/plugins','bs-v1.0.0')


if __name__=='__main__':unittest.main()
