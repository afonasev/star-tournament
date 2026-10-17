import base64
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parents[1]))
import broker_upgrade as broker
import release_compatibility as compatibility
import package as packager


class BrokerProducer(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory(prefix='star-broker-producer-')
        cls.root = Path(cls.temp.name).resolve()
        cls.key = cls.root/'private.pem'
        subprocess.run(['openssl', 'genpkey', '-algorithm', 'RSA', '-pkeyopt', 'rsa_keygen_bits:2048',
                        '-out', str(cls.key)], check=True, capture_output=True)
        cls.public = cls.root/'public.pem'
        cls.public.write_bytes(subprocess.run(['openssl', 'pkey', '-in', str(cls.key), '-pubout'],
                                             check=True, capture_output=True).stdout)

    @classmethod
    def tearDownClass(cls): cls.temp.cleanup()

    def setUp(self):
        self.local = tempfile.TemporaryDirectory(dir=self.root)
        self.addCleanup(self.local.cleanup)
        self.directory = Path(self.local.name)/'broker-upgrade'; self.directory.mkdir()
        (self.directory/broker.FILES[0]).write_bytes(b'fixture broker never executed')
        (self.directory/broker.FILES[2]).write_bytes(self.public.read_bytes())
        self.config = dict(appId='tech.afonasev.star-tournament.win-x64', platform='win', architecture='x64',
                           version='1.6.0', publicationDate='2026-10-17',
                           feedBase='https://github.com/afonasev/star-tournament/releases/latest/download/',
                           gameExecutable='game/StarTournament.exe', updateChannel='production',
                           compatibility=compatibility.current('win-x64', self.root))

    def seal(self): return broker.seal(self.directory, self.config, self.key, self.public)

    def test_real_signature_exact_inventory_and_file_hashes(self):
        self.seal()
        self.assertEqual({p.name for p in self.directory.iterdir()}, set(broker.FILES)|{broker.ENVELOPE})
        envelope=json.loads((self.directory/broker.ENVELOPE).read_text())
        raw=base64.b64decode(envelope['payload']); signature=Path(self.local.name)/'signature'
        signature.write_bytes(base64.b64decode(envelope['signature']))
        subprocess.run(['openssl','dgst','-sha256','-verify',str(self.public),'-signature',str(signature),
                        '-sigopt','rsa_padding_mode:pss','-sigopt','rsa_pss_saltlen:32'],input=raw,check=True,capture_output=True)
        body=json.loads(raw)
        self.assertEqual((body['schema'],body['protocol'],body['metadataReader']),(1,2,2))
        for row in body['files']:
            data=(self.directory/row['name']).read_bytes()
            self.assertEqual((row['size'],row['sha256']),(len(data),hashlib.sha256(data).hexdigest()))

    def test_installer_copies_identical_bytes_and_preserves_embedded_candidate(self):
        frozen=self.seal(); before={p.name:p.read_bytes() for p in self.directory.iterdir()}
        destination=Path(self.local.name)/'maintenance'
        broker.install_component(self.directory,destination,frozen)
        self.assertEqual({p.name:p.read_bytes() for p in destination.iterdir()}, {k:before[k] for k in broker.FILES})
        self.assertEqual({p.name:p.read_bytes() for p in self.directory.iterdir()},before)
        with self.assertRaises(ValueError): broker.install_component(self.directory,destination,frozen)

    def test_installer_rejects_changes_since_signing(self):
        frozen=self.seal(); destination=Path(self.local.name)/'maintenance'
        for name in (*broker.FILES,broker.ENVELOPE):
            path=self.directory/name; before=path.read_bytes(); path.write_bytes(before+b'changed')
            try:
                with self.subTest(file=name),self.assertRaises(ValueError): broker.install_component(self.directory,destination,frozen)
                self.assertFalse(destination.exists())
            finally: path.write_bytes(before)

    def test_installer_checks_copied_bytes(self):
        frozen=self.seal(); destination=Path(self.local.name)/'maintenance'
        def corrupt(source,target): Path(target).write_bytes(b'corrupt copy')
        with patch.object(broker.shutil,'copyfile',side_effect=corrupt),self.assertRaises(ValueError):
            broker.install_component(self.directory,destination,frozen)

    def test_different_pinned_key_never_seals(self):
        self.public.write_bytes(b'foreign key')
        try:
            with self.assertRaises(ValueError): self.seal()
            self.assertFalse((self.directory/broker.ENVELOPE).exists())
        finally:
            self.public.write_bytes((self.directory/broker.FILES[2]).read_bytes())

    def test_unexpected_publish_dependencies_fail_closed(self):
        (self.directory/'dependency.dll').write_bytes(b'unbundled dependency')
        with self.assertRaises(ValueError): self.seal()

    def test_symlink_component_rejected(self):
        exe=self.directory/broker.FILES[0]; exe.unlink(); exe.symlink_to(self.public)
        with self.assertRaises(ValueError): self.seal()

    def test_payload_identity_and_compatibility_rejected_before_writes(self):
        original=copy.deepcopy(self.config)
        for field,value in [('version','01.6.0'),('version','1.6.0-test.1'),('version','2147483648.0.0'),('publicationDate','20261017'),
                            ('architecture','arm64'),('appId','foreign'),('feedBase','https://example.org'),
                            ('compatibility',dict(original['compatibility'],minimumUpdaterProtocol=3))]:
            self.config=copy.deepcopy(original); self.config[field]=value
            with self.subTest(field=field,value=value),self.assertRaises(ValueError): self.seal()
            self.assertFalse((self.directory/broker.FILES[1]).exists())

    def test_test_channel_uses_pinned_discovery_and_canonical_prerelease(self):
        self.config.update(version='1.6.0-test.1',updateChannel='test',feedBase='https://api.github.com/repos/afonasev/star-tournament/releases')
        self.seal()
        body=json.loads(base64.b64decode(json.loads((self.directory/broker.ENVELOPE).read_text())['payload']))
        self.assertEqual((body['channel'],body['version']),('test','1.6.0-test.1'))

    def test_sealed_candidate_cannot_be_overwritten(self):
        self.seal(); envelope=(self.directory/broker.ENVELOPE).read_bytes()
        with self.assertRaises(ValueError): self.seal()
        self.assertEqual((self.directory/broker.ENVELOPE).read_bytes(),envelope)

    def test_real_csharp_consumer_accepts_producer_and_rejects_tampering(self):
        # Required explicit integration invocation; never executes the fixture exe.
        if not os.environ.get('BROKER_CONTRACT_DLL'):
            self.skipTest('NotRun: set BROKER_CONTRACT_DLL and BROKER_DOTNET for actual C# consumer')
        dll=Path(os.environ['BROKER_CONTRACT_DLL']); dotnet=os.environ['BROKER_DOTNET']
        self.seal(); payload=Path(self.local.name)/'release.json'; payload.write_text(json.dumps(self.config))
        command=[dotnet,str(dll),'--verify-produced-broker',str(self.directory),str(payload),str(self.public)]
        result=subprocess.run(command,capture_output=True,text=True)
        self.assertEqual(result.returncode,0,result.stderr)
        for name in broker.FILES+(broker.ENVELOPE,):
            path=self.directory/name; before=path.read_bytes(); path.write_bytes(before+b'tampered')
            try:
                with self.subTest(file=name): self.assertNotEqual(subprocess.run(command,capture_output=True).returncode,0)
            finally: path.write_bytes(before)


class PackagingRoute(unittest.TestCase):
    def route(self, update_only):
        with tempfile.TemporaryDirectory(prefix='star-broker-route-') as temporary:
            root=Path(temporary); player=root/'player'; player.mkdir(); (player/'StarTournament.exe').write_bytes(b'player')
            output=root/'out'; events=[]; builds=[]
            public=(Path(packager.__file__).parent/'update-public.pem').read_bytes()
            def run(command, **kwargs):
                command=list(map(str,command))
                if 'publish' in command:
                    target=Path(command[command.index('-o')+1]); target.mkdir(parents=True,exist_ok=True)
                    maintenance=any(x.endswith('/Maintenance.csproj') for x in command)
                    (target/('StarTournamentMaintenance.exe' if maintenance else 'StarTournamentLauncher.exe')).write_bytes(b'published once')
                    if maintenance: (target/'update-public.pem').write_bytes(public)
                    events.append('maintenance' if maintenance else 'launcher'); builds.append(command)
                elif command[0]=='openssl' and 'pkey' in command:
                    Path(command[command.index('-out')+1]).write_bytes(public)
                elif 'pack' in command:
                    stage=Path(command[command.index('--packDir')+1]); out=Path(command[command.index('--outputDir')+1])
                    self.assertTrue((stage/'broker-upgrade/broker-upgrade.json').is_file())
                    events.append('pack')
                    (out/'full.nupkg').write_bytes(b'fixture package')
                    (out/'releases.win-x64.json').write_text(json.dumps({'Assets':[{'Version':'1.6.0','Type':'Full','FileName':'full.nupkg'}]}))
                    (out/'OneClickSetup.exe').write_bytes(b'unpublished')
                    with zipfile.ZipFile(out/'Portable.zip','w') as archive:
                        archive.writestr('.portable',''); archive.writestr('Update.exe','native')
                        archive.writestr('Star Tournament.exe','stub')
                elif command[0]=='python3':
                    events.append('installer'); Path(command[command.index('--output')+1]).write_bytes(b'installer')
                return subprocess.CompletedProcess(command,0,stdout=b'fixture signature')
            def seal(directory,config,key,pinned):
                events.append('seal'); (directory/'maintenance.json').write_text(json.dumps(dict(protocol=2,metadataReader=2,**config)))
                (directory/'broker-upgrade.json').write_text('{}')
                return {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in directory.iterdir()}
            argv=['package.py','--runtime','win-x64','--player',str(player),'--version','1.6.0','--date','2026-10-17',
                  '--channel','production','--key',str(root/'private'),'--output',str(output),
                  '--dotnet',str(root/'dotnet'),'--vpk',str(root/'vpk')]+(['--update-only'] if update_only else ['--nsis-host','gfe'])
            with patch.object(sys,'argv',argv), patch.object(packager.subprocess,'run',side_effect=run), \
                 patch.object(packager.subprocess,'check_output',return_value='fixture-revision\n'), \
                 patch.object(packager,'validate_windows_dpi'), patch.object(packager,'seal_broker_upgrade',side_effect=seal), \
                 patch.object(packager,'verify_payload'), patch.object(packager,'release_manifest',return_value={}):
                # git binary diff is bytes, unlike git text commands.
                def checked(command,**kw): return 'fixture-revision\n' if kw.get('text') else b'fixture diff'
                with patch.object(packager.subprocess,'check_output',side_effect=checked): packager.main()
            self.assertEqual(events[:4],['launcher','maintenance','seal','pack'])
            self.assertEqual(sum(any(x.endswith('/Maintenance.csproj') for x in c) for c in builds),1)
            if update_only:
                self.assertNotIn('installer',events)
                self.assertFalse(list((output/'releases').glob('*.exe')))
            else:
                self.assertEqual(events[-1],'installer')
                candidate=output/'payload-1.6.0/broker-upgrade'; installed=output/'windows-layout-1.6.0/maintenance'
                for name in broker.FILES: self.assertEqual((candidate/name).read_bytes(),(installed/name).read_bytes())

    def test_update_only_embeds_before_pack_without_installer(self): self.route(True)
    def test_first_install_reuses_same_component_once(self): self.route(False)


if __name__ == '__main__': unittest.main()
