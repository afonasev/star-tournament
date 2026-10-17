"""Actual PSS metadata/ZIP collector tests; no packaging, network or release mutation."""
import base64
import copy
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch
sys.path.insert(0, str(Path(__file__).parents[1]))
import publish_release as publisher
import release_compatibility as compatibility
import package as packager


class LegacyBridgePublication(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.keys = tempfile.TemporaryDirectory(prefix='st-bootstrap-keys-')
        cls.keyroot = Path(cls.keys.name)
        cls.private = cls.keyroot/'private.pem'
        subprocess.run(['openssl','genpkey','-algorithm','RSA','-pkeyopt','rsa_keygen_bits:2048','-out',str(cls.private)],check=True,capture_output=True)
        subprocess.run(['openssl','pkey','-in',str(cls.private),'-pubout','-out',str(cls.keyroot/'update-public.pem')],check=True,capture_output=True)

    @classmethod
    def tearDownClass(cls): cls.keys.cleanup()

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='st-bootstrap-publication-'); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name); self.bridge = self.root/'bridge'; self.bridge.mkdir()
        self.output = self.root/'output'
        for runtime in publisher.RUNTIMES: (self.output/runtime/'releases').mkdir(parents=True)
        self.patcher = patch.object(publisher,'ROOT',self.keyroot); self.patcher.start(); self.addCleanup(self.patcher.stop)
        self.descriptors = {}
        self.channel = 'production'; self.version = '2.0.0'; self.bridge_version = '1.1.0'
        self.make_bridge()

    def sign(self, path, descriptor):
        payload = json.dumps(descriptor).encode()
        signature = subprocess.run(['openssl','dgst','-sha256','-sign',str(self.private),'-sigopt','rsa_padding_mode:pss','-sigopt','rsa_pss_saltlen:32'],input=payload,capture_output=True,check=True).stdout
        path.write_text(json.dumps(dict(payload=base64.b64encode(payload).decode(),signature=base64.b64encode(signature).decode()))+'\n')

    def make_bridge(self):
        for runtime in publisher.RUNTIMES:
            platform, arch = runtime.split('-')
            config = dict(appId='tech.afonasev.star-tournament.'+runtime,version=self.bridge_version,platform=platform,architecture=arch,publicationDate='2026-10-17',updateChannel=self.channel,
                compatibility=dict(minimumOsVersion='10.0.19043' if platform=='win' else '12.0',minimumUpdaterProtocol=2,payloadFormat=compatibility.PAYLOAD_FORMAT,userDataPolicy=compatibility.DATA_POLICY))
            asset = dict(PackageId=config['appId'],Version=self.bridge_version,Type='Full',FileName=runtime+'-full.nupkg',Size=1,SHA256='a'*64)
            modern = compatibility.manifest(config,asset,'2026-10-17T00:00:00Z'); modern['legacyBootstrap'] = dict(publisher.BOOTSTRAP_CONTRACT)
            legacy = {k:v for k,v in modern.items() if k not in ('compatibility','legacyBootstrap')}; legacy['schema'] = 1
            self.descriptors[runtime] = [modern, legacy]
            self.write_pair(runtime)

    def write_pair(self, runtime):
        modern, legacy = self.descriptors[runtime]
        self.sign(self.bridge/f'latest-v2-{runtime}.json', modern)
        self.sign(self.bridge/f'latest-{runtime}.json', legacy)

    def collect(self): return publisher.collect_bridge(self.output,self.version,self.channel,self.bridge)

    def assert_no_outputs(self):
        self.assertEqual(list(self.output.rglob('*.json')), [])

    def test_cli_without_published_bootstrap_stops_before_packaging_or_draft_mutation(self):
        args=['publish_release.py','--version',self.version,'--date','2026-10-17','--channel',self.channel,'--source-ref','a'*40]
        for name in ('windows-player','mac-player','output','key','dotnet','vpk','changelog-review'):
            args+=['--'+name,str(self.root/('cli-output' if name=='output' else name))]
        with patch.object(sys,'argv',args),patch.object(sys,'stdin',io.StringIO('fixture')),patch.object(publisher,'resolve',return_value='a'*40),patch.object(publisher,'git',return_value=''),patch.object(publisher,'api',return_value={'sha':'a'*40}) as api,patch.object(publisher,'prepare_changelog') as notes,patch.object(publisher,'publish') as publish,patch.object(publisher.subprocess,'run') as run:
            with self.assertRaisesRegex(ValueError,'bootstrap directory required'): publisher.main()
            self.assertEqual([c.args[0] for c in api.call_args_list],['GET'])
            notes.assert_not_called();publish.assert_not_called();run.assert_not_called()
            self.assertFalse((self.root/'cli-output').exists())

    def test_packager_outputs_modern_metadata_without_old_reader_names(self):
        directory=self.root/'metadata';directory.mkdir()
        envelope=json.loads((self.bridge/'latest-v2-win-x64.json').read_bytes())
        packager.write_metadata(directory,'win-x64',self.version,envelope)
        self.assertEqual({p.name for p in directory.iterdir()},{self.version+'.signed.json','latest-v2-win-x64.json'})
        self.assertEqual((directory/'latest-v2-win-x64.json').read_bytes(),(self.bridge/'latest-v2-win-x64.json').read_bytes())
        packager.write_metadata(directory,'win-x64',self.version,envelope)
        with self.assertRaises(ValueError): packager.write_metadata(directory,'win-x64',self.version,dict(envelope,signature='changed'))
        self.assertEqual((directory/'latest-v2-win-x64.json').read_bytes(),(self.bridge/'latest-v2-win-x64.json').read_bytes())

    def test_production_frozen_aliases_are_exact_bytes_and_resume_is_identical(self):
        result = self.collect()
        self.assertEqual(len(result),4)
        for runtime in publisher.RUNTIMES:
            target = self.output/runtime/'releases'
            self.assertEqual((target/f'latest-{runtime}.json').read_bytes(),(self.bridge/f'latest-{runtime}.json').read_bytes())
            self.assertEqual((target/f'legacy-bootstrap-{runtime}.json').read_bytes(),(self.bridge/f'latest-v2-{runtime}.json').read_bytes())
        self.assertEqual(result,self.collect())

    def test_test_channel_has_no_legacy_names_to_confuse_old_selector(self):
        self.channel='test'; self.bridge_version='1.1.0-test.9'; self.version='1.1.0-test.10'; self.make_bridge()
        result=self.collect()
        self.assertEqual(len(result),2)
        self.assertFalse(any(x['name'].startswith('latest-') for x in result))

    def test_missing_bridge_blocks_publication(self):
        with self.assertRaisesRegex(ValueError,'required'): publisher.collect_bridge(self.output,self.version,self.channel,None)
        self.assert_no_outputs()

    def test_historical_schema1_alone_is_not_a_bootstrap(self):
        for runtime in publisher.RUNTIMES: (self.bridge/f'latest-v2-{runtime}.json').unlink()
        with self.assertRaises(ValueError): self.collect()
        self.assert_no_outputs()

    def test_modern_without_signed_bootstrap_declaration_is_rejected(self):
        self.descriptors['win-x64'][0].pop('legacyBootstrap'); self.write_pair('win-x64')
        with self.assertRaisesRegex(ValueError,'bootstrap contract'): self.collect()
        self.assert_no_outputs()

    def test_bool_or_extra_bootstrap_contract_not_accepted(self):
        for value in (dict(publisher.BOOTSTRAP_CONTRACT,schema=True),dict(publisher.BOOTSTRAP_CONTRACT,extra=1)):
            with self.subTest(value=value):
                self.descriptors['win-x64'][0]['legacyBootstrap']=value; self.write_pair('win-x64')
                with self.assertRaises(ValueError): self.collect()
                self.assert_no_outputs()

    def test_raised_os_floor_or_different_data_policy_not_a_legacy_bridge(self):
        original=copy.deepcopy(self.descriptors['win-x64'][0])
        for field,value in [('minimumOsVersion','10.0.22000'),('userDataPolicy','destructive')]:
            with self.subTest(field=field):
                self.descriptors['win-x64'][0]=copy.deepcopy(original)
                self.descriptors['win-x64'][0]['compatibility'][field]=value; self.write_pair('win-x64')
                with self.assertRaises(ValueError): self.collect()
                self.assert_no_outputs()

    def test_legacy_override_and_same_version_different_package_are_rejected(self):
        original=copy.deepcopy(self.descriptors['win-x64'][1])
        for change in ('compatibility','package','date'):
            with self.subTest(change=change):
                legacy=copy.deepcopy(original)
                if change=='compatibility': legacy['compatibility']=self.descriptors['win-x64'][0]['compatibility']
                elif change=='package': legacy['feed']['Assets'][0]['SHA256']='b'*64
                else: legacy['publicationDate']='2026-10-16'
                self.descriptors['win-x64'][1]=legacy; self.write_pair('win-x64')
                with self.assertRaises(ValueError): self.collect()
                self.assert_no_outputs()

    def test_wrong_channel_or_same_or_future_bridge_is_rejected(self):
        for version in ('1.1.0','1.0.0','2.0.0-test.1'):
            with self.subTest(target=version),self.assertRaises(ValueError): publisher.collect_bridge(self.output,version,self.channel,self.bridge)
        self.assert_no_outputs()

    def test_platform_pair_disagreement_writes_nothing(self):
        for descriptor in self.descriptors['osx-arm64']: descriptor['publicationDate']='2026-10-16'
        self.write_pair('osx-arm64')
        with self.assertRaisesRegex(ValueError,'pair must share'): self.collect()
        self.assert_no_outputs()

    def test_bad_signature_after_valid_first_platform_writes_nothing(self):
        path=self.bridge/'latest-osx-arm64.json'; envelope=json.loads(path.read_text()); envelope['signature']=base64.b64encode(b'bad').decode();path.write_text(json.dumps(envelope))
        with self.assertRaises(subprocess.CalledProcessError): self.collect()
        self.assert_no_outputs()

    def test_oversized_or_symlink_metadata_rejected(self):
        path=self.bridge/'latest-osx-arm64.json'; raw=path.read_bytes()
        path.write_bytes(b' '*65537)
        with self.assertRaises(ValueError): self.collect()
        path.unlink(); (self.bridge/'actual.json').write_bytes(raw); path.symlink_to('actual.json')
        with self.assertRaises(ValueError): self.collect()
        self.assert_no_outputs()

    def test_immutable_collision_never_overwrites_any_alias(self):
        existing=self.output/'osx-arm64/releases/latest-osx-arm64.json';existing.write_bytes(b'foreign')
        with self.assertRaisesRegex(ValueError,'immutable'): self.collect()
        self.assertEqual(existing.read_bytes(),b'foreign')
        self.assertEqual(len(list(self.output.rglob('*.json'))),1)

    def remote_bridge(self):
        assets=[]
        for runtime in publisher.RUNTIMES:
            for prefix in ('latest-v2-','latest-'):
                item=publisher.identity(self.bridge/f'{prefix}{runtime}.json')
                assets.append(dict(name=item['name'],size=item['size'],digest='sha256:'+item['sha256'],state='uploaded'))
            package=self.descriptors[runtime][0]['feed']['Assets'][0]
            assets.append(dict(name=package['FileName'],size=package['Size'],digest='sha256:'+package['SHA256'],state='uploaded'))
            installer=f'Star-Tournament-{self.bridge_version}-Windows-x64-Setup.exe' if runtime=='win-x64' else f'Star-Tournament-{self.bridge_version}-macOS-arm64.pkg'
            assets.append(dict(name=installer,size=1,digest='sha256:'+'b'*64,state='uploaded'))
        return dict(tag_name='v'+self.bridge_version,draft=False,prerelease=False,published_at='2026-10-17',assets=assets)

    def test_live_preflight_uses_only_get_then_reads_all_descriptors_packages_and_installers(self):
        with patch.object(publisher,'api',return_value=self.remote_bridge()) as api,patch.object(publisher,'readback',return_value=['verified']) as readback:
            receipt=publisher.verify_published_bridge(self.version,self.channel,self.bridge,'fixture')
            self.assertEqual(receipt['version'],self.bridge_version)
            self.assertEqual(receipt['readback'],['verified'])
            api.assert_called_once_with('GET','/releases/tags/v'+self.bridge_version,'fixture')
            self.assertEqual(len(readback.call_args.args[1]),8)
            self.assert_no_outputs()

    def test_published_dmg_bootstrap_is_read_back_without_requiring_a_pkg(self):
        remote=self.remote_bridge()
        installer=next(a for a in remote['assets'] if a['name'].endswith('.pkg'))
        installer['name']=installer['name'][:-4]+'.dmg'
        with patch.object(publisher,'api',return_value=remote),patch.object(publisher,'readback',return_value=[]) as readback:
            publisher.verify_published_bridge(self.version,self.channel,self.bridge,'fixture')
            names={a['name'] for a in readback.call_args.args[1]}
            self.assertTrue(any(n.endswith('.dmg') for n in names))
            self.assertFalse(any(n.endswith('.pkg') for n in names))

    def test_present_invalid_bridge_dmg_cannot_fall_back_to_valid_historical_pkg(self):
        remote=self.remote_bridge();pkg=next(a for a in remote['assets'] if a['name'].endswith('.pkg'))
        remote['assets'].append(dict(pkg,name=pkg['name'][:-4]+'.dmg',digest='invalid'))
        with patch.object(publisher,'api',return_value=remote),patch.object(publisher,'readback') as readback:
            with self.assertRaises(ValueError):publisher.verify_published_bridge(self.version,self.channel,self.bridge,'fixture')
            readback.assert_not_called()

    def test_readback_inputs_are_frozen_even_if_original_directory_changes(self):
        with patch.object(publisher,'api',return_value=self.remote_bridge()),patch.object(publisher,'readback',return_value=[]):
            receipt=publisher.verify_published_bridge(self.version,self.channel,self.bridge,'fixture')
        original=(self.bridge/'latest-win-x64.json').read_bytes()
        (self.bridge/'latest-win-x64.json').write_bytes(b'changed after preflight')
        frozen=publisher.snapshot_bridge(self.output,receipt)
        self.assertEqual((frozen/'latest-win-x64.json').read_bytes(),original)
        publisher.collect_bridge(self.output,self.version,self.channel,frozen)
        self.assertEqual((self.output/'win-x64/releases/latest-win-x64.json').read_bytes(),original)
        self.assertEqual(publisher.snapshot_bridge(self.output,receipt),frozen)
        changed=copy.deepcopy(receipt);changed['metadata']['latest-win-x64.json']='different'
        with self.assertRaisesRegex(ValueError,'immutable bootstrap snapshot'): publisher.snapshot_bridge(self.output,changed)
        self.assertEqual((frozen/'latest-win-x64.json').read_bytes(),original)

    def test_unpublished_or_wrong_channel_release_rejected_before_readback(self):
        for update in (dict(draft=True),dict(published_at=None),dict(prerelease=True),dict(tag_name='v0.1.5')):
            with self.subTest(update=update),patch.object(publisher,'api',return_value=dict(self.remote_bridge(),**update)),patch.object(publisher,'readback') as readback:
                with self.assertRaises(ValueError): publisher.verify_published_bridge(self.version,self.channel,self.bridge,'fixture')
                readback.assert_not_called()

    def test_changed_package_metadata_or_missing_installer_rejected(self):
        for change in ('package','metadata','installer','duplicate'):
            remote=self.remote_bridge()
            if change=='package': next(a for a in remote['assets'] if a['name'].endswith('.nupkg'))['digest']='sha256:'+'0'*64
            if change=='metadata': remote['assets'][0]['digest']='sha256:'+'0'*64
            if change=='installer': remote['assets']=[a for a in remote['assets'] if not a['name'].endswith('.pkg')]
            if change=='duplicate': remote['assets'].append(remote['assets'][0])
            with self.subTest(change=change),patch.object(publisher,'api',return_value=remote),patch.object(publisher,'readback') as readback:
                with self.assertRaises(ValueError): publisher.verify_published_bridge(self.version,self.channel,self.bridge,'fixture')
                readback.assert_not_called()

    def test_public_bytes_readback_failure_is_not_a_successful_receipt(self):
        with patch.object(publisher,'api',return_value=self.remote_bridge()),patch.object(publisher,'readback',side_effect=ValueError('bytes changed')):
            with self.assertRaisesRegex(ValueError,'bytes changed'): publisher.verify_published_bridge(self.version,self.channel,self.bridge,'fixture')

    def make_current_packages(self):
        for runtime in publisher.RUNTIMES:
            config=copy.deepcopy(self.descriptors[runtime][0]);config.pop('legacyBootstrap');config.pop('feed');config.pop('downloadUrl');config.pop('schema');config.pop('publishedAt')
            config['version']=self.version;config['appId']='tech.afonasev.star-tournament.'+runtime;config['updateChannel']=config.pop('channel')
            releases=self.output/runtime/'releases';full=releases/f'{runtime}-full.nupkg'
            with zipfile.ZipFile(full,'w') as archive: archive.writestr('lib/app/release.json',json.dumps(config))
            item=publisher.identity(full)
            asset=dict(PackageId=config['appId'],Version=self.version,Type='Full',FileName=full.name,Size=item['size'],SHA256=item['sha256'])
            self.sign(releases/f'latest-v2-{runtime}.json',compatibility.manifest(config,asset,'2026-10-17T00:00:00Z'))
            name=f'Star-Tournament-{self.version}-Windows-x64-Setup.exe' if runtime=='win-x64' else f'Star-Tournament-{self.version}-macOS-arm64.dmg'
            installer=releases/name;installer.write_bytes(b'fixture not executable')
            (self.output/runtime/f'{self.version}.identity.json').write_text(json.dumps(dict(version=self.version,date='2026-10-17',runtime=runtime,channel=self.channel,revision='a'*40,dirty='',packages=[item,publisher.identity(installer)])))

    def test_actual_zip_custody_collector_uses_modern_and_frozen_legacy_aliases(self):
        self.make_current_packages()
        assets=publisher.collect(self.output,self.version,'2026-10-17',self.channel,'a'*40,legacy_bridge=self.bridge)
        self.assertEqual(len(assets),12)
        names={a['name'] for a in assets}
        self.assertTrue({'latest-v2-win-x64.json','latest-v2-osx-arm64.json','latest-win-x64.json','latest-osx-arm64.json'}<=names)
        self.assertIn(f'Star-Tournament-{self.version}-macOS-arm64.dmg',names)

    def test_modern_collector_rejects_pkg_substitution_and_changed_dmg_custody(self):
        self.make_current_packages()
        image=self.output/'osx-arm64/releases'/f'Star-Tournament-{self.version}-macOS-arm64.dmg'
        package=image.with_suffix('.pkg');image.rename(package)
        with self.assertRaisesRegex(ValueError,'Unsafe asset input'):publisher.collect(self.output,self.version,'2026-10-17',self.channel,'a'*40,legacy_bridge=self.bridge)
        package.rename(image);image.write_bytes(b'changed after custody')
        with self.assertRaisesRegex(ValueError,'custody changed'):publisher.collect(self.output,self.version,'2026-10-17',self.channel,'a'*40,legacy_bridge=self.bridge)

    def test_actual_test_zip_collector_omits_all_old_selector_names(self):
        self.channel='test'; self.bridge_version='1.1.0-test.9'; self.version='1.1.0-test.10'; self.make_bridge(); self.make_current_packages()
        assets=publisher.collect(self.output,self.version,'2026-10-17',self.channel,'a'*40,legacy_bridge=self.bridge)
        names={a['name'] for a in assets}
        self.assertEqual(len(assets),10)
        self.assertTrue({'latest-v2-win-x64.json','latest-v2-osx-arm64.json'}<=names)
        self.assertFalse({'latest-win-x64.json','latest-osx-arm64.json'}&names)

    def test_explicit_owner_production_keeps_signed_modern_pair_without_legacy_claim(self):
        self.make_current_packages()
        assets=publisher.collect(self.output,self.version,'2026-10-17','production','a'*40,new_install_only=True)
        self.assertEqual(len(assets),8)
        self.assertFalse({'latest-win-x64.json','latest-osx-arm64.json','legacy-bootstrap-win-x64.json'} & {a['name'] for a in assets})
        full=self.output/'win-x64/releases/win-x64-full.nupkg';full.write_bytes(b'tampered')
        with self.assertRaisesRegex(ValueError,'Package changed'):
            publisher.collect(self.output,self.version,'2026-10-17','production','a'*40,new_install_only=True)

    def test_owner_mode_cannot_mix_bridge_test_channel_or_stale_alias(self):
        with self.assertRaisesRegex(ValueError,'production and no legacy bridge'):
            publisher.publication_mode('test',None,True)
        with self.assertRaisesRegex(ValueError,'production and no legacy bridge'):
            publisher.publication_mode('production',self.bridge,True)
        self.make_current_packages()
        (self.output/'win-x64/releases/latest-win-x64.json').write_bytes(b'old alias')
        with self.assertRaisesRegex(ValueError,'cannot claim or contain'):
            publisher.collect(self.output,self.version,'2026-10-17','production','a'*40,new_install_only=True)

    def test_owner_notes_require_both_languages(self):
        for body in ('', 'Установите эту версию вручную', 'Install this version manually'):
            with self.assertRaisesRegex(ValueError,'Russian/English'):
                publisher.require_new_install_notice(body)
        publisher.require_new_install_notice('Установите эту версию вручную. Install this version manually.')

    def test_collector_cannot_publish_modern_only_production_without_bridge(self):
        self.make_current_packages()
        with self.assertRaisesRegex(ValueError,'bootstrap directory required'):
            publisher.collect(self.output,self.version,'2026-10-17',self.channel,'a'*40)

if __name__=='__main__':unittest.main()
