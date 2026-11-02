"""Package independent bootstrap code; no Player build or publication."""
import json
from pathlib import Path
import shutil
import subprocess


def install_windows(layout: Path):
    current = layout/'current'
    destination = layout/'maintenance/bootstrap'
    if destination.exists() or destination.is_symlink():
        raise ValueError('Immutable bootstrap already exists')
    config = json.loads((current/'release.json').read_text())
    if type(config.get('bootstrapProtocol')) is not int or config.get('bootstrapProtocol') != 1:
        raise ValueError('Independent bootstrap payload contract required')
    required = ['StarTournamentLauncher.exe', 'StarTournamentLauncher.deps.json',
                'StarTournamentLauncher.runtimeconfig.json', 'StarTournamentLauncher.dll',
                'release.json', 'update-public.pem']
    for path in [layout, current, layout/'maintenance'] + [current/name for name in required]:
        if path.is_symlink() or not path.exists():
            raise ValueError('Complete plain bootstrap source required')
    # Self-contained Windows publish keeps runtime assemblies/native libraries flat.
    files = [p for p in current.iterdir() if p.is_file() and
             (p.suffix == '.dll' or p.name in required)]
    if any(p.is_symlink() for p in files): raise ValueError('Bootstrap runtime links forbidden')
    destination.mkdir()
    try:
        for path in files: shutil.copyfile(path, destination/path.name)
    except BaseException:
        shutil.rmtree(destination)
        raise


def compile_macos(stage: Path, runtime: str, app_id: str):
    architecture = {'osx-arm64': 'arm64', 'osx-x64': 'x86_64'}[runtime]
    subprocess.run(['clang', '-arch', architecture, '-mmacosx-version-min=12.0',
                    '-Wno-deprecated-declarations', '-DAPP_ID="'+app_id+'"',
                    str(Path(__file__).parent/'bootstrap/macos_entry.c'),
                    '-o', str(stage/'StarTournamentBootstrap')], check=True)
    subprocess.run(['/usr/bin/codesign', '--force', '--sign', '-', str(stage/'StarTournamentBootstrap')], check=True, capture_output=True)
