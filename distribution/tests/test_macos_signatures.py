import plistlib
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parents[1]))
import macos_dmg as dmg


@unittest.skipUnless(sys.platform == 'darwin', 'Native macOS codesign required')
class NativeSignature(unittest.TestCase):
    def test_sealed_bundle_passes_and_changed_resources_fail(self):
        with tempfile.TemporaryDirectory() as temporary:
            app = Path(temporary)/'Fixture.app'
            (app/'Contents/MacOS').mkdir(parents=True)
            (app/'Contents/Resources').mkdir()
            shutil.copy('/bin/echo', app/'Contents/MacOS/Fixture')
            with (app/'Contents/Info.plist').open('wb') as stream:
                plistlib.dump(dict(CFBundleExecutable='Fixture', CFBundleIdentifier='tech.afonasev.signature-fixture', CFBundlePackageType='APPL'), stream)
            resource = app/'Contents/Resources/data'
            resource.write_text('original')
            # Standalone executable signature cannot seal its surrounding bundle.
            with self.assertRaises(subprocess.CalledProcessError):
                dmg.verify_signature(app)
            subprocess.run(['/usr/bin/codesign', '--force', '--sign', '-', str(app)], check=True, capture_output=True)
            dmg.verify_signature(app)
            resource.write_text('tampered')
            with self.assertRaises(subprocess.CalledProcessError):
                dmg.verify_signature(app)


class ArtifactGate(unittest.TestCase):
    def test_missing_update_or_portable_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaises(ValueError):
                dmg.verify_packages(Path(temporary))

    def test_update_links_restore_exact_directory_target(self):
        with tempfile.TemporaryDirectory() as temporary:
            app = Path(temporary)/'Fixture.app'
            base = app/'Contents/MacOS'; base.mkdir(parents=True)
            resources = app/'Contents/Resources/payload'; resources.mkdir(parents=True)
            (resources/'game').mkdir()
            (base/'game.__symlink').write_text('../Resources/payload/game/')
            dmg.restore_update_links(app)
            import os
            self.assertEqual(os.readlink(base/'game'), '../Resources/payload/game/')
            self.assertTrue((base/'game').is_dir())

    def test_update_link_escape_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            app = Path(temporary)/'Fixture.app'; app.mkdir()
            (app/'escape.__symlink').write_text('../')
            with self.assertRaises(ValueError): dmg.restore_update_links(app)

    def test_invalid_signature_stops_before_dmg_builder(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)/'fixture.dmg'
            with patch.object(dmg, 'inspect_app', return_value={}), patch.object(dmg, 'verify_signature', side_effect=subprocess.CalledProcessError(1, 'codesign')), patch.object(dmg, 'render_background') as render:
                with self.assertRaises(subprocess.CalledProcessError):
                    dmg.build(Path(temporary)/'Fixture.app', output, 'osx-arm64', '1.0.0')
                render.assert_not_called()
                self.assertFalse(output.exists())


if __name__ == '__main__': unittest.main()
