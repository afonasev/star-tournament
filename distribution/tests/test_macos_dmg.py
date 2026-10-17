import hashlib
import json
from pathlib import Path
import plistlib
import sys
import tempfile
import unittest

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
                         CFBundleShortVersionString=self.config['version'], LSMinimumSystemVersion='12.0')
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
        dmg.build(self.app, self.output, 'osx-arm64', self.config['version'], builder=builder)
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
            dmg.build(self.app, self.output, 'osx-arm64', self.config['version'], builder=collision)
        self.assertEqual(self.output.read_bytes(), b'concurrent owner')
        self.assertFalse(list(self.root.glob('.dmg-stage-*')))


if __name__ == '__main__': unittest.main()
