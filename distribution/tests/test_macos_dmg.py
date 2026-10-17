import hashlib
import json
from pathlib import Path
import plistlib
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parents[1]))
import macos_dmg as dmg
from release_compatibility import PAYLOAD_FORMAT, DATA_POLICY


class MacDmg(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.app = self.root/'Original.app'
        self.base = self.app/'Contents/MacOS'
        self.base.mkdir(parents=True)
        self.config = dict(platform='osx', architecture='arm64', version='1.6.0-test.1',
                           appId='tech.afonasev.star-tournament.osx-arm64', updateChannel='test',
                           gameExecutable='game/Player.app/Contents/MacOS/Player',
                           compatibility=dict(minimumOsVersion='12.0', minimumUpdaterProtocol=2,
                                              payloadFormat=PAYLOAD_FORMAT, userDataPolicy=DATA_POLICY))
        self.info = dict(CFBundleExecutable='StarTournamentLauncher',
                         CFBundleShortVersionString='1.6.0', CFBundleVersion=self.config['version'], LSMinimumSystemVersion='12.0')
        self.write_identity()
        for relative in ['StarTournamentLauncher', 'UpdateMac', self.config['gameExecutable']]:
            path = self.base/relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b'non-executable fixture payload')
            path.chmod(0o755)
        (self.base/'update-public.pem').write_bytes(Path(dmg.__file__).with_name('update-public.pem').read_bytes())
        resources = self.app/'Contents/Resources'
        resources.mkdir()
        (resources/'sq.version').write_text('<fixture metadata/>')
        (self.base/'sq.version').symlink_to('../Resources/sq.version')
        self.output = self.root/'release.dmg'

    def write_identity(self):
        (self.base/'release.json').write_text(json.dumps(self.config))
        with (self.app/'Contents/Info.plist').open('wb') as stream:
            plistlib.dump(self.info, stream)

    def inspect(self):
        return dmg.inspect_app(self.app, 'osx-arm64', self.config['version'])

    def test_complete_launcher_bundle_and_internal_link(self):
        self.assertEqual(self.inspect(), self.config)

    def test_rejects_nested_player_or_wrong_version_architecture(self):
        for runtime, version in [('osx-x64', self.config['version']), ('osx-arm64', '1.6.0'), ('win-x64', self.config['version'])]:
            with self.subTest(runtime=runtime, version=version), self.assertRaises(ValueError):
                dmg.inspect_app(self.app, runtime, version)
        self.info['CFBundleExecutable'] = 'Player'
        self.write_identity()
        with self.assertRaises(ValueError): self.inspect()

    def test_minimum_os_and_finder_version_must_match_payload(self):
        for key, value in [('LSMinimumSystemVersion', '11.0'), ('CFBundleShortVersionString', '1.5.0')]:
            old = self.info[key]
            self.info[key] = value
            self.write_identity()
            with self.subTest(key=key), self.assertRaises(ValueError): self.inspect()
            self.info[key] = old

    def test_escaping_symlink_and_missing_update_trust_are_rejected(self):
        link = self.base/'outside'
        link.symlink_to(self.root)
        with self.assertRaises(ValueError): self.inspect()
        link.unlink()
        (self.base/'update-public.pem').write_text('foreign key')
        with self.assertRaises(ValueError): self.inspect()

    def test_player_path_cannot_traverse_to_a_different_bundle_component(self):
        self.config['gameExecutable'] = 'game/Player.app/Contents/MacOS/../../../../StarTournamentLauncher'
        self.write_identity()
        with self.assertRaises(ValueError): self.inspect()

    def test_non_runnable_or_missing_components_are_rejected(self):
        for path in [self.base/'StarTournamentLauncher', self.base/'UpdateMac', self.base/self.config['gameExecutable']]:
            path.chmod(0o644)
            with self.subTest(path=path), self.assertRaises(ValueError): self.inspect()
            path.chmod(0o755)
        (self.app/'Contents/Resources/sq.version').unlink()
        with self.assertRaises(ValueError): self.inspect()

    def test_recording_builder_gets_approved_layout_and_input_stays_intact(self):
        before = {str(p.relative_to(self.app)): hashlib.sha256(p.read_bytes()).hexdigest()
                  for p in self.app.rglob('*') if p.is_file()}
        calls = []
        def builder(filename, label, *, settings):
            calls.append((filename, label, settings))
            self.assertEqual(settings['files'], [(str(self.app.resolve()), dmg.APP_NAME)])
            self.assertEqual(settings['symlinks'], {'Applications': '/Applications'})
            self.assertEqual(settings['icon_locations'][dmg.APP_NAME], (314, 230))
            self.assertEqual(settings['icon_locations']['Applications'], (762, 230))
            self.assertEqual(Path(settings['background']).read_bytes()[:8], b'\x89PNG\r\n\x1a\n')
            self.assertEqual(settings['format'], 'UDZO')
            Path(filename).write_bytes(b'recording image, not native DMG')
        dmg.build(self.app, self.output, 'osx-arm64', self.config['version'], builder=builder, verifier=lambda *a: None)
        self.assertEqual(len(calls), 1)
        self.assertEqual(calls[0][1], 'Star Tournament · '+self.config['version'])
        self.assertEqual(before, {str(p.relative_to(self.app)): hashlib.sha256(p.read_bytes()).hexdigest()
                                 for p in self.app.rglob('*') if p.is_file()})
        self.assertFalse(list(self.root.glob('.dmg-stage-*')))

    def test_existing_output_and_failed_builder_preserve_bytes_and_cleanup(self):
        self.output.write_bytes(b'previous')
        with self.assertRaises(ValueError): dmg.build(self.app, self.output, 'osx-arm64', self.config['version'])
        self.assertEqual(self.output.read_bytes(), b'previous')
        self.output.unlink()
        def fail(*args, **kwargs): raise RuntimeError('recorded builder failure')
        with self.assertRaises(RuntimeError): dmg.build(self.app, self.output, 'osx-arm64', self.config['version'], builder=fail)
        self.assertFalse(self.output.exists())
        self.assertFalse(list(self.root.glob('.dmg-stage-*')))

    def test_output_collision_during_build_is_not_overwritten(self):
        def collision(filename, *args, **kwargs):
            Path(filename).write_bytes(b'candidate')
            self.output.write_bytes(b'concurrent owner')
        with self.assertRaises(FileExistsError):
            dmg.build(self.app, self.output, 'osx-arm64', self.config['version'], builder=collision, verifier=lambda *a: None)
        self.assertEqual(self.output.read_bytes(), b'concurrent owner')
        self.assertFalse(list(self.root.glob('.dmg-stage-*')))

    def test_outer_plist_preserves_pinned_identifier_and_real_version_contract(self):
        info = dmg.outer_plist(self.config)
        self.assertEqual(info['CFBundleIdentifier'], 'com.tech.afonasev.star-tournament.osx-arm64.tech.afonasev.star-tournament.osx-arm64')
        self.assertEqual(info['CFBundleVersion'], '1.6.0-test.1')
        self.assertEqual(info['CFBundleShortVersionString'], '1.6.0')
        self.assertEqual(info['LSMinimumSystemVersion'], '12.0')
        self.assertEqual(info['CFBundleExecutable'], 'StarTournamentLauncher')

    def test_real_ditto_zip_preserves_bundle_links_modes_and_metadata(self):
        app = self.root/dmg.APP_NAME
        self.app.rename(app)
        framework = app/'Contents/Frameworks/Test.framework'
        (framework/'Versions/A').mkdir(parents=True)
        (framework/'Versions/A/Test').write_bytes(b'framework fixture')
        (framework/'Versions/Current').symlink_to('A')
        (framework/'Test').symlink_to('Versions/Current/Test')
        subprocess.run(['/usr/bin/xattr','-w','com.afonasev.fixture','nested Player metadata',str(app/'Contents/MacOS/game/Player.app')],check=True)
        subprocess.run(['/usr/bin/xattr','-w','com.afonasev.fixture','launcher metadata',str(app/'Contents/MacOS/StarTournamentLauncher')],check=True)
        archive = self.root/'Portable.zip'
        subprocess.run(['/usr/bin/ditto','-c','-k','--rsrc','--keepParent','--sequesterRsrc',str(app),str(archive)], check=True)
        dmg.validate_portable(archive)
        def recording_builder(extracted, output, runtime, version):
            self.assertEqual(dmg.inspect_app(extracted, runtime, version), self.config)
            self.assertTrue((extracted/'Contents/MacOS/sq.version').is_symlink())
            self.assertTrue((extracted/'Contents/Frameworks/Test.framework/Versions/Current').is_symlink())
            self.assertEqual((extracted/'Contents/Frameworks/Test.framework/Test').read_bytes(), b'framework fixture')
            self.assertEqual(subprocess.check_output(['/usr/bin/xattr','-p','com.afonasev.fixture',str(extracted/'Contents/MacOS/game/Player.app')]).strip(), b'nested Player metadata')
            self.assertEqual(subprocess.check_output(['/usr/bin/xattr','-p','com.afonasev.fixture',str(extracted/'Contents/MacOS/StarTournamentLauncher')]).strip(), b'launcher metadata')
            Path(output).write_bytes(b'recording fixture, not native DMG')
            return output
        with patch.object(dmg, 'build', side_effect=recording_builder) as builder:
            dmg.from_portable(archive, self.output, 'osx-arm64', self.config['version'])
            builder.assert_called_once()
        self.assertFalse(list(self.root.glob('.dmg-unpack-*')))

    def test_zip_admission_rejects_traversal_collisions_escaping_links_and_link_children(self):
        bad_cases = [('../outside', b'bad', False),
                     (dmg.APP_NAME+'/Contents/../../outside', b'bad', False),
                     (dmg.APP_NAME+'/Contents/link', b'../../../outside', True),
                     ('Other.app/Contents/file', b'bad', False)]
        for index, (name, data, symlink) in enumerate(bad_cases):
            archive = self.root/('bad'+str(index)+'.zip')
            with zipfile.ZipFile(archive,'w') as output:
                output.writestr(dmg.APP_NAME+'/Contents/file', b'fixture')
                entry = zipfile.ZipInfo(name); entry.create_system=3
                entry.external_attr=((stat.S_IFLNK if symlink else stat.S_IFREG)|0o755)<<16
                output.writestr(entry,data)
            with self.subTest(name=name), patch.object(dmg.subprocess,'run') as extract:
                with self.assertRaises(ValueError): dmg.from_portable(archive,self.output,'osx-arm64',self.config['version'])
                extract.assert_not_called()
        for collision in ['contents/File','Contents/file/child']:
            archive = self.root/'collision.zip'
            with zipfile.ZipFile(archive,'w') as output:
                output.writestr(dmg.APP_NAME+'/Contents/file', b'fixture')
                output.writestr(dmg.APP_NAME+'/'+collision, b'bad')
            with self.assertRaises(ValueError): dmg.validate_portable(archive)

    def test_mounted_readback_checks_real_tree_and_always_detaches(self):
        (self.root/'fixture.dmg').write_bytes(b'recording image')
        for corrupt in [False, True]:
            calls=[]
            def command(args, **kwargs):
                calls.append(args)
                if 'attach' in args:
                    mount=Path(args[args.index('-mountpoint')+1])
                    shutil.copytree(self.app,mount/dmg.APP_NAME,symlinks=True)
                    (mount/'Applications').symlink_to('/Applications')
                    if corrupt:(mount/dmg.APP_NAME/'Contents/MacOS/UpdateMac').write_bytes(b'corrupt copy')
                return subprocess.CompletedProcess(args,0)
            with patch.object(dmg.subprocess,'run',side_effect=command):
                if corrupt:
                    with self.assertRaisesRegex(ValueError,'bundle differs'):dmg.verify_image(self.root/'fixture.dmg',self.app)
                else:dmg.verify_image(self.root/'fixture.dmg',self.app)
            self.assertEqual(len(calls),2)
            self.assertIn('-readonly',calls[0]);self.assertIn('-nobrowse',calls[0]);self.assertEqual(calls[1][1],'detach')
            self.assertFalse(list(self.root.glob('.dmg-readback-*')))

    def test_failed_image_readback_never_releases_output(self):
        def builder(filename, *a, **kw):Path(filename).write_bytes(b'recording image')
        def verifier(*a):raise ValueError('recorded readback failure')
        with self.assertRaisesRegex(ValueError,'readback failure'):
            dmg.build(self.app,self.output,'osx-arm64',self.config['version'],builder=builder,verifier=verifier)
        self.assertFalse(self.output.exists());self.assertFalse(list(self.root.glob('.dmg-stage-*')))

    def test_failed_detach_retains_owned_mount_path_instead_of_removing_its_tree(self):
        (self.root/'fixture.dmg').write_bytes(b'recording image')
        def command(args, **kw):
            if 'detach' in args:raise subprocess.CalledProcessError(1,args)
            mount=Path(args[args.index('-mountpoint')+1]);shutil.copytree(self.app,mount/dmg.APP_NAME,symlinks=True)
            (mount/'Applications').symlink_to('/Applications')
            return subprocess.CompletedProcess(args,0)
        with patch.object(dmg.subprocess,'run',side_effect=command):
            with self.assertRaises(subprocess.CalledProcessError):dmg.verify_image(self.root/'fixture.dmg',self.app)
        retained=list(self.root.glob('.dmg-readback-*/volume/Star Tournament.app'))
        self.assertEqual(len(retained),1)
        self.assertEqual((retained[0]/'Contents/MacOS/UpdateMac').read_bytes(),b'non-executable fixture payload')

    def test_build_cleanup_cannot_delete_image_or_mount_retained_after_failed_detach(self):
        def builder(filename,*a,**kw):Path(filename).write_bytes(b'recording image')
        def command(args,**kw):
            if 'detach' in args:raise subprocess.CalledProcessError(1,args)
            mount=Path(args[args.index('-mountpoint')+1]);shutil.copytree(self.app,mount/dmg.APP_NAME,symlinks=True)
            (mount/'Applications').symlink_to('/Applications')
            return subprocess.CompletedProcess(args,0)
        with patch.object(dmg.subprocess,'run',side_effect=command):
            with self.assertRaises(subprocess.CalledProcessError):dmg.build(self.app,self.output,'osx-arm64',self.config['version'],builder=builder)
        self.assertFalse(self.output.exists());self.assertFalse(list(self.root.glob('.dmg-stage-*')))
        retained=list(self.root.glob('.dmg-readback-*/readback.dmg'))
        self.assertEqual(len(retained),1);self.assertEqual(retained[0].read_bytes(),b'recording image')
        self.assertTrue((retained[0].parent/'volume'/dmg.APP_NAME).is_dir())


if __name__ == '__main__': unittest.main()
