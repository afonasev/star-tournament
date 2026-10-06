#!/usr/bin/env python3
"""Diskless compatibility downloads for fixed legacy paths, backed by GitHub Releases."""
import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import threading
import urllib.request
import urllib.parse

REPOSITORY_PREFIX = '/afonasev/star-tournament/releases/download/'
CDN_HOSTS = {'release-assets.githubusercontent.com', 'objects.githubusercontent.com'}


def allowed_url(url, initial=False):
    u = urllib.parse.urlsplit(url)
    if u.scheme != 'https' or u.port not in (None, 443) or u.username or u.password or u.fragment:
        return False
    return (u.hostname == 'github.com' and u.path.startswith(REPOSITORY_PREFIX) and not u.query) or (not initial and u.hostname in CDN_HOSTS)


class ReleaseRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        count = getattr(req, 'release_redirects', 0)
        if count >= 3 or not allowed_url(newurl):
            raise ValueError('Untrusted or excessive release redirect')
        result = super().redirect_request(req, fp, code, msg, headers, newurl)
        result.release_redirects = count + 1
        return result


def read_manifest(path):
    data = json.loads(Path(path).read_text())
    for route, item in data.items():
        if not route.startswith('/desktop/test/') or '?' in route or '..' in route or not allowed_url(item['url'], initial=True):
            raise ValueError('Invalid fixed release route')
        if item['size'] <= 0 or len(item['sha256']) != 64:
            raise ValueError('Invalid release identity')
    return data


def serve(manifest, port):
    slots = threading.BoundedSemaphore(4)
    class Handler(BaseHTTPRequestHandler):
        def do_HEAD(self): self.download(head=True)
        def do_GET(self): self.download(head=False)
        def download(self, head):
            # Reload atomically-written small metadata for publication; never accept a URL from a caller.
            try: item = read_manifest(manifest).get(urllib.parse.urlsplit(self.path).path)
            except Exception: self.send_error(503); return
            if item is None: self.send_error(404); return
            if not slots.acquire(blocking=False): self.send_error(503); return
            sent = False
            try:
                req = urllib.request.Request(item['url'], method='HEAD' if head else 'GET', headers={'User-Agent': 'StarTournament-LegacyRelay/1'})
                with urllib.request.build_opener(ReleaseRedirects()).open(req, timeout=45) as source:
                    if source.status != 200 or int(source.headers.get('Content-Length', '-1')) != item['size']:
                        raise ValueError('GitHub asset size/status mismatch')
                    self.send_response(200)
                    self.send_header('Content-Type', 'application/octet-stream')
                    self.send_header('Content-Length', str(item['size']))
                    self.send_header('Cache-Control', 'public, max-age=31536000, immutable')
                    self.end_headers(); sent = True
                    if not head:
                        received = 0
                        while chunk := source.read(128 * 1024):
                            received += len(chunk)
                            if received > item['size']: raise ValueError('Oversized GitHub asset')
                            self.wfile.write(chunk)
                        if received != item['size']: raise ValueError('Truncated GitHub asset')
            except (BrokenPipeError, ConnectionResetError): pass
            except Exception:
                if not sent: self.send_error(502)
                self.close_connection = True
                self.log_error('GitHub release transfer failed for known route')
            finally: slots.release()
    ThreadingHTTPServer(('127.0.0.1', port), Handler).serve_forever()


if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('--manifest', required=True); p.add_argument('--port', type=int, default=4192)
    args = p.parse_args(); read_manifest(args.manifest); serve(args.manifest, args.port)
