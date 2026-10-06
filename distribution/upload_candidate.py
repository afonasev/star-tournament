#!/usr/bin/env python3
"""Atomically publish authenticated test packages; do not switch the landing."""
import argparse
import hashlib
import base64
import json
from pathlib import Path
import re
import shlex
import subprocess
import uuid


def remote(script, *args):
    subprocess.run(['ssh', '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10', 'gfe',
                    'python3 -c ' + shlex.quote(script) + ' ' + ' '.join(map(shlex.quote, args))], check=True)


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--runtime', choices=['win-x64','osx-arm64','osx-x64'], required=True)
    p.add_argument('--releases', type=Path, required=True)
    p.add_argument('--version', required=True)
    p.add_argument('--installers', action='store_true', help='Also publish Setup/Portable artifacts after their checks')
    a = p.parse_args()
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', a.version): p.error('Invalid version')
    target = '/opt/robowar/desktop/test/' + a.runtime
    files = [f for f in a.releases.iterdir() if f.name.endswith('.nupkg') and '-'+a.version+'-' in f.name]
    manifest = a.releases / (a.version + '.signed.json')
    if len(files) != 1 or not manifest.is_file(): p.error('Exactly one full package and signed manifest required')
    release = json.loads(base64.b64decode(json.loads(manifest.read_text())['payload']))
    if release['version'] != a.version or release['platform']+'-'+release['architecture'] != a.runtime:
        p.error('Signed manifest target/version mismatch')
    asset = release['feed']['Assets'][0]
    full = files[0]
    if asset['FileName'] != full.name or asset['Size'] != full.stat().st_size or asset['SHA256'].lower() != sha256(full):
        p.error('Package differs from signed manifest')
    if a.installers: files += [f for f in a.releases.iterdir() if f.suffix in ('.exe','.pkg','.zip')]
    identity = json.loads((a.releases.parent / (a.version+'.identity.json')).read_text())
    if identity['version'] != a.version or identity['runtime'] != a.runtime:
        p.error('Artifact identity target/version mismatch')
    expected = {item['name']: item for item in identity['packages']}
    for local in files:
        item = expected.get(local.name)
        if not item or item['size'] != local.stat().st_size or item['sha256'] != sha256(local):
            p.error('Artifact changed since packaging; installer may belong to another version')
    remote('import pathlib,sys; pathlib.Path(sys.argv[1]).mkdir(parents=True,exist_ok=True)', target)
    for local in files + [manifest]:
        if not re.fullmatch(r'[A-Za-z0-9._-]+', local.name): p.error('Unsafe filename')
        digest = sha256(local)
        destination = target
        if local.suffix in ('.exe','.pkg','.zip'):
            destination += '/installers/' + a.version
            remote('import pathlib,sys; pathlib.Path(sys.argv[1]).mkdir(parents=True,exist_ok=True)', destination)
        staging = target + '/.upload-' + uuid.uuid4().hex
        subprocess.run(['scp','-q',str(local),'gfe:'+staging], check=True)
        remote('''import hashlib, pathlib, sys
src,dst,digest=pathlib.Path(sys.argv[1]),pathlib.Path(sys.argv[2]),sys.argv[3]
assert hashlib.file_digest(src.open('rb'),'sha256').hexdigest()==digest,'Upload hash mismatch'
if dst.exists():
    assert hashlib.file_digest(dst.open('rb'),'sha256').hexdigest()==digest,'Immutable filename collision'
    src.unlink()
else: src.replace(dst)
''', staging, destination+'/'+local.name, digest)
    remote('''import pathlib,sys
root=pathlib.Path(sys.argv[1]); data=(root/sys.argv[2]).read_bytes()
temp=root/'.latest-next';temp.write_bytes(data);temp.replace(root/'latest.json')
''', target, manifest.name)
    print('PUBLISHED TEST FEED', 'https://robowar.afonasev.tech/desktop/test/'+a.runtime+'/latest.json')


if __name__ == '__main__': main()
