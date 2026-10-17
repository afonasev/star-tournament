import base64
import copy
import hashlib
import json
from pathlib import Path
import plistlib
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch
sys.path.insert(0, str(Path(__file__).parents[1]))
import release_compatibility as compatibility
import publish_release as publisher


class ReleaseCompatibility(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='star-compat-python-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.player = self.root/'Player.app'
        (self.player/'Contents').mkdir(parents=True)
        self.plist('12.0')
        self.requirements = compatibility.current('osx-arm64', self.player)
        self.config = dict(appId='tech.afonasev.star-tournament.osx-arm64', version='1.1.0', platform='osx', architecture='arm64',
                           publicationDate='2026-10-17', updateChannel='production', compatibility=self.requirements)
        self.package = self.root/'fixture-1.1.0-full.nupkg'
        self.write_package()
        self.asset = dict(PackageId=self.config['appId'], Version='1.1.0', Type='Full', FileName=self.package.name,
                          Size=self.package.stat().st_size, SHA256=hashlib.sha256(self.package.read_bytes()).hexdigest())
        self.descriptor = compatibility.manifest(self.config, self.asset, '2026-10-17T00:00:00Z')

    def plist(self, minimum):
        (self.player/'Contents/Info.plist').write_bytes(plistlib.dumps({'LSMinimumSystemVersion': minimum} if minimum is not None else {}))

    def write_package(self, config=None, duplicate=False):
        with zipfile.ZipFile(self.package, 'w') as archive:
            archive.writestr('lib/osx/release.json', json.dumps(config or self.config))
            if duplicate: archive.writestr('other/release.json', json.dumps(self.config))

    def test_producer_uses_actual_mac_floor_and_emits_schema2(self):
        self.plist('14.2.1')
        self.assertEqual(compatibility.current('osx-arm64', self.player)['minimumOsVersion'], '14.2.1')
        self.assertEqual(self.descriptor['schema'], 2)
        self.assertEqual(self.descriptor['compatibility'], self.config['compatibility'])
        compatibility.verify_payload(self.package, self.descriptor)

    def test_windows_floor_and_reader_are_explicit(self):
        result = compatibility.current('win-x64', self.root)
        self.assertEqual(result['minimumOsVersion'], '10.0.19043')
        self.assertEqual(result['minimumUpdaterProtocol'], 2)

    def test_cross_language_fixture_is_actual_producer_output(self):
        fixture = json.loads((Path(__file__).parent/'fixtures/release-compatibility.json').read_text())
        config = dict(appId='tech.test', platform='win', architecture='x64', version='1.1.0', publicationDate='2026-10-17',
                      updateChannel='production', compatibility=compatibility.current('win-x64', self.root))
        self.assertEqual(fixture, compatibility.manifest(config, fixture['feed']['Assets'][0], '2026-10-17T00:00:00Z'))

    def test_invalid_or_missing_mac_floor_is_not_silently_defaulted(self):
        for value in (None, '11.7', '012.0', '12', '12.0.0.1'):
            with self.subTest(value=value):
                self.plist(value)
                with self.assertRaises(ValueError): compatibility.current('osx-arm64', self.player)

    def test_unsupported_contract_never_emitted(self):
        for field, value in (('minimumUpdaterProtocol', 3), ('minimumUpdaterProtocol', True), ('payloadFormat', 'future'), ('userDataPolicy', 'destructive'), ('minimumOsVersion', '11.0')):
            with self.subTest(field=field, value=value):
                changed = dict(self.requirements, **{field: value})
                with self.assertRaises(ValueError): compatibility.manifest(dict(self.config, compatibility=changed), self.asset, 'now')

    def test_missing_or_extra_requirements_are_rejected(self):
        for requirements in (None, {}, dict(self.requirements, unknown=1)):
            with self.subTest(requirements=requirements), self.assertRaises(ValueError): compatibility.validate(requirements, 'osx')

    def test_payload_mismatch_is_rejected_even_with_signed_contract(self):
        for field, value in (('version', '9.0.0'), ('appId', 'foreign'), ('updateChannel', 'test'), ('architecture', 'x64'), ('publicationDate', '2026-10-01'), ('compatibility', dict(self.requirements, minimumOsVersion='16.0'))):
            with self.subTest(field=field):
                self.write_package(dict(self.config, **{field: value}))
                with self.assertRaises(ValueError): compatibility.verify_payload(self.package, self.descriptor)

    def test_payload_config_must_be_unique_and_bounded(self):
        self.write_package(duplicate=True)
        with self.assertRaises(ValueError): compatibility.verify_payload(self.package, self.descriptor)
        with zipfile.ZipFile(self.package, 'w') as archive: archive.writestr('lib/osx/release.json', ' '*65537)
        with self.assertRaises(ValueError): compatibility.verify_payload(self.package, self.descriptor)

    def test_publisher_verifies_real_pss_before_schema_and_requirements(self):
        private = self.root/'private.pem'
        subprocess.run(['openssl', 'genpkey', '-algorithm', 'RSA', '-pkeyopt', 'rsa_keygen_bits:2048', '-out', str(private)], check=True, capture_output=True)
        subprocess.run(['openssl', 'pkey', '-in', str(private), '-pubout', '-out', str(self.root/'update-public.pem')], check=True, capture_output=True)
        envelope = self.root/'manifest.json'
        def sign(data):
            payload = json.dumps(data).encode()
            signature = subprocess.run(['openssl','dgst','-sha256','-sign',str(private),'-sigopt','rsa_padding_mode:pss','-sigopt','rsa_pss_saltlen:32'], input=payload, capture_output=True, check=True).stdout
            envelope.write_text(json.dumps(dict(payload=base64.b64encode(payload).decode(), signature=base64.b64encode(signature).decode())))
        with patch.object(publisher, 'ROOT', self.root):
            sign(self.descriptor)
            data, asset = publisher.verify_manifest(envelope, '1.1.0', 'osx-arm64', 'production')
            self.assertEqual(data['compatibility'], self.requirements)
            for field, value in (('schema', 1), ('compatibility', None), ('compatibility', dict(self.requirements, minimumUpdaterProtocol=3))):
                with self.subTest(field=field):
                    sign(dict(self.descriptor, **{field: value}))
                    with self.assertRaises(ValueError): publisher.verify_manifest(envelope, '1.1.0', 'osx-arm64', 'production')
            sign(self.descriptor)
            data = json.loads(envelope.read_text()); data['signature'] = base64.b64encode(b'wrong').decode(); envelope.write_text(json.dumps(data))
            with self.assertRaises(subprocess.CalledProcessError): publisher.verify_manifest(envelope, '1.1.0', 'osx-arm64', 'production')

    def test_data_contract_matches_current_namespace_generation(self):
        # This is a declaration consistency check, not execution of Unity migration logic.
        source = (Path(__file__).parents[2]/'unity/Assets/StarTournament/Runtime/Core/VersionedUserData.cs').read_text()
        self.assertIn('Profiles="player-profiles-v2.json"', source)
        self.assertIn('LabHistory="design-lab-history-v3.json"', source)
        self.assertEqual(self.requirements['userDataPolicy'], 'profiles2-lab3-prefs1')


if __name__ == '__main__': unittest.main()
