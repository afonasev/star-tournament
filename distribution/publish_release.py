#!/usr/bin/env python3
"""Paired, authenticated, draft-first GitHub release publication. Credential: stdin only."""
import argparse
import base64
import datetime
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

from release_compatibility import validate as validate_compatibility, verify_payload
from upload_github_assets import REPO, upload, tls_context
from legacy_release_relay import ReleaseRedirects
from release_media import prepare as prepare_media
from release_changelog import previous_release, resolve, render, validate_body, git, version_key

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


def metadata_bytes(path):
    if path.is_symlink() or not path.is_file() or path.stat().st_size > 65536:
        raise ValueError('Unique bounded regular signed metadata required')
    raw = path.read_bytes()
    if len(raw) > 65536: raise ValueError('Signed metadata exceeds limit')
    return raw


def verify_manifest(path, version, runtime, channel, *, schema=2):
    return verify_envelope(metadata_bytes(path), version, runtime, channel, schema=schema)


def verify_envelope(raw, version, runtime, channel, *, schema=2):
    envelope = json.loads(raw)
    payload = base64.b64decode(envelope['payload'], validate=True)
    signature = base64.b64decode(envelope['signature'], validate=True)
    with tempfile.TemporaryDirectory(prefix='star-tournament-signature-') as temp:
        signed = Path(temp)/'signature'; signed.write_bytes(signature)
        subprocess.run(['openssl','dgst','-sha256','-verify',str(ROOT/'update-public.pem'),
                        '-signature',str(signed),'-sigopt','rsa_padding_mode:pss','-sigopt','rsa_pss_saltlen:32'],
                       input=payload,check=True,stdout=subprocess.DEVNULL)
    data = json.loads(payload)
    platform, arch = runtime.split('-')
    if (data['schema'],data['version'],data['platform'],data['architecture'],data['channel']) != (schema,version,platform,arch,channel):
        raise ValueError('Signed release identity mismatch')
    if type(data['schema']) is not int: raise ValueError('Integer metadata schema required')
    date = data['publicationDate']
    if not isinstance(date, str) or datetime.date.fromisoformat(date).isoformat() != date:
        raise ValueError('Canonical publication date required')
    if schema == 2:
        validate_compatibility(data.get('compatibility'), platform)
    elif schema != 1 or 'compatibility' in data:
        raise ValueError('Legacy metadata cannot override historical requirements')
    if len(data['feed']['Assets']) != 1: raise ValueError('Full package required')
    package = data['feed']['Assets'][0]
    if (package['PackageId'],package['Version'],package['Type']) != ('tech.afonasev.star-tournament.'+runtime,version,'Full'):
        raise ValueError('Invalid package identity')
    if type(package['Size']) is not int or package['Size'] <= 0 or not re.fullmatch(r'[a-fA-F0-9]{64}',package['SHA256']):
        raise ValueError('Invalid authenticated package size/hash')
    filename = package['FileName']
    if not re.fullmatch(r'[A-Za-z0-9._-]+\.nupkg',filename): raise ValueError('Unsafe package name')
    if data['downloadUrl'] != f'https://github.com/{REPO}/releases/download/v{version}/{filename}':
        raise ValueError('Unpinned package URL')
    return data, package


def publication_mode(channel, legacy_bridge, new_install_only):
    if new_install_only and (channel != 'production' or legacy_bridge is not None):
        raise ValueError('New-install-only requires production and no legacy bridge')
    return 'production-new-install-only' if new_install_only else 'legacy-compatible'


def require_new_install_notice(body):
    if 'Установите эту версию вручную' not in body or 'Install this version manually' not in body:
        raise ValueError('New-install-only requires explicit Russian/English manual installation notes')


def collect(output, version, date, channel, source_ref=None, *, legacy_bridge=None, new_install_only=False):
    publication_mode(channel, legacy_bridge, new_install_only)
    assets = []; dates = set(); revisions = set()
    for runtime in RUNTIMES:
        base = output/runtime; releases = base/'releases'
        manifest = releases/f'latest-v2-{runtime}.json'
        data, package = verify_manifest(manifest,version,runtime,channel)
        if new_install_only:
            if 'legacyBootstrap' in data or any((releases/name).exists() for name in (f'latest-{runtime}.json', 'latest.json', f'legacy-bootstrap-{runtime}.json')):
                raise ValueError('New-install-only cannot claim or contain legacy bootstrap aliases')
        dates.add(data['publicationDate'])
        full = identity(releases/package['FileName'])
        if (full['size'],full['sha256'].lower()) != (package['Size'],package['SHA256'].lower()):
            raise ValueError('Package changed after signing')
        verify_payload(releases/package['FileName'], data)
        installer_name = (f'Star-Tournament-{version}-Windows-x64-Setup.exe' if runtime == 'win-x64'
                          else f'Star-Tournament-{version}-macOS-arm64.dmg')
        installer = identity(releases/installer_name)
        installers = [installer]
        if runtime == 'osx-arm64':
            installers.append(identity(releases/f'Star-Tournament-{version}-macOS-arm64.pkg'))
        custody_path = base/f'{version}.identity.json'; custody = json.loads(custody_path.read_text())
        if (custody['version'],custody['date'],custody['runtime'],custody['channel']) != (version,date,runtime,channel):
            raise ValueError('Package custody mismatch')
        if custody.get('dirty','').strip(): raise ValueError('Dirty package custody cannot be described by Git history')
        revisions.add(custody['revision'])
        by_name = {x['name']:x for x in custody['packages']}
        for item in (full,*installers):
            signed_item = by_name[item['name']]
            if (signed_item['size'],signed_item['sha256']) != (item['size'],item['sha256']):
                raise ValueError('Installer/package custody changed')
        # Each platform gets a unique custody filename in the shared release.
        release_identity = releases/f'identity-{runtime}.json'
        release_identity.write_bytes(custody_path.read_bytes())
        assets.extend((full,*installers,identity(manifest),identity(release_identity)))
    if dates != {date} or len(revisions) != 1: raise ValueError('Paired assets must share date and source revision')
    if source_ref is not None and revisions != {source_ref}: raise ValueError('Assets do not match changelog source revision')
    if not new_install_only: assets.extend(collect_bridge(output, version, channel, legacy_bridge))
    return assets


# Reserved for the compatible bootstrap producer, not a general schema1 downgrade.
BOOTSTRAP_CONTRACT = dict(schema=1, metadataReader=2, windowsMaintenance=2)


def validate_bridge(version, channel, directory):
    """Validate signed, same-package bootstrap pairs; never synthesize legacy bytes.

    This is a publication gate, not evidence that the bootstrap runs correctly.
    The bootstrap producer/automatic broker migration remains a required stage.
    """
    if channel not in ('test', 'production') or ('-' in version) != (channel == 'test'):
        raise ValueError('Target channel/version mismatch')
    if directory is None:
        raise ValueError('Signed legacy bootstrap directory required before modern publication')
    directory = Path(directory)
    if directory.is_symlink() or not directory.is_dir():
        raise ValueError('Regular legacy bootstrap directory required')
    pairs = []
    versions = set()
    dates = set()
    for runtime in RUNTIMES:
        modern_path = directory/f'latest-v2-{runtime}.json'
        # Decode only to locate version, then authenticate identity with pinned key.
        # No path or URL is constructed from this untrusted value.
        modern_raw = metadata_bytes(modern_path)
        unsigned = json.loads(base64.b64decode(json.loads(modern_raw)['payload'], validate=True))
        bridge_version = unsigned['version']
        if version_key(bridge_version) >= version_key(version) or ('-' in bridge_version) != (channel == 'test'):
            raise ValueError('Bootstrap must precede target in the same channel')
        modern, _ = verify_envelope(modern_raw, bridge_version, runtime, channel)
        if modern.get('legacyBootstrap') != BOOTSTRAP_CONTRACT or any(type(v) is not int for v in modern.get('legacyBootstrap', {}).values()):
            raise ValueError('Explicit signed compatible bootstrap contract required')
        floor = '10.0.19043' if runtime.startswith('win') else '12.0'
        if modern['compatibility']['minimumOsVersion'] != floor:
            raise ValueError('Bootstrap cannot raise the historical OS floor')
        legacy_path = directory/f'latest-{runtime}.json'
        legacy_raw = metadata_bytes(legacy_path)
        legacy, _ = verify_envelope(legacy_raw, bridge_version, runtime, channel, schema=1)
        expected = {k:v for k,v in modern.items() if k not in ('compatibility', 'legacyBootstrap')}
        expected['schema'] = 1
        if legacy != expected:
            raise ValueError('Bootstrap descriptors must bind exactly the same package and identity')
        versions.add(bridge_version); dates.add(modern['publicationDate'])
        pairs.append((runtime, modern_raw, legacy_raw))
    if len(versions) != 1 or len(dates) != 1:
        raise ValueError('Bootstrap platform pair must share version and date')
    return next(iter(versions)), pairs


def collect_bridge(output, version, channel, directory):
    _, pairs = validate_bridge(version, channel, directory)
    # Validate the entire pair before writing. Immutable outputs permit safe resume.
    pending = []
    for runtime, modern_raw, legacy_raw in pairs:
        destination = output/runtime/'releases'
        pending.append((modern_raw, destination/f'legacy-bootstrap-{runtime}.json'))
        if channel == 'production': pending.append((legacy_raw, destination/f'latest-{runtime}.json'))
    for raw, target in pending:
        if target.is_symlink() or (target.exists() and target.read_bytes() != raw):
            raise ValueError('Existing immutable bootstrap metadata differs')
    assets = []
    for raw, target in pending:
        target.write_bytes(raw)
        assets.append(identity(target))
    return assets


def verify_published_bridge(version, channel, directory, token):
    """Read-only live check before any new draft/package is created.

    Signed bytes alone are insufficient: the bootstrap must already be publicly
    downloadable with the paired installers and authenticated full packages.
    """
    bridge_version, pairs = validate_bridge(version, channel, directory)
    release = api('GET', '/releases/tags/v'+bridge_version, token)
    if (release.get('draft') or not release.get('published_at') or
        release.get('prerelease') != (channel == 'test') or release.get('tag_name') != 'v'+bridge_version):
        raise ValueError('Compatible bootstrap must already be published in the same channel')
    rows = release['assets']
    by_name = {a['name']:a for a in rows}
    if len(by_name) != len(rows): raise ValueError('Duplicate bootstrap release asset names')
    descriptors = []; metadata = {}
    for runtime, modern_raw, legacy_raw in pairs:
        modern, package = verify_envelope(modern_raw, bridge_version, runtime, channel)
        installer = (f'Star-Tournament-{bridge_version}-Windows-x64-Setup.exe' if runtime == 'win-x64'
                     else f'Star-Tournament-{bridge_version}-macOS-arm64.dmg')
        # Published bootstrap bytes are immutable. Older bridges used PKG.
        # Prefer a present DMG strictly; a corrupt DMG must not fall back to PKG.
        if runtime.startswith('osx') and installer not in by_name:
            installer = f'Star-Tournament-{bridge_version}-macOS-arm64.pkg'
        for name in (package['FileName'], installer):
            remote = by_name.get(name, {})
            if (remote.get('state') != 'uploaded' or type(remote.get('size')) is not int or remote['size'] <= 0 or
                not re.fullmatch(r'sha256:[a-f0-9]{64}', remote.get('digest', ''))):
                raise ValueError('Published bootstrap package/installer incomplete')
            descriptors.append(dict(name=name,size=remote['size'],sha256=remote['digest'][7:]))
        full = by_name[package['FileName']]
        if (full['size'],full['digest']) != (package['Size'],'sha256:'+package['SHA256'].lower()):
            raise ValueError('Published bootstrap package differs from signed identity')
        for prefix, raw in (('latest-v2-', modern_raw), ('latest-', legacy_raw)):
            name = prefix+runtime+'.json'; remote = by_name.get(name, {})
            expected = dict(name=name,size=len(raw),sha256=hashlib.sha256(raw).hexdigest())
            if remote.get('state') != 'uploaded' or (remote.get('size'),remote.get('digest')) != (expected['size'],'sha256:'+expected['sha256']):
                raise ValueError('Published bootstrap metadata differs from signed input')
            descriptors.append(expected); metadata[name] = raw.decode('utf-8')
    return dict(version=bridge_version, channel=channel, metadata=metadata, readback=readback(bridge_version,descriptors))


def snapshot_bridge(output, receipt):
    """Freeze the exact read-back signed inputs across lengthy packaging/resume."""
    destination = output/'legacy-bridge-inputs'
    if destination.is_symlink(): raise ValueError('Bootstrap snapshot cannot use a symlink')
    destination.mkdir(exist_ok=True)
    expected = {prefix+runtime+'.json' for prefix in ('latest-', 'latest-v2-') for runtime in RUNTIMES}
    if set(receipt['metadata']) != expected: raise ValueError('Incomplete bootstrap snapshot')
    pending = [(destination/name, raw.encode('utf-8')) for name,raw in receipt['metadata'].items()]
    for path, raw in pending:
        if path.is_symlink() or (path.exists() and path.read_bytes() != raw):
            raise ValueError('Existing immutable bootstrap snapshot differs')
    for path, raw in pending: path.write_bytes(raw)
    return destination


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


def changelog_baseline(version, channel, token, repo):
    rows=[]
    for page in range(1,11):
        batch=api('GET',f'/releases?per_page=100&page={page}',token);rows.extend(batch)
        if len(batch)<100: break
    else: raise ValueError('Release history exceeds pagination limit')
    tag=previous_release(rows,version,channel)
    if tag is None: return None
    remote=api('GET','/git/ref/tags/'+tag,token)['object']
    for _ in range(5):
        if remote['type']=='commit': break
        if remote['type']!='tag': raise ValueError('Unexpected release tag object')
        remote=api('GET','/git/tags/'+remote['sha'],token)['object']
    else: raise ValueError('Release tag nesting exceeds limit')
    local=resolve(repo,'refs/tags/'+tag)
    if local!=remote['sha']: raise ValueError('Local previous release tag differs from GitHub; fetch exact published tag')
    return dict(tag=tag,revision=local)


def prepare_changelog(review_path, repo, source_ref, version, channel, token, output):
    previous=changelog_baseline(version,channel,token,repo)
    review_bytes=review_path.read_bytes()
    review=json.loads(review_bytes.decode('utf-8'))
    body=render(review,repo,source_ref,channel,previous,version=version)
    identity=dict(schema=1,version=version,source_ref=source_ref,channel=channel,previous_release=previous,
                  body_sha256=hashlib.sha256(body.encode()).hexdigest(),
                  review_sha256=hashlib.sha256(review_bytes).hexdigest())
    # Reject a changed review/body before materializing any new local media on resume.
    for name,value in [('release-notes.md',body.encode('utf-8')),('release-notes.review.json',review_bytes)]:
        path=output/name
        if path.is_symlink() or (path.exists() and path.read_bytes()!=value): raise ValueError('Existing release notes identity differs; inspect output before resuming')
    media=prepare_media(review,review_path.parent,version,output)
    if media: identity['media']=[dict(name=path.name,size=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest()) for path in media]
    output.mkdir(parents=True,exist_ok=True)
    files=[('release-notes.md',body.encode('utf-8')),
           ('release-notes.identity.json',(json.dumps(identity,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode('utf-8')),
           ('release-notes.review.json',review_bytes)]
    for name,value in files:
        path=output/name
        if path.exists() and path.read_bytes()!=value: raise ValueError('Existing release notes identity differs; inspect output before resuming')
    for name,value in files:
        path=output/name;next_path=output/(name+'.tmp');next_path.write_bytes(value);next_path.replace(path)
    return body


def changelog_assets(output):
    notes_identity=json.loads((output/'release-notes.identity.json').read_text())
    result=[]
    for item in notes_identity.get('media',[]):
        if not re.fullmatch(r'history-[0-9a-f]{64}\.png',item['name']): raise ValueError('Unsafe release media name')
        actual=identity(output/'history-media'/item['name'])
        if (actual['name'],actual['size'],actual['sha256']) != (item['name'],item['size'],item['sha256']):
            raise ValueError('Prepared release media identity differs before upload')
        result.append(actual)
    return result


def publish(version, date, channel, assets, token, source_ref, draft_only=False, *, body=None):
    validate_body(body)
    tag='v'+version
    release=find_release(tag,token)
    if release is None:
        release=api('POST','/releases',token,dict(tag_name=tag,target_commitish=source_ref,name='Star Tournament '+version,
            body=body,
            draft=True,prerelease=channel=='test',make_latest='false'))
    if release.get('body') != body: raise ValueError('Release body differs from reviewed immutable notes')
    if release['draft']:
        upload(release['id'],assets,token)
        release=api('GET',f'/releases/{release["id"]}',token)
        verify_remote(release,assets)
        if release.get('body') != body: raise ValueError('Draft body changed during upload')
        if draft_only: return release, []
        release=api('PATCH',f'/releases/{release["id"]}',token,dict(draft=False,prerelease=channel=='test',make_latest='true' if channel=='production' else 'false',target_commitish=source_ref))
    else: verify_remote(release,assets)  # Idempotent identical published input; no asset replacement.
    if release['prerelease'] != (channel=='test') or release['draft']: raise ValueError('Published channel mismatch')
    if release.get('body') != body: raise ValueError('Published body mismatch')
    return release,readback(version,assets)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('version','date','channel','source-ref'): p.add_argument('--'+name,required=True)
    for name in ('windows-player','mac-player','output','key','dotnet','vpk'): p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--changelog-review',type=Path,required=True,help='Reviewed RU/EN Git coverage JSON from release_changelog.py')
    p.add_argument('--legacy-bridge',type=Path,help='Authenticated paired schema1/schema2 compatible bootstrap metadata; required for publication')
    p.add_argument('--new-install-only',action='store_true',help='Explicit owner-authorized production installation check; no legacy transition claim or aliases')
    p.add_argument('--nsis-host',choices=['gfe'],default='gfe')
    p.add_argument('--draft-only',action='store_true',help='Prepare and verify a draft for pre-publication QA')
    p.add_argument('--resume',action='store_true',help='Validate and resume an already packaged exact output')
    a=p.parse_args()
    if a.channel not in ('test','production') or not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?',a.version) or ('-' in a.version)!=(a.channel=='test'):
        p.error('Channel/version mismatch')
    mode=publication_mode(a.channel,a.legacy_bridge,a.new_install_only)
    token=sys.stdin.read(1024).strip()
    if not token: p.error('GitHub credential required on stdin')
    if not re.fullmatch(r'[0-9a-f]{40}',a.source_ref): p.error('Full reviewed public commit SHA required for --source-ref')
    if resolve(ROOT.parent,'HEAD')!=a.source_ref or git(ROOT.parent,'status','--porcelain'):
        raise ValueError('Run publication from the clean exact source-ref checkout; keep review/output ignored or outside it')
    commit=api('GET','/commits/'+a.source_ref,token)
    if commit['sha']!=a.source_ref: raise ValueError('Public source identity mismatch')
    bridge_readback=None if a.new_install_only else verify_published_bridge(a.version,a.channel,a.legacy_bridge,token)
    a.output=a.output.resolve();a.output.mkdir(parents=True,exist_ok=True)
    policy=(json.dumps(dict(mode=mode,version=a.version,channel=a.channel,source_ref=a.source_ref),sort_keys=True,indent=2)+'\n').encode()
    policy_path=a.output/'publication-policy.json'
    if policy_path.is_symlink() or (policy_path.exists() and policy_path.read_bytes()!=policy):
        raise ValueError('Publication policy differs from immutable output')
    policy_path.write_bytes(policy)
    bridge_snapshot=None
    if bridge_readback is not None:
        bridge_snapshot=snapshot_bridge(a.output,bridge_readback)
        (a.output/'legacy-bridge-readback.json').write_text(json.dumps(bridge_readback,indent=2)+'\n')
    body=prepare_changelog(a.changelog_review,ROOT.parent,a.source_ref,a.version,a.channel,token,a.output)
    if a.new_install_only: require_new_install_notice(body)
    if not a.resume:
        for runtime,player in zip(RUNTIMES,(a.windows_player,a.mac_player)):
            cmd=[sys.executable,str(ROOT/'package.py'),'--runtime',runtime,'--player',str(player.resolve()),
                 '--version',a.version,'--date',a.date,'--channel',a.channel,'--key',str(a.key.resolve()),
                 '--output',str(a.output/runtime),'--dotnet',str(a.dotnet.resolve()),'--vpk',str(a.vpk.resolve())]
            if runtime=='win-x64':cmd+=['--nsis-host',a.nsis_host]
            subprocess.run(cmd,check=True)
    assets=collect(a.output,a.version,a.date,a.channel,a.source_ref,legacy_bridge=bridge_snapshot,new_install_only=a.new_install_only)
    assets.extend(changelog_assets(a.output))
    (a.output/'asset-manifest.json').write_text(json.dumps(assets,indent=2)+'\n')
    release,checks=publish(a.version,a.date,a.channel,assets,token,a.source_ref,a.draft_only,body=body)
    (a.output/'publication.json').write_text(json.dumps(dict(release=release,readback=checks),indent=2)+'\n')
    print('DRAFT' if release['draft'] else 'PUBLISHED',release['html_url'])


if __name__=='__main__':main()
