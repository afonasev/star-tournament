"""Bounded, content-addressed PNG screenshots belonging to one immutable release."""
import hashlib
from pathlib import Path
import re
import struct
import zlib

MAX_BYTES = 2 * 1024 * 1024
MAX_IMAGES = 12
MAX_WIDTH, MAX_HEIGHT = 1600, 1200
REPO_URL = 'https://github.com/afonasev/star-tournament'


def png_dimensions(data):
    if not 45 <= len(data) <= MAX_BYTES or data[:16] != b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR':
        raise ValueError('Release image must be a bounded PNG')
    width, height = struct.unpack('>II', data[16:24])
    if not (0 < width <= MAX_WIDTH and 0 < height <= MAX_HEIGHT):
        raise ValueError('Release image exceeds 1600x1200 screenshot profile')
    if data[24] != 8 or data[25] not in (2, 6) or data[26:29] != b'\0\0\0':
        raise ValueError('Release PNG requires noninterlaced 8-bit RGB/RGBA')
    # Check all chunks and the bounded decoded scanlines, without an imaging dependency.
    offset = 8; compressed = bytearray(); ended = False
    while offset + 12 <= len(data):
        size = struct.unpack('>I', data[offset:offset+4])[0]
        chunk = data[offset+4:offset+8]; end = offset+12+size
        if end > len(data): raise ValueError('Truncated PNG')
        payload = data[offset+8:offset+8+size]
        if chunk not in (b'IHDR', b'IDAT', b'IEND', b'sRGB', b'gAMA', b'pHYs', b'cHRM') or (chunk == b'IHDR' and offset != 8):
            raise ValueError('PNG must be a single screenshot with stripped metadata')
        if zlib.crc32(chunk+payload) & 0xffffffff != struct.unpack('>I',data[end-4:end])[0]: raise ValueError('PNG CRC mismatch')
        if chunk == b'IDAT': compressed.extend(payload)
        if chunk == b'IEND':
            if size or end != len(data): raise ValueError('Invalid PNG ending')
            ended = True; break
        offset = end
    expected = height * (1 + width * (3 if data[25] == 2 else 4))
    decoder = zlib.decompressobj()
    raw = decoder.decompress(compressed, expected+1)
    if not ended or len(raw) != expected or not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
        raise ValueError('Invalid PNG pixels')
    return width, height


def images(review):
    changes = review.get('changes', [])
    if not isinstance(changes, list): raise ValueError('Review changes list required')
    rows = []
    for change in changes:
        if not isinstance(change, dict) or not isinstance(change.get('images', []), list): raise ValueError('Image list required per change')
        rows.extend(change.get('images', []))
    if len(rows) > MAX_IMAGES: raise ValueError('Too many release illustrations')
    for image in rows:
        if not isinstance(image, dict) or not re.fullmatch('[0-9a-f]{64}', image.get('sha256', '')):
            raise ValueError('Release image SHA256 required')
        for language in ('ru', 'en'):
            caption = image.get(language)
            if not isinstance(caption, str) or not caption.strip() or len(caption) > 300 or re.search(r'[\[\]\\\r\n\x00-\x1f]', caption):
                raise ValueError('Plain paired image captions required')
    return rows


def image_link(image, version, language):
    if not version or not re.fullmatch(r'[0-9A-Za-z.-]+', version): raise ValueError('Release version required for image links')
    name = 'history-' + image['sha256'] + '.png'
    return '![' + image[language].strip() + '](' + REPO_URL + '/releases/download/v' + version + '/' + name + ')'


def prepare(review, directory, version, output):
    prepared = {}
    for image in images(review):
        source = Path(image.get('path', ''))
        if source.is_absolute() or '..' in source.parts or not source.parts:
            raise ValueError('Image path must be relative to the reviewed document')
        path = directory / source
        if path.is_symlink() or not path.resolve().is_relative_to(directory.resolve()) or not path.is_file() or path.stat().st_size > MAX_BYTES:
            raise ValueError('Unsafe release image input')
        data = path.read_bytes(); png_dimensions(data)
        if hashlib.sha256(data).hexdigest() != image['sha256']: raise ValueError('Release image identity mismatch')
        name = 'history-' + image['sha256'] + '.png'
        prepared[name] = data
    target = output / 'history-media'
    for name, data in prepared.items():
        path = target / name
        if path.is_symlink() or (path.exists() and path.read_bytes() != data): raise ValueError('Existing release media identity differs')
    if not prepared: return []
    target.mkdir(parents=True, exist_ok=True)
    for name, data in prepared.items():
        path = target / name; temporary = target / (name + '.tmp'); temporary.write_bytes(data); temporary.replace(path)
    return [target / name for name in sorted(prepared)]
