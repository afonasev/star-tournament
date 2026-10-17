"""Recording routes through the actual packager, without publishing/building apps."""
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch

sys.path.insert(0,str(Path(__file__).parents[1]))
import package as packager


class MacPackageRoute(unittest.TestCase):
    def route(self, update_only=False, minimum='12.0'):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);player=root/'Player.app'
            (player/'Contents/MacOS').mkdir(parents=True)
            (player/'Contents/MacOS/Player').write_bytes(b'not runnable fixture')
            with (player/'Contents/Info.plist').open('wb') as stream:
                plistlib.dump(dict(LSMinimumSystemVersion=minimum,CFBundleExecutable='Player'),stream)
            output=root/'output';events=[];configs=[]
            version='1.6.0-test.1'
            public=Path(packager.__file__).with_name('update-public.pem').read_bytes()
            def run(command, **kwargs):
                command=list(map(str,command))
                if 'publish' in command:
                    events.append('launcher');target=Path(command[command.index('-o')+1]);target.mkdir(parents=True,exist_ok=True)
                    (target/'StarTournamentLauncher').write_bytes(b'not executable fixture')
                elif command[0]=='ditto': shutil.copytree(command[1],command[2])
                elif command[0]=='openssl' and 'pkey' in command: Path(command[command.index('-out')+1]).write_bytes(public)
                elif 'pack' in command:
                    events.append('pack');self.assertIn('--noInst',command)
                    info=plistlib.loads(Path(command[command.index('--plist')+1]).read_bytes())
                    self.assertEqual(info['CFBundleVersion'],version);self.assertEqual(info['CFBundleShortVersionString'],'1.6.0')
                    self.assertEqual(info['LSMinimumSystemVersion'],minimum)
                    self.assertEqual(info['CFBundleExecutable'],'StarTournamentLauncher')
                    stage=Path(command[command.index('--packDir')+1]);out=Path(command[command.index('--outputDir')+1])
                    config=json.loads((stage/'release.json').read_text());configs.append(config)
                    with zipfile.ZipFile(out/'full.nupkg','w') as archive:
                        archive.writestr('lib/osx/Contents/MacOS/release.json',json.dumps(config))
                    (out/'fixture-Portable.zip').write_bytes(b'recording portable fixture')
                    asset=dict(PackageId=config['appId'],Version=version,Type='Full',FileName='full.nupkg')
                    import hashlib
                    asset.update(Size=(out/'full.nupkg').stat().st_size,SHA256=hashlib.sha256((out/'full.nupkg').read_bytes()).hexdigest())
                    (out/'releases.osx-arm64.json').write_text(json.dumps(dict(Assets=[asset])))
                elif command[0]=='openssl' and 'dgst' in command: return subprocess.CompletedProcess(command,0,stdout=b'fixture signature')
                else: self.fail('Unexpected command '+repr(command))
                return subprocess.CompletedProcess(command,0)
            def check_output(command, **kwargs):
                if command[0]=='/usr/libexec/PlistBuddy':return 'Player'
                if command[:2]==['git','rev-parse']:return 'a'*40+'\n'
                if command[:2]==['git','status']:return ''
                if command[:2]==['git','diff']:return b''
                self.fail('Unexpected read command '+repr(command))
            def dmg(archive,target,runtime,release):
                self.assertEqual((runtime,release),('osx-arm64',version));self.assertTrue(archive.is_file())
                events.append('dmg');target.write_bytes(b'recording image, not native DMG')
            args=['package.py','--runtime','osx-arm64','--player',str(player),'--version',version,'--date','2026-10-17',
                  '--channel','test','--key',str(root/'private.pem'),'--output',str(output),'--dotnet',str(root/'dotnet'),'--vpk',str(root/'vpk')]
            if update_only:args.append('--update-only')
            with patch.object(sys,'argv',args),patch.object(packager.subprocess,'run',side_effect=run),patch.object(packager.subprocess,'check_output',side_effect=check_output),patch.object(packager.macos_dmg,'from_portable',side_effect=dmg) as build,patch('builtins.print'):
                packager.main()
            self.assertEqual(events,['launcher','pack']+([] if update_only else ['dmg']))
            self.assertEqual(build.call_count,0 if update_only else 1)
            custody=json.loads((output/(version+'.identity.json')).read_text())
            names={p['name'] for p in custody['packages']}
            self.assertEqual(names,{'full.nupkg'} if update_only else {'full.nupkg','Star-Tournament-'+version+'-macOS-arm64.dmg'})
            self.assertFalse(list((output/'releases').glob('*.pkg')))
            self.assertFalse(list((output/'releases').glob('*.zip')))
            self.assertEqual(configs[0]['compatibility']['minimumOsVersion'],minimum)

    def test_mac_first_install_routes_to_dmg_and_records_exact_custody(self):self.route()
    def test_update_only_never_builds_or_keeps_first_install_artifacts(self):self.route(update_only=True)
    def test_outer_os_floor_comes_from_actual_player_not_a_hardcoded_caption(self):self.route(minimum='13.2')


if __name__=='__main__':unittest.main()
