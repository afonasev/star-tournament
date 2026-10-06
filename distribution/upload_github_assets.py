#!/usr/bin/env python3
"""Upload immutable release assets on the file-owning host; token comes from stdin only."""
import argparse
import hashlib
import http.client
import json
from pathlib import Path
import re
import sys
import ssl
import urllib.parse

REPO = 'afonasev/star-tournament'


def tls_context():
    context = ssl.create_default_context()
    # macOS python.org installs may lack their optional certificate bootstrap.
    # Use the OS PEM roots only when the default store is empty; verification stays required.
    if not context.get_ca_certs() and sys.platform == 'darwin':
        context.load_verify_locations('/etc/ssl/cert.pem')
    return context


def request(host, method, path, token, body=None, size=None):
    conn = http.client.HTTPSConnection(host, timeout=1800, context=tls_context())
    headers = {'Authorization':'Bearer '+token, 'User-Agent':'StarTournament-Publisher/1',
               'Accept':'application/vnd.github+json', 'X-GitHub-Api-Version':'2022-11-28'}
    if body is not None: headers.update({'Content-Type':'application/octet-stream','Content-Length':str(size)})
    try:
        conn.request(method, path, body=body, headers=headers)
        response = conn.getresponse(); data = response.read()
        if response.status not in (200,201): raise RuntimeError('GitHub asset API status '+str(response.status))
        return json.loads(data)
    finally: conn.close()


def upload(release_id, manifest, token):
    existing = request('api.github.com','GET',f'/repos/{REPO}/releases/{release_id}/assets?per_page=100',token)
    by_name = {item['name']:item for item in existing}
    result = []
    for item in manifest:
        source = Path(item['path'])
        if source.is_symlink() or not source.is_file() or not re.fullmatch(r'[A-Za-z0-9._-]+',item['name']):
            raise ValueError('Unsafe release input')
        with source.open('rb') as stream: digest = hashlib.file_digest(stream,'sha256').hexdigest()
        if source.stat().st_size != item['size'] or digest != item['sha256']: raise ValueError('Source identity changed')
        asset = by_name.get(item['name'])
        if asset is None:
            with source.open('rb') as stream:
                asset = request('uploads.github.com','POST',f'/repos/{REPO}/releases/{release_id}/assets?name='+urllib.parse.quote(item['name']),token,stream,item['size'])
        if asset.get('digest') != 'sha256:'+digest or asset['size'] != item['size']:
            raise ValueError('GitHub identity mismatch or immutable name collision')
        row = dict(item, url=asset['browser_download_url'], githubAssetId=asset['id'], githubDigest=asset['digest'])
        result.append(row)
        print(json.dumps(row),flush=True)
    return result


if __name__ == '__main__':
    p=argparse.ArgumentParser(); p.add_argument('--release-id',type=int,required=True); p.add_argument('--manifest',type=Path,required=True)
    args=p.parse_args(); token=sys.stdin.read(1024).strip()
    if not token: raise SystemExit('GitHub credential required on stdin')
    upload(args.release_id,json.loads(args.manifest.read_text()),token)
