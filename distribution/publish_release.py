#!/usr/bin/env python3
"""Paired, authenticated, draft-first GitHub release publication. Credential: stdin only."""
import argparse
import base64
import hashlib
import http.client
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request

from upload_github_assets import REPO, upload, tls_context
from legacy_release_relay import ReleaseRedirects

RUNTIMES = ('win-x64', 'osx-arm64')
ROOT = Path(__file__).resolve().parent


def api(method, path, token, data=None, missing=False):
    conn = http.client.HTTPSConnection('api.github.com', timeout=60, context=tls_context())
    body = None if data is None else json.dumps(data).encode()
    try:
        conn.request(method, '/repos/' + REPO + path, body=body, headers={
            'Authorization': 'Bearer ' + token, 'User-Agent': 'StarTournament-Publisher/1',
            'Accept': 'application/vnd.github+json', 'Content-Type': 'application/json',
            'X-GitHub-Api-Version': '2022-11-28'})
        response = conn.getresponse(); raw = response.read()
        if missing and response.status == 404: return None
        if response.status not in (200, 201):
            details=json.loads(raw)
            raise RuntimeError('GitHub API status '+str(response.status)+': '+json.dumps(details.get('errors',details.get('message'))))
        return json.loads(raw)
    finally: conn.close()


def identity(path):
    if path.is_symlink() or not path.is_file(): raise ValueError('Unsafe asset input')
    with path.open('rb') as stream: digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(path=str(path.resolve()), name=path.name, size=path.stat().st_size, sha256=digest)


def verify_manifest(path, version, runtime, channel):
    envelope = json.loads(path.read_text())
    payload = base64.b64decode(envelope['payload'], validate=True)
    signature = base64.b64decode(envelope['signature'], validate=True)
    with tempfile.TemporaryDirectory(prefix='star-tournament-signature-') as temp:
        signed = Path(temp)/'signature'; signed.write_bytes(signature)
        subprocess.run(['openssl','dgst','-sha256','-verify',str(ROOT/'update-public.pem'),
                        '-signature',str(signed),'-sigopt','rsa_padding_mode:pss','-sigopt','rsa_pss_saltlen:32'],
                       input=payload,check=True,stdout=subprocess.DEVNULL)
    data = json.loads(payload)
    platform, arch = runtime.split('-')
    if (data['schema'],data['version'],data['platform'],data['architecture'],data['channel']) != (1,version,platform,arch,channel):
        raise ValueError('Signed release identity mismatch')
    if len(data['feed']['Assets']) != 1: raise ValueError('Full package required')
    package = data['feed']['Assets'][0]
    if (package['PackageId'],package['Version'],package['Type']) != ('tech.afonasev.star-tournament.'+runtime,version,'Full'):
        raise ValueError('Invalid package identity')
    filename = package['FileName']
    if not re.fullmatch(r'[A-Za-z0-9._-]+\.nupkg',filename): raise ValueError('Unsafe package name')
    if data['downloadUrl'] != f'https://github.com/{REPO}/releases/download/v{version}/{filename}':
        raise ValueError('Unpinned package URL')
    return data, package


def collect(output, version, date, channel):
    assets = []; dates = set(); revisions = set()
    for runtime in RUNTIMES:
        base = output/runtime; releases = base/'releases'
        manifest = releases/f'latest-{runtime}.json'
        data, package = verify_manifest(manifest,version,runtime,channel)
        dates.add(data['publicationDate'])
        full = identity(releases/package['FileName'])
        if (full['size'],full['sha256'].lower()) != (package['Size'],package['SHA256'].lower()):
            raise ValueError('Package changed after signing')
        installer_name = (f'Star-Tournament-{version}-Windows-x64-Setup.exe' if runtime == 'win-x64'
                          else f'Star-Tournament-{version}-macOS-arm64.pkg')
        installer = identity(releases/installer_name)
        custody_path = base/f'{version}.identity.json'; custody = json.loads(custody_path.read_text())
        if (custody['version'],custody['date'],custody['runtime'],custody['channel']) != (version,date,runtime,channel):
            raise ValueError('Package custody mismatch')
        revisions.add(custody['revision'])
        by_name = {x['name']:x for x in custody['packages']}
        for item in (full,installer):
            signed_item = by_name[item['name']]
            if (signed_item['size'],signed_item['sha256']) != (item['size'],item['sha256']):
                raise ValueError('Installer/package custody changed')
        # Each platform gets a unique custody filename in the shared release.
        release_identity = releases/f'identity-{runtime}.json'
        release_identity.write_bytes(custody_path.read_bytes())
        assets.extend((full,installer,identity(manifest),identity(release_identity)))
    if dates != {date} or len(revisions) != 1: raise ValueError('Paired assets must share date and source revision')
    return assets


def verify_remote(release, assets):
    by_name = {x['name']:x for x in release['assets']}
    if set(by_name) != {x['name'] for x in assets}: raise ValueError('Draft assets incomplete or unexpected')
    for item in assets:
        remote = by_name[item['name']]
        if remote.get('state') != 'uploaded' or (remote['size'],remote.get('digest')) != (item['size'],'sha256:'+item['sha256']):
            raise ValueError('Draft identity mismatch')


def readback(version, assets):
    rows=[]; opener=urllib.request.build_opener(ReleaseRedirects(), urllib.request.HTTPSHandler(context=tls_context()))
    for item in assets:
        url=f'https://github.com/{REPO}/releases/download/v{version}/{item["name"]}'
        digest=hashlib.sha256();size=0
        with opener.open(urllib.request.Request(url,headers={'User-Agent':'StarTournament-Readback/1'}),timeout=60) as response:
            if response.status != 200: raise ValueError('Public download status mismatch')
            while chunk:=response.read(128*1024):
                size+=len(chunk)
                if size > item['size']: raise ValueError('Public asset exceeds expected size')
                digest.update(chunk)
        if (size,digest.hexdigest()) != (item['size'],item['sha256']): raise ValueError('Public asset identity mismatch')
        rows.append(dict(name=item['name'],url=url,size=size,sha256=digest.hexdigest(),status=200))
    return rows


def find_release(tag, token):
    # GitHub's by-tag endpoint cannot resolve a draft's not-yet-created Git tag.
    release=api('GET','/releases/tags/'+tag,token,missing=True)
    if release is not None: return release
    matches=[]
    for page in range(1,11):
        rows=api('GET',f'/releases?per_page=100&page={page}',token)
        matches.extend(x for x in rows if x['tag_name']==tag)
        if len(rows)<100:
            if len(matches)>1: raise ValueError('Multiple releases for this tag; inspect draft identities before resuming')
            return matches[0] if matches else None
    raise ValueError('Release lookup exceeds pagination limit')


def publish(version, date, channel, assets, token, source_ref, draft_only=False):
    tag='v'+version
    release=find_release(tag,token)
    if release is None:
        release=api('POST','/releases',token,dict(tag_name=tag,target_commitish=source_ref,name='Star Tournament '+version,
            body=f'Authenticated {channel} candidate, {date}. Windows/macOS installers and full updates share version and date.\n'
                 'OS developer signing/notarization and physical Windows/device acceptance remain pending.\n'
                 'Installers retain Program Files/UAC contract and corrected uninstaller. Test channel does not promote production.',
            draft=True,prerelease=channel=='test',make_latest='false'))
    if release['draft']:
        upload(release['id'],assets,token)
        release=api('GET',f'/releases/{release["id"]}',token)
        verify_remote(release,assets)
        if draft_only: return release, []
        release=api('PATCH',f'/releases/{release["id"]}',token,dict(draft=False,prerelease=channel=='test',make_latest='true' if channel=='production' else 'false',target_commitish=source_ref))
    else: verify_remote(release,assets)  # Idempotent identical published input; no asset replacement.
    if release['prerelease'] != (channel=='test') or release['draft']: raise ValueError('Published channel mismatch')
    return release,readback(version,assets)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('version','date','channel','source-ref'): p.add_argument('--'+name,required=True)
    for name in ('windows-player','mac-player','output','key','dotnet','vpk'): p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--nsis-host',choices=['gfe'],default='gfe')
    p.add_argument('--draft-only',action='store_true',help='Prepare and verify a draft for pre-publication QA')
    p.add_argument('--resume',action='store_true',help='Validate and resume an already packaged exact output')
    a=p.parse_args()
    if a.channel not in ('test','production') or not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?',a.version) or ('-' in a.version)!=(a.channel=='test'):
        p.error('Channel/version mismatch')
    token=sys.stdin.read(1024).strip()
    if not token: p.error('GitHub credential required on stdin')
    if not re.fullmatch(r'[0-9a-f]{40}',a.source_ref): p.error('Full reviewed public commit SHA required for --source-ref')
    commit=api('GET','/commits/'+a.source_ref,token)
    if commit['sha']!=a.source_ref: raise ValueError('Public source identity mismatch')
    a.output=a.output.resolve();a.output.mkdir(parents=True,exist_ok=True)
    if not a.resume:
        for runtime,player in zip(RUNTIMES,(a.windows_player,a.mac_player)):
            cmd=[sys.executable,str(ROOT/'package.py'),'--runtime',runtime,'--player',str(player.resolve()),
                 '--version',a.version,'--date',a.date,'--channel',a.channel,'--key',str(a.key.resolve()),
                 '--output',str(a.output/runtime),'--dotnet',str(a.dotnet.resolve()),'--vpk',str(a.vpk.resolve())]
            if runtime=='win-x64':cmd+=['--nsis-host',a.nsis_host]
            subprocess.run(cmd,check=True)
    assets=collect(a.output,a.version,a.date,a.channel)
    (a.output/'asset-manifest.json').write_text(json.dumps(assets,indent=2)+'\n')
    release,checks=publish(a.version,a.date,a.channel,assets,token,a.source_ref,a.draft_only)
    (a.output/'publication.json').write_text(json.dumps(dict(release=release,readback=checks),indent=2)+'\n')
    print('DRAFT' if release['draft'] else 'PUBLISHED',release['html_url'])


if __name__=='__main__':main()
