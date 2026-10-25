"""Signed requirements of the atomic launcher/Unity Player payload (metadata schema 2)."""
import plistlib
import re
from pathlib import Path

READER_PROTOCOL = 2
PAYLOAD_FORMAT = 'velopack-full-v1'
DATA_POLICY = 'profiles2-lab3-prefs1'
FIELDS = {'minimumOsVersion', 'minimumUpdaterProtocol', 'payloadFormat', 'userDataPolicy'}


def os_version(value, platform):
    if not isinstance(value, str) or not re.fullmatch(r'(?:0|[1-9][0-9]*)(?:\.(?:0|[1-9][0-9]*)){1,2}', value):
        raise ValueError('Canonical minimum OS version required')
    parts = tuple(map(int, value.split('.')))
    if any(x > 2147483647 for x in parts) or (platform == 'win' and len(parts) != 3):
        raise ValueError('Invalid minimum OS version')
    floor = (10, 0, 19043) if platform == 'win' else (12, 0)
    if platform not in ('win', 'osx') or parts < floor:
        raise ValueError('Target OS is below the Unity Player baseline')
    return parts


def validate(requirements, platform):
    if not isinstance(requirements, dict) or set(requirements) != FIELDS:
        raise ValueError('Explicit signed compatibility fields required')
    os_version(requirements['minimumOsVersion'], platform)
    if type(requirements['minimumUpdaterProtocol']) is not int or requirements['minimumUpdaterProtocol'] != READER_PROTOCOL:
        raise ValueError('Unsupported updater reader protocol')
    if requirements['payloadFormat'] != PAYLOAD_FORMAT or requirements['userDataPolicy'] != DATA_POLICY:
        raise ValueError('Unsupported payload or user-data contract')
    return requirements


def current(runtime, player: Path):
    platform = runtime.split('-')[0]
    minimum = '10.0.19043'
    if platform == 'osx':
        with (player / 'Contents/Info.plist').open('rb') as stream:
            minimum = plistlib.load(stream).get('LSMinimumSystemVersion')
        # Never advertise a lower floor than the actual Player. Missing/invalid is an error.
        os_version(minimum, platform)
    return validate(dict(minimumOsVersion=minimum, minimumUpdaterProtocol=READER_PROTOCOL,
                         payloadFormat=PAYLOAD_FORMAT, userDataPolicy=DATA_POLICY), platform)


def manifest(config, asset, published_at):
    requirements = validate(config['compatibility'], config['platform'])
    return dict(schema=2, channel=config['updateChannel'], platform=config['platform'], architecture=config['architecture'],
                version=config['version'], publicationDate=config['publicationDate'], compatibility=requirements,
                downloadUrl=f'https://github.com/afonasev/star-tournament/releases/download/v{config["version"]}/{asset["FileName"]}',
                publishedAt=published_at, feed={'Assets': [asset]})


def verify_payload(package, descriptor):
    import zipfile
    with zipfile.ZipFile(package) as archive:
        entries = [e for e in archive.infolist() if Path(e.filename).name == 'release.json']
        if len(entries) != 1 or entries[0].file_size > 65536:
            raise ValueError('Unique bounded payload config required')
        with archive.open(entries[0]) as stream:
            content = stream.read(65537)
        if len(content) > 65536:
            raise ValueError('Payload config exceeds limit')
        import json
        config = json.loads(content)
    for name in ('version', 'platform', 'architecture', 'publicationDate', 'compatibility'):
        if config.get(name) != descriptor.get(name):
            raise ValueError('Payload config differs from signed target requirements: ' + name)
    if config.get('updateChannel') != descriptor['channel'] or config.get('appId') != descriptor['feed']['Assets'][0]['PackageId']:
        raise ValueError('Payload config identity mismatch')
