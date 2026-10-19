"""Produce the separately authenticated, fixed-file Windows upgrade component."""
import base64
import datetime
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

from release_compatibility import validate, READER_PROTOCOL

FILES = ('StarTournamentMaintenance.exe', 'maintenance.json', 'update-public.pem')
ENVELOPE = 'broker-upgrade.json'


def plain(path):
    for item in (path, *path.parents):
        if item.is_symlink():
            raise ValueError('Broker candidate cannot use symlinks')


def seal(directory: Path, config: dict, key: Path, pinned_key: Path):
    """Seal an unpublished single-file publish output; no builds or execution here."""
    expected = {'appId', 'platform', 'architecture', 'version', 'publicationDate',
                'feedBase', 'gameExecutable', 'updateChannel', 'compatibility'}
    if set(config) != expected:
        raise ValueError('Exact payload config fields required')
    channel = config['updateChannel']
    feed = ('https://api.github.com/repos/afonasev/star-tournament/releases' if channel == 'test'
            else 'https://github.com/afonasev/star-tournament/releases/latest/download/')
    if (config['appId'] != 'tech.afonasev.star-tournament.win-x64' or config['platform'] != 'win'
            or config['architecture'] != 'x64' or config['gameExecutable'] != 'game/StarTournament.exe'
            or channel not in ('test', 'production') or config['feedBase'] != feed
            or not re.fullmatch(r'(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*)?', config['version'])
            or ('-' in config['version']) != (channel == 'test')):
        raise ValueError('Unsupported broker payload identity')
    if datetime.date.fromisoformat(config['publicationDate']).isoformat() != config['publicationDate']:
        raise ValueError('Canonical publication date required')
    if any(int(n) > 2147483647 for n in config['version'].split('-')[0].split('.')):
        raise ValueError('Version exceeds reader range')
    validate(config['compatibility'], 'win')
    plain(directory); plain(pinned_key)
    if {p.name for p in directory.iterdir()} != {FILES[0], FILES[2]}:
        raise ValueError('Fresh self-contained single-file broker output required')
    for name in (FILES[0], FILES[2]):
        path = directory/name; plain(path)
        if not path.is_file() or not 0 < path.stat().st_size <= (128*1024*1024 if name.endswith('.exe') else 65536):
            raise ValueError('Invalid broker file size/type')
    public = subprocess.run(['openssl', 'pkey', '-in', str(key), '-pubout'], check=True, capture_output=True).stdout
    if public != pinned_key.read_bytes() or (directory/FILES[2]).read_bytes() != public:
        raise ValueError('Candidate cannot rotate the pinned signing key')
    contract = dict(protocol=2, metadataReader=READER_PROTOCOL, **config)
    contract_bytes=(json.dumps(contract, indent=2)+'\n').encode()
    if len(contract_bytes)>65536:
        raise ValueError('Broker contract exceeds reader bound')
    (directory/FILES[1]).write_bytes(contract_bytes)
    body = dict(schema=1, **{k: config[k] for k in ('appId', 'platform', 'architecture', 'version', 'publicationDate')},
                channel=channel, protocol=2, metadataReader=READER_PROTOCOL,
                files=[dict(name=name, size=(directory/name).stat().st_size,
                            sha256=hashlib.sha256((directory/name).read_bytes()).hexdigest()) for name in FILES])
    raw = json.dumps(body, separators=(',', ':')).encode()
    signed = subprocess.run(['openssl', 'dgst', '-sha256', '-sign', str(key),
                             '-sigopt', 'rsa_padding_mode:pss', '-sigopt', 'rsa_pss_saltlen:32'],
                            input=raw, capture_output=True, check=True).stdout
    envelope = dict(payload=base64.b64encode(raw).decode(), signature=base64.b64encode(signed).decode())
    (directory/ENVELOPE).write_text(json.dumps(envelope)+'\n')
    return {name: hashlib.sha256((directory/name).read_bytes()).hexdigest() for name in (*FILES, ENVELOPE)}


def install_component(candidate: Path, destination: Path, sealed_hashes: dict):
    """First-install uses exactly the component already embedded in the payload."""
    plain(candidate); plain(destination)
    if {p.name for p in candidate.iterdir()} != set(FILES) | {ENVELOPE}:
        raise ValueError('Complete sealed broker candidate required')
    if set(sealed_hashes) != set(FILES) | {ENVELOPE}:
        raise ValueError('Frozen sealed candidate required')
    if destination.exists():
        raise ValueError('Fresh maintenance destination required')
    for name in (*FILES, ENVELOPE):
        plain(candidate/name)
        if not (candidate/name).is_file():
            raise ValueError('Broker component must contain only files')
        if hashlib.sha256((candidate/name).read_bytes()).hexdigest() != sealed_hashes[name]:
            raise ValueError('Candidate changed after signing/packing')
    destination.mkdir()
    for name in FILES:
        shutil.copyfile(candidate/name, destination/name)
        if hashlib.sha256((destination/name).read_bytes()).hexdigest() != sealed_hashes[name]:
            raise ValueError('Installed component differs from frozen candidate')
