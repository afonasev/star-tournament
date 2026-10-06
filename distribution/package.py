#!/usr/bin/env python3
"""Package an exact Player with a self-contained launcher and authenticated feed."""
import argparse
import base64
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import zipfile


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--runtime', choices=['osx-arm64', 'osx-x64', 'win-x64'], required=True)
    p.add_argument('--player', type=Path, required=True)
    p.add_argument('--version', required=True)
    p.add_argument('--date', required=True)
    p.add_argument('--feed', help='Legacy override, only for compatibility fixtures')
    p.add_argument('--channel', choices=['test','production'], required=True)
    p.add_argument('--key', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--dotnet', type=Path, required=True)
    p.add_argument('--vpk', type=Path, required=True)
    p.add_argument('--makensis', type=Path, help='Required Windows wizard compiler')
    p.add_argument('--update-only', action='store_true', help='Produce authenticated update package without a first-install wizard')
    p.add_argument('--nsis-host', choices=['gfe'], help='Compile on the pinned VPS NSIS toolchain')
    a = p.parse_args()
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', a.version): p.error('Invalid SemVer')
    datetime.date.fromisoformat(a.date)
    expected_feed = ('https://api.github.com/repos/afonasev/star-tournament/releases' if a.channel == 'test' else 'https://github.com/afonasev/star-tournament/releases/latest/download/')
    if a.feed and a.feed != expected_feed: p.error('Pinned GitHub feed required')
    a.feed = expected_feed
    if ('-' in a.version) != (a.channel == 'test'): p.error('Version must match release channel')
    if a.runtime == 'win-x64' and not a.update_only and not a.nsis_host and (a.makensis is None or not a.makensis.is_file()):
        p.error('Windows packages require --makensis for the installation wizard')
    a.output = a.output.resolve(); a.player = a.player.resolve()
    stage = a.output / ('payload-' + a.version)
    if stage.exists(): p.error('Payload already exists; use an immutable new version/output')
    stage.mkdir(parents=True)
    root = Path(__file__).resolve().parent
    env = os.environ.copy(); env['DOTNET_ROOT'] = str(a.dotnet.resolve().parent)
    subprocess.run([str(a.dotnet.resolve()), 'publish', str(root/'launcher/Launcher.csproj'), '-c', 'Release',
                    '-r', a.runtime, '--self-contained', 'true', '-p:DebugType=None', '-o', str(stage)] +
                   (['-p:OutputType=WinExe'] if a.runtime.startswith('win') else []), check=True, env=env)
    game = stage / 'game'; game.mkdir()
    if a.runtime.startswith('osx'):
        if not a.player.is_dir() or a.player.suffix != '.app': p.error('macOS requires Unity .app')
        subprocess.run(['ditto', str(a.player), str(game/'Player.app')], check=True)
        executable = subprocess.check_output(['/usr/libexec/PlistBuddy', '-c', 'Print :CFBundleExecutable',
                                             str(game/'Player.app/Contents/Info.plist')], text=True).strip()
        game_exe = 'game/Player.app/Contents/MacOS/' + executable
    else:
        if not a.player.is_dir() or not (a.player/'StarTournament.exe').exists(): p.error('Windows requires Player directory with StarTournament.exe')
        shutil.copytree(a.player, game, dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns('*_DoNotShip', '*.pdb'))
        game_exe = 'game/StarTournament.exe'
    app_id = 'tech.afonasev.star-tournament.' + a.runtime
    config = dict(appId=app_id, platform=a.runtime.split('-')[0], architecture=a.runtime.split('-')[1],
                  version=a.version, publicationDate=a.date, feedBase=a.feed, gameExecutable=game_exe, updateChannel=a.channel)
    (stage/'release.json').write_text(json.dumps(config, indent=2)+'\n')
    subprocess.run(['openssl', 'pkey', '-in', str(a.key.resolve()), '-pubout', '-out', str(stage/'update-public.pem')], check=True)
    if (stage/'update-public.pem').read_bytes() != (root/'update-public.pem').read_bytes():
        raise ValueError('Signing key differs from pinned distribution key')
    output = a.output/'releases'; output.mkdir(exist_ok=True)
    launcher = 'StarTournamentLauncher.exe' if a.runtime.startswith('win') else 'StarTournamentLauncher'
    cmd = [str(a.vpk.resolve())] + (['[win]'] if a.runtime.startswith('win') else [])
    cmd += ['--skip-updates', 'pack', '--packId', app_id, '--packVersion', a.version, '--packDir', str(stage),
            '--mainExe', launcher, '--runtime', a.runtime, '--channel', a.runtime, '--packTitle', 'Star Tournament',
            '--delta', 'None', '--outputDir', str(output), '--icon', str(root/'branding'/('app-icon.ico' if a.runtime.startswith('win') else 'app-icon.icns'))]
    if a.runtime == 'win-x64': cmd += ['--shortcuts', 'None']
    subprocess.run(cmd, check=True, env=env)
    if a.update_only:
        for first_install in output.iterdir():
            if first_install.suffix in ('.exe', '.pkg', '.zip'): first_install.unlink()
    elif a.runtime == 'win-x64':
        # NSIS owns installation, shortcuts and ARP; preserve the portable marker.
        portable = next(output.glob('*Portable.zip'))
        layout = a.output / ('windows-layout-' + a.version)
        with zipfile.ZipFile(portable) as archive:
            for entry in archive.infolist():
                if not (layout/entry.filename).resolve().is_relative_to(layout.resolve()):
                    raise ValueError('Unsafe portable layout path')
            archive.extractall(layout)
        if not (layout/'.portable').is_file() or not (layout/'Update.exe').is_file():
            raise ValueError('Expected complete Velopack portable layout')
        shutil.copyfile(root/'branding/app-icon.ico',layout/'app-icon.ico')
        maintenance = layout/'maintenance'; maintenance.mkdir()
        subprocess.run([str(a.dotnet.resolve()),'publish',str(root/'maintenance/Maintenance.csproj'),
                        '-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true',
                        '-p:DebugType=None','-o',str(maintenance)],check=True,env=env)
        (maintenance/'maintenance.json').write_text(json.dumps(dict(protocol=1, **config),indent=2)+'\n')
        shutil.copyfile(root/'update-public.pem',maintenance/'update-public.pem')
        next(output.glob('*Setup.exe')).unlink()  # Unpublished one-click Setup.
        installer = output/f'Star-Tournament-{a.version}-Windows-x64-Setup.exe'
        compiler = (['--compiler-host',a.nsis_host] if a.nsis_host else ['--makensis',str(a.makensis.resolve())])
        subprocess.run(['python3',str(root/'windows/build_installer.py'),'--layout',str(layout),
                        '--version',a.version,'--output',str(installer)] + compiler,check=True)
    if a.runtime.startswith('osx') and not a.update_only:
        for package in output.glob('*.pkg'):
            package.rename(output/f'Star-Tournament-{a.version}-macOS-{a.runtime.split("-")[1]}.pkg')
    feed = json.loads((output/f'releases.{a.runtime}.json').read_text())
    asset = next(x for x in feed['Assets'] if x['Version'] == a.version and x['Type'] == 'Full')
    payload = json.dumps(dict(schema=1, channel=a.channel, platform=config['platform'], architecture=config['architecture'],
                              version=a.version, publicationDate=a.date,
                              downloadUrl=f'https://github.com/afonasev/star-tournament/releases/download/v{a.version}/{asset["FileName"]}',
                              publishedAt=datetime.datetime.now(datetime.timezone.utc).isoformat(), feed={'Assets': [asset]}),
                         separators=(',', ':'), ensure_ascii=False).encode()
    signed = subprocess.run(['openssl','dgst','-sha256','-sign',str(a.key.resolve()),
                             '-sigopt','rsa_padding_mode:pss','-sigopt','rsa_pss_saltlen:32'], input=payload,
                            capture_output=True, check=True).stdout
    envelope = dict(payload=base64.b64encode(payload).decode(), signature=base64.b64encode(signed).decode())
    (output/f'{a.version}.signed.json').write_text(json.dumps(envelope)+'\n')
    (output/'latest.json').write_text(json.dumps(envelope)+'\n')  # Bridge feed compatibility.
    (output/f'latest-{a.runtime}.json').write_text(json.dumps(envelope)+'\n')
    files = []
    for f in sorted(output.iterdir()):
        if f.is_file() and f.suffix in ('.nupkg','.exe','.pkg','.zip'):
            with f.open('rb') as stream: digest = hashlib.file_digest(stream, 'sha256').hexdigest()
            files.append(dict(name=f.name, size=f.stat().st_size, sha256=digest))
    revision = subprocess.check_output(['git','rev-parse','HEAD'], cwd=root, text=True).strip()
    dirty = subprocess.check_output(['git','status','--short'], cwd=root, text=True)
    diff = subprocess.check_output(['git','diff','HEAD','--binary'], cwd=root)
    marker = hashlib.sha256()
    for player_file in sorted(a.player.rglob('*')):
        if player_file.is_file():
            with player_file.open('rb') as stream: digest = hashlib.file_digest(stream, 'sha256').hexdigest()
            marker.update((player_file.relative_to(a.player).as_posix()+'\0'+digest+'\n').encode())
    (a.output/f'{a.version}.identity.json').write_text(json.dumps(dict(version=a.version, date=a.date,
        runtime=a.runtime, channel=a.channel, revision=revision, dirty=dirty, sourceDiffSha256=hashlib.sha256(diff).hexdigest(),
        player=str(a.player), playerMarkerSha256=marker.hexdigest(), packages=files), indent=2)+'\n')
    print('PACKAGED', a.runtime, a.version, output)


if __name__ == '__main__': main()
