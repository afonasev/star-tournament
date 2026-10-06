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

    def run_upload(self, reject=False):
        args=['upload_candidate.py','--runtime','win-x64','--version','0.1.0-test.3',
              '--releases',str(self.releases),'--installers']
        with patch.object(sys,'argv',args), patch.object(uploader.subprocess,'run') as remote, patch('builtins.print'):
            if reject:
                with self.assertRaises(SystemExit): uploader.main()
                remote.assert_not_called()
            else:
                uploader.main()
                self.assertTrue(remote.called)

    def test_exact_artifacts_can_publish(self): self.run_upload()
    def test_changed_full_package_never_contacts_vps(self):
        self.full.write_bytes(b'changed package'); self.run_upload(reject=True)
    def test_newer_unversioned_installer_cannot_publish_as_old_version(self):
        self.installer.write_bytes(b'newer installer'); self.run_upload(reject=True)
    def test_wrong_platform_manifest_never_contacts_vps(self):
        self.payload['platform']='osx';self.write_manifest();self.run_upload(reject=True)


if __name__ == '__main__': unittest.main()
