"""Prevent wrong-version or changed artifacts from reaching the VPS."""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('uploader', Path(__file__).parents[1]/'upload_candidate.py')
uploader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(uploader)


class PublicationContract(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.releases = self.root/'releases'
        self.releases.mkdir()
        self.full = self.releases/'game-0.1.0-test.3-win-x64-full.nupkg'
        self.full.write_bytes(b'exact package')
        self.installer = self.releases/'game-Setup.exe'
        self.installer.write_bytes(b'initial installer')
        asset = dict(FileName=self.full.name, Size=self.full.stat().st_size,
                     SHA256=hashlib.sha256(self.full.read_bytes()).hexdigest())
        self.payload = dict(version='0.1.0-test.3',platform='win',architecture='x64',feed={'Assets':[asset]})
        self.write_manifest()
        identity = dict(version='0.1.0-test.3',runtime='win-x64',packages=[
            dict(name=p.name,size=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
            for p in [self.full,self.installer]])
        (self.root/'0.1.0-test.3.identity.json').write_text(json.dumps(identity))

    def write_manifest(self):
        (self.releases/'0.1.0-test.3.signed.json').write_text(json.dumps({
            'payload':base64.b64encode(json.dumps(self.payload).encode()).decode(),'signature':'fixture'}))

    def run_upload(self, reject=False, runtime='win-x64'):
        args=['upload_candidate.py','--runtime',runtime,'--version','0.1.0-test.3',
              '--releases',str(self.releases),'--installers']
        with patch.object(sys,'argv',args), patch.object(uploader.subprocess,'run') as remote, patch('builtins.print'):
            if reject:
                with self.assertRaises(SystemExit): uploader.main()
                remote.assert_not_called()
            else:
                uploader.main()
                self.assertTrue(remote.called)
            return remote.call_args_list

    def prepare_mac(self):
        self.payload.update(platform='osx',architecture='arm64')
        renamed=self.releases/'Star-Tournament-0.1.0-test.3-macOS-arm64.dmg'
        self.installer.rename(renamed);self.installer=renamed
        self.write_manifest()
        identity=dict(version='0.1.0-test.3',runtime='osx-arm64',packages=[dict(name=p.name,size=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in [self.full,self.installer]])
        (self.root/'0.1.0-test.3.identity.json').write_text(json.dumps(identity))

    def test_dmg_uses_versioned_installer_destination_after_custody_checks(self):
        self.prepare_mac();calls=self.run_upload(runtime='osx-arm64')
        self.assertTrue(any(self.installer.name in str(c) and '/installers/0.1.0-test.3' in str(c) for c in calls))

    def test_changed_dmg_never_contacts_upload_destination(self):
        self.prepare_mac();self.installer.write_bytes(b'changed');self.run_upload(reject=True,runtime='osx-arm64')

    def test_exact_artifacts_can_publish(self): self.run_upload()
    def test_changed_full_package_never_contacts_vps(self):
        self.full.write_bytes(b'changed package'); self.run_upload(reject=True)
    def test_newer_unversioned_installer_cannot_publish_as_old_version(self):
        self.installer.write_bytes(b'newer installer'); self.run_upload(reject=True)
    def test_wrong_platform_manifest_never_contacts_vps(self):
        self.payload['platform']='osx';self.write_manifest();self.run_upload(reject=True)


if __name__ == '__main__': unittest.main()
