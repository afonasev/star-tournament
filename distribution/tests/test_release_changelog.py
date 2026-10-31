"""Real temporary Git histories and mocked publisher API; no publication/build."""
import copy
import io
import hashlib
import struct
import zlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).parents[1]))
import release_changelog as notes
import publish_release as publisher


class ReleaseNotes(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.repo=Path(self.temp.name)/'repo';self.repo.mkdir()
        self.git('init','-b','main');self.git('config','user.name','Fixture');self.git('config','user.email','fixture@example.invalid')
        self.base=self.commit('Initial');self.git('tag','v1.0.0')
        self.previous=dict(tag='v1.0.0',revision=self.base)
        self.first=self.commit('feat: add controller entry');self.source=self.commit('fix: keep player data')
        self.review=notes.scaffold(self.repo,self.source,'production',self.previous)
        self.review.update(pending=[],changes=[
            dict(commits=[self.first],category='added',ru='Введите имя с геймпада.',en='Enter your name with a controller.'),
            dict(commits=[self.source],category='fixed',ru='Настройки сохраняются после перезапуска.',en='Settings persist after restarting.')])
    def git(self,*args):
        return subprocess.check_output(['git','-C',str(self.repo),*args],text=True,stderr=subprocess.DEVNULL).strip()
    def commit(self,subject):
        (self.repo/'file.txt').write_text(subject)
        self.git('add','file.txt');self.git('commit','-m',subject);return self.git('rev-parse','HEAD')
    def render(self,review=None,previous='default',source=None):
        return notes.render(review or self.review,self.repo,source or self.source,'production',self.previous if previous=='default' else previous)
    def add_image(self):
        def chunk(kind, payload):
            return struct.pack('>I',len(payload))+kind+payload+struct.pack('>I',zlib.crc32(kind+payload)&0xffffffff)
        data=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',2,1,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(b'\0\xff\0\0\0\xff\0'))+chunk(b'IEND',b'')
        image=Path(self.temp.name)/'frame.png';image.write_bytes(data)
        self.review['changes'][0]['images']=[dict(path='frame.png',sha256=hashlib.sha256(data).hexdigest(),ru='Арена после обновления.',en='Updated arena.')]
        return data
    def test_images_follow_their_own_bullet_in_both_languages(self):
        self.add_image()
        body=notes.render(self.review,self.repo,self.source,'production',self.previous,version='2.0.0')
        name='history-'+self.review['changes'][0]['images'][0]['sha256']+'.png'
        self.assertIn('- Введите имя с геймпада.\n\n  ![Арена после обновления.]',body)
        self.assertEqual(body.count('/releases/download/v2.0.0/'+name),2)
        self.assertEqual(body.count('\n- '),4)
        with self.assertRaisesRegex(ValueError,'version required'):self.render()
    def test_paired_caption_and_safe_relative_path_required(self):
        self.add_image()
        original=copy.deepcopy(self.review)
        for caption in ['', 'broken]caption', 'caption\nnew paragraph']:
            self.review=copy.deepcopy(original);self.review['changes'][0]['images'][0]['en']=caption
            with self.assertRaises(ValueError):notes.render(self.review,self.repo,self.source,'production',self.previous,version='2.0.0')
        import release_media
        for path in ['../frame.png','/frame.png']:
            self.review=copy.deepcopy(original);self.review['changes'][0]['images'][0]['path']=path
            with self.assertRaises(ValueError):release_media.prepare(self.review,Path(self.temp.name),'2.0.0',Path(self.temp.name)/'out')
    def test_media_preparation_and_changed_bytes_fail_before_publish(self):
        data=self.add_image();path=Path(self.temp.name)/'review.json';path.write_text(json.dumps(self.review));out=Path(self.temp.name)/'out'
        with patch.object(publisher,'changelog_baseline',return_value=self.previous):
            body=publisher.prepare_changelog(path,self.repo,self.source,'2.0.0','production','fixture',out)
            identity=json.loads((out/'release-notes.identity.json').read_text())
            self.assertEqual((out/'history-media'/identity['media'][0]['name']).read_bytes(),data)
            self.assertEqual(publisher.changelog_assets(out)[0]['sha256'],hashlib.sha256(data).hexdigest())
            self.assertEqual(publisher.prepare_changelog(path,self.repo,self.source,'2.0.0','production','fixture',out),body)
            (Path(self.temp.name)/'frame.png').write_bytes(data[:-1])
            with self.assertRaises(ValueError):publisher.prepare_changelog(path,self.repo,self.source,'2.0.0','production','fixture',out)
            self.assertEqual((out/'release-notes.md').read_text(),body)
            (out/'history-media'/identity['media'][0]['name']).write_bytes(b'changed after packaging')
            with self.assertRaisesRegex(ValueError,'before upload'):publisher.changelog_assets(out)

    def test_scaffold_contains_exact_range_and_pending_evidence(self):
        draft=notes.scaffold(self.repo,self.source,'production',self.previous)
        self.assertEqual([x['commit'] for x in draft['pending']],[self.first,self.source])
        with self.assertRaisesRegex(ValueError,'unreviewed'):self.render(draft)
    def test_paired_categories_no_empty_sections_or_commit_subjects(self):
        body=self.render()
        self.assertIn('## Русский\n',body);self.assertIn('## English\n',body)
        self.assertIn('### Новое\n',body);self.assertIn('### Fixed\n',body)
        self.assertNotIn('### Improved',body);self.assertNotIn('### Улучшения',body)
        self.assertNotIn(self.first,body);self.assertNotIn('feat:',body)
        self.assertEqual(body.count('\n- '),4)
    def test_missing_translation_empty_pending_and_uncovered_fail(self):
        for mutate in [lambda r:r['changes'][0].pop('en'),lambda r:r['changes'].pop(),lambda r:r.update(pending=[dict(commit=self.first)])]:
            value=copy.deepcopy(self.review);mutate(value)
            with self.assertRaises(ValueError):self.render(value)
    def test_foreign_duplicate_and_overlapping_omissions_fail(self):
        for ref in [self.base,self.first]:
            value=copy.deepcopy(self.review);value['changes'][1]['commits']=[ref]
            with self.assertRaises(ValueError):self.render(value)
        value=copy.deepcopy(self.review);value['omitted']=[dict(commits=[self.first],reason='Packaging only')]
        with self.assertRaises(ValueError):self.render(value)
    def test_technical_omissions_explicit_and_not_rendered(self):
        value=copy.deepcopy(self.review);value['changes'].pop()
        value['omitted']=[dict(commits=[self.source],reason='Internal test harness only')]
        body=self.render(value);self.assertNotIn('Internal test',body);self.assertNotIn('### Fixed',body)
        value['omitted'][0]['reason']=''
        with self.assertRaises(ValueError):self.render(value)
    def test_wrong_source_channel_or_baseline_boundaries_fail(self):
        for key,val in [('channel','test'),('source_ref',self.first),('previous_release',None)]:
            value=copy.deepcopy(self.review);value[key]=val
            with self.assertRaises(ValueError):self.render(value)
    def test_first_release_includes_initial_history(self):
        review=notes.scaffold(self.repo,self.source,'production',None)
        self.assertEqual({r['commit'] for r in review['pending']},{self.base,self.first,self.source})
        review.update(pending=[],changes=[dict(commits=[self.base,self.first,self.source],category='added',ru='Играйте вместе на одном экране.',en='Play together on one screen.')])
        self.assertIn('Play together',self.render(review,previous=None))
    def test_merge_and_side_branch_commit_not_silently_omitted(self):
        self.git('checkout','-b','side',self.base)
        (self.repo/'side.txt').write_text('side');self.git('add','side.txt');self.git('commit','-m','side change')
        side=self.git('rev-parse','HEAD');self.git('checkout','main');self.git('merge','--no-ff','side','-m','Merge side')
        source=self.git('rev-parse','HEAD');pending=notes.scaffold(self.repo,source,'production',self.previous)['pending']
        self.assertEqual({x['commit'] for x in pending},{self.first,self.source,side,source})
        review=copy.deepcopy(self.review);review['source_ref']=source
        with self.assertRaisesRegex(ValueError,'Unreviewed'):self.render(review,source=source)
    def test_divergent_and_moved_previous_tag_rejected(self):
        self.git('tag','-f','v1.0.0',self.source)
        with self.assertRaisesRegex(ValueError,'mismatch'):self.render()
        self.git('tag','-f','v1.0.0',self.base)
        self.git('checkout','--orphan','separate');self.git('rm','-rf','.')
        outside=self.commit('unrelated');self.git('tag','v2.0.0',outside)
        with self.assertRaisesRegex(ValueError,'ancestor'):notes.history(self.repo,self.source,dict(tag='v2.0.0',revision=outside))
    def test_incomplete_shallow_git_rejected(self):
        clone=Path(self.temp.name)/'shallow'
        subprocess.run(['git','clone','--depth=1',self.repo.as_uri(),str(clone)],check=True,capture_output=True)
        with self.assertRaisesRegex(ValueError,'Complete Git'):notes.history(clone,self.source,None)
    def test_control_headers_and_raw_subjects_cannot_replace_player_notes(self):
        for description in ['','line\n## English','fix: internal subject',self.source]:
            value=copy.deepcopy(self.review);value['changes'][0]['ru']=description
            with self.assertRaises(ValueError):self.render(value)
    def test_channel_selection_semver_draft_and_future(self):
        def row(version,pre=False,draft=False):return dict(tag_name='v'+version,prerelease=pre,draft=draft,published_at='2026-10-16')
        rows=[row('1.0.0'),row('2.0.0-test.9',True),row('2.0.0-test.10',True),row('2.0.0-test.11',True,True),row('9.0.0',False)]
        self.assertEqual(notes.previous_release(rows,'2.0.0-test.11','test'),'v2.0.0-test.10')
        self.assertEqual(notes.previous_release(rows,'2.0.0','production'),'v1.0.0')
        self.assertIsNone(notes.previous_release(rows,'0.1.0','production'))
        self.assertLess(notes.version_key('1.0.0-test.10'),notes.version_key('1.0.0'))
        with self.assertRaises(ValueError):notes.version_key('1.0.0-test.01')
    def test_previous_tag_remote_identity_and_annotated_peel(self):
        rows=[dict(tag_name='v1.0.0',draft=False,prerelease=False,published_at='2026-10-16')]
        with patch.object(publisher,'api',side_effect=[rows,dict(object=dict(type='tag',sha='a'*40)),dict(object=dict(type='commit',sha=self.base))]):
            self.assertEqual(publisher.changelog_baseline('2.0.0','production','fixture',self.repo),self.previous)
        with patch.object(publisher,'api',side_effect=[rows,dict(object=dict(type='commit',sha=self.source))]):
            with self.assertRaisesRegex(ValueError,'differs from GitHub'):publisher.changelog_baseline('2.0.0','production','fixture',self.repo)
    def test_prepare_preserves_notes_identity_on_resume(self):
        path=Path(self.temp.name)/'review.json';path.write_text(json.dumps(self.review));output=Path(self.temp.name)/'out'
        with patch.object(publisher,'changelog_baseline',return_value=self.previous):
            body=publisher.prepare_changelog(path,self.repo,self.source,'2.0.0','production','fixture',output)
            self.assertEqual(publisher.prepare_changelog(path,self.repo,self.source,'2.0.0','production','fixture',output),body)
            self.assertEqual((output/'release-notes.md').read_text(),body)
            self.assertEqual((output/'release-notes.review.json').read_bytes(),path.read_bytes())
            changed=copy.deepcopy(self.review);changed['changes'][0]['ru']='Изменённый текст.';path.write_text(json.dumps(changed))
            with self.assertRaisesRegex(ValueError,'identity differs'):publisher.prepare_changelog(path,self.repo,self.source,'2.0.0','production','fixture',output)
            self.assertEqual((output/'release-notes.md').read_text(),body)
    def test_cli_rejects_incomplete_review_before_packaging_or_uploads(self):
        path=Path(self.temp.name)/'review.json';broken=copy.deepcopy(self.review);broken['changes'][0].pop('en');path.write_text(json.dumps(broken))
        args=['publish_release.py','--version','2.0.0','--date','2026-10-16','--channel','production','--source-ref',self.source,'--changelog-review',str(path),'--output',str(Path(self.temp.name)/'out')]
        for option in ['windows-player','mac-player','key','dotnet','vpk']:args+=['--'+option,str(Path(self.temp.name)/option)]
        original_run=subprocess.run
        def git_only(command,**kwargs):
            self.assertEqual(command[0],'git','Invalid review must not invoke package/build subprocess')
            return original_run(command,**kwargs)
        with patch.object(sys,'argv',args),patch.object(sys,'stdin',io.StringIO('fixture')),patch.object(publisher,'ROOT',self.repo/'distribution'),patch.object(publisher,'api',return_value=dict(sha=self.source)),patch.object(publisher,'changelog_baseline',return_value=self.previous),patch.object(publisher,'verify_published_bridge',return_value={}),patch.object(publisher,'snapshot_bridge',return_value=self.repo/'snapshot'),patch.object(publisher.subprocess,'run',side_effect=git_only),patch.object(publisher,'collect') as collect,patch.object(publisher,'publish') as publish:
            with self.assertRaises(ValueError):publisher.main()
            collect.assert_not_called();publish.assert_not_called()
    def test_cli_wrong_checkout_rejected_before_api_or_packaging(self):
        path=Path(self.temp.name)/'review.json';path.write_text(json.dumps(self.review))
        args=['publish_release.py','--version','2.0.0','--date','2026-10-16','--channel','production','--source-ref',self.first,'--changelog-review',str(path),'--output',str(Path(self.temp.name)/'out')]
        for option in ['windows-player','mac-player','key','dotnet','vpk']:args+=['--'+option,str(Path(self.temp.name)/option)]
        with patch.object(sys,'argv',args),patch.object(sys,'stdin',io.StringIO('fixture')),patch.object(publisher,'ROOT',self.repo/'distribution'),patch.object(publisher,'api') as api,patch.object(publisher,'prepare_changelog') as prepare:
            with self.assertRaisesRegex(ValueError,'clean exact'):publisher.main()
            api.assert_not_called();prepare.assert_not_called()
    def test_custody_source_must_match_notes_source(self):
        output=Path(self.temp.name)/'packages';packages=[]
        for runtime in publisher.RUNTIMES:
            base=output/runtime;releases=base/'releases';releases.mkdir(parents=True)
            (releases/('latest-v2-'+runtime+'.json')).write_text('{}')
            full=releases/('game-'+runtime+'.nupkg')
            platform,arch=runtime.split('-')
            config=dict(version='2.0.0',platform=platform,architecture=arch,publicationDate='2026-10-16',updateChannel='production',appId='tech.afonasev.star-tournament.'+runtime,compatibility=None)
            with zipfile.ZipFile(full,'w') as archive: archive.writestr('lib/app/release.json',json.dumps(config))
            filename='Star-Tournament-2.0.0-Windows-x64-Setup.exe' if runtime=='win-x64' else 'Star-Tournament-2.0.0-macOS-arm64.dmg'
            setup=releases/filename;setup.write_bytes(b'installer')
            items=[publisher.identity(full),publisher.identity(setup)]
            if runtime=='osx-arm64':
                compatibility=releases/'Star-Tournament-2.0.0-macOS-arm64.pkg';compatibility.write_bytes(b'compatibility pkg')
                items.append(publisher.identity(compatibility))
            (base/'2.0.0.identity.json').write_text(json.dumps(dict(version='2.0.0',date='2026-10-16',runtime=runtime,channel='production',revision=self.first,packages=items)))
            asset=dict(FileName=full.name,Size=items[0]['size'],SHA256=items[0]['sha256'],PackageId=config['appId'])
            packages.append((dict(config,channel='production',feed={'Assets':[asset]}),asset))
        with patch.object(publisher,'verify_manifest',side_effect=packages):
            with self.assertRaisesRegex(ValueError,'changelog source'):publisher.collect(output,'2.0.0','2026-10-16','production',self.source)
        custody_path=output/publisher.RUNTIMES[0]/'2.0.0.identity.json'
        custody=json.loads(custody_path.read_text());custody.update(revision=self.source,dirty=' M file.txt');custody_path.write_text(json.dumps(custody))
        with patch.object(publisher,'verify_manifest',side_effect=packages):
            with self.assertRaisesRegex(ValueError,'Dirty package'):publisher.collect(output,'2.0.0','2026-10-16','production',self.source)
    def test_body_limit_matches_unity_utf16_reader(self):
        body="## Русский\n\n"+("😀"*20000)+"\n\n## English\n\n"+("😀"*20000)+"\n"
        self.assertLess(len(body),65536)
        with self.assertRaisesRegex(ValueError,'max65536'):notes.validate_body(body)
    def test_body_categories_must_match_between_languages(self):
        body=self.render().replace('### Fixed','### Improved')
        with self.assertRaisesRegex(ValueError,'different sets'):notes.validate_body(body)
    def test_cli_scaffold_and_render_are_offline(self):
        script=Path(notes.__file__);review=Path(self.temp.name)/'review.json';out=Path(self.temp.name)/'notes.md'
        common=[sys.executable,str(script),'--repo',str(self.repo),'--source-ref',self.source,'--channel','production','--previous-tag','v1.0.0']
        subprocess.run(common+['scaffold','--output',str(review)],check=True,capture_output=True)
        self.assertEqual(len(json.loads(review.read_text())['pending']),2)
        review.write_text(json.dumps(self.review))
        subprocess.run(common+['render','--review',str(review),'--output',str(out)],check=True,capture_output=True)
        self.assertEqual(out.read_text(),self.render())


if __name__=='__main__': unittest.main()
