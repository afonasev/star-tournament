"""Approved Finder layout for an intact, already packaged Velopack application.

This module does not build a Player or publish anything. build() is invoked only
by an explicitly requested installer build, with the packaging Python runtime.
"""
import json
import hashlib
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import shutil
import posixpath
import stat
import subprocess
import tempfile
import unicodedata
import zipfile

from release_compatibility import validate as validate_compatibility

APP_NAME = 'Star Tournament.app'
WINDOW = (1088, 528)
ICON_LOCATIONS = {APP_NAME: (314, 230), 'Applications': (762, 230)}


def outer_plist(config):
    """Pinned Velopack 1.2.158 defaults, plus the actual Player OS floor."""
    minimum = validate_compatibility(config['compatibility'], 'osx')['minimumOsVersion']
    app_id, version = config['appId'], config['version']
    return dict(CFBundleName='Star Tournament', CFBundleExecutable='StarTournamentLauncher',
                CFBundleIdentifier='com.'+app_id+'.'+app_id, CFBundlePackageType='APPL',
                CFBundleShortVersionString=version.split('-')[0], CFBundleVersion=version,
                CFBundleSignature='????', NSPrincipalClass='NSApplication',
                NSHighResolutionCapable=True, CFBundleIconFile='app-icon.icns',
                LSMinimumSystemVersion=minimum)


def validate_portable(archive):
    """Check every ZIP path/link before ditto can write any bundle bytes."""
    nodes, folded, links, kinds = {}, set(), {}, {}
    with zipfile.ZipFile(archive) as source:
        for entry in source.infolist():
            path = entry.filename.rstrip('/')
            parts = PurePosixPath(path).parts
            if (not parts or '\\' in path or '\x00' in path or path.startswith('/') or
                    '..' in parts or str(PurePosixPath(path)) != path):
                raise ValueError('Unsafe portable archive path')
            if parts[0] not in (APP_NAME, '__MACOSX'):
                raise ValueError('Portable archive must contain exactly the complete outer app')
            if parts[0] == '__MACOSX' and len(parts)>1 and parts[1] not in (APP_NAME, '._'+APP_NAME):
                raise ValueError('Resource fork belongs to a different bundle')
            canonical = unicodedata.normalize('NFC', path).casefold()
            if canonical in folded: raise ValueError('Duplicate portable archive path')
            folded.add(canonical)
            mode = (entry.external_attr >> 16) & 0xffff
            kind = stat.S_IFMT(mode)
            if kind not in (0, stat.S_IFREG, stat.S_IFDIR, stat.S_IFLNK):
                raise ValueError('Special filesystem node in portable archive')
            nodes[path] = entry.is_dir() or kind == stat.S_IFDIR
            kinds[canonical] = nodes[path]
            if kind == stat.S_IFLNK:
                if parts[0] == '__MACOSX' or entry.file_size > 4096:
                    raise ValueError('Invalid portable symlink')
                target = source.read(entry).decode('utf-8')
                if not target or target.startswith('/') or '\\' in target or '\x00' in target:
                    raise ValueError('Unsafe portable symlink target')
                links[path] = posixpath.normpath(posixpath.join(posixpath.dirname(path), target))
        if not any(p.startswith(APP_NAME+'/Contents/') for p in nodes):
            raise ValueError('Complete app contents missing')
        for path in nodes:
            for parent in PurePosixPath(path).parents:
                name = str(parent)
                canonical = unicodedata.normalize('NFC', name).casefold()
                if canonical in kinds and not kinds[canonical]:
                    raise ValueError('Portable file or symlink cannot own child entries')
        for path in links:
            target, seen = path, set()
            while True:
                prefixes = [str(p) for p in reversed(PurePosixPath(target).parents)] + [target]
                link = next((p for p in prefixes if p in links), None)
                if link is None: break
                if link in seen: raise ValueError('Portable symlink cycle')
                seen.add(link)
                target = posixpath.normpath(links[link]+target[len(link):])
            if not target.startswith(APP_NAME+'/') or (target not in nodes and not any(p.startswith(target+'/') for p in nodes)):
                raise ValueError('Portable symlink escapes bundle or is missing')


def from_portable(archive, output, runtime, version):
    validate_portable(archive)
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.dmg-unpack-', dir=output.parent) as directory:
        subprocess.run(['/usr/bin/ditto', '-x', '-k', str(Path(archive).resolve()), directory], check=True)
        return build(Path(directory)/APP_NAME, output, runtime, version)


def prepare_bundle(stage, output, config, icon):
    """Keep code in MacOS and non-code in Resources, without xattr signatures.

    Directory/file links retain launcher/recovery paths. Velopack preserves
    links using its existing .__symlink update encoding; reader2 stays valid.
    """
    app = Path(output)/APP_NAME
    macos = app/'Contents/MacOS'
    resources = app/'Contents/Resources'
    payload = resources/'payload'
    macos.mkdir(parents=True)
    payload.mkdir(parents=True)
    (app/'Contents/Info.plist').write_bytes(plistlib.dumps(outer_plist(config)))
    shutil.copyfile(icon, resources/'app-icon.icns')
    for path in Path(stage).iterdir():
        # .NET native libraries/apphost already have embedded ad-hoc signatures.
        if path.is_file() and (path.name == 'StarTournamentLauncher' or path.suffix == '.dylib'):
            path.rename(macos/path.name)
        else:
            path.rename(payload/path.name)
            (macos/path.name).symlink_to('../Resources/payload/'+path.name+('/' if (payload/path.name).is_dir() else ''))
    return app


def verify_signature(app):
    """Integrity only: ad-hoc signing is not Developer ID or notarization."""
    result = subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict',
                             '--verbose=4', str(app)], capture_output=True)
    if result.returncode:
        error = subprocess.CalledProcessError(result.returncode, result.args, result.stdout, result.stderr)
        error.add_note(result.stderr.decode(errors='replace'))
        raise error


def restore_update_links(app):
    """Match pinned Velopack extraction before checking its signature seal."""
    app = Path(app).resolve()
    for encoded in app.rglob('*.__symlink'):
        link = encoded.with_name(encoded.name[:-len('.__symlink')])
        if encoded.is_symlink() or encoded.stat().st_size > 4096 or link.exists() or link.is_symlink():
            raise ValueError('Invalid encoded update link')
        target = encoded.read_text()
        if not target or target.startswith('/') or '\\' in target or '\x00' in target:
            raise ValueError('Unsafe encoded update link')
        if not (link.parent/target).resolve().is_relative_to(app):
            raise ValueError('Update link escapes bundle')
        encoded.unlink()
        link.symlink_to(target)
    for path in app.rglob('*'):
        if path.is_symlink() and (not path.resolve().is_relative_to(app) or not path.exists()):
            raise ValueError('Invalid restored update link')


def verify_packages(output):
    """Fail closed on extracted update, portable and compatibility installer."""
    output = Path(output)
    for pattern in ('*.nupkg', '*Portable.zip'):
        archives = list(output.glob(pattern))
        if len(archives) != 1:
            raise ValueError('Exactly one macOS '+pattern+' required for signature verification')
        with tempfile.TemporaryDirectory(prefix='.signature-', dir=output.parent) as temporary:
            root = Path(temporary)
            subprocess.run(['/usr/bin/ditto', '-x', '-k', str(archives[0]), str(root)], check=True)
            if pattern == '*.nupkg':
                app = root/'Verified.app'
                (root/'lib/app').rename(app)
            else:
                app = root/APP_NAME
            if pattern == '*.nupkg': restore_update_links(app)
            verify_signature(app)
    for package in output.glob('*.pkg'):
        with tempfile.TemporaryDirectory(prefix='.signature-', dir=output.parent) as temporary:
            root = Path(temporary)/'expanded'
            subprocess.run(['/usr/sbin/pkgutil', '--expand-full', str(package), str(root)], check=True)
            apps = [p for p in root.rglob('*.app') if (p/'Contents/MacOS/release.json').is_file()]
            if len(apps) != 1:
                raise ValueError('Exactly one launcher bundle required in PKG')
            verify_signature(apps[0])


def inspect_app(app, runtime, version):
    app = Path(app)
    if runtime not in ('osx-arm64', 'osx-x64') or not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', version):
        raise ValueError('Explicit macOS runtime and version required')
    if app.is_symlink() or not app.is_dir() or app.suffix != '.app':
        raise ValueError('Complete application directory required')
    root = app.resolve()
    # Internal framework/Velopack links are legitimate; escaping links are not.
    for path in app.rglob('*'):
        if path.is_symlink() and not path.resolve().is_relative_to(root):
            raise ValueError('Application symlink escapes bundle')
    contents = app/'Contents'
    with (contents/'Info.plist').open('rb') as stream:
        info = plistlib.load(stream)
    base = contents/'MacOS'
    config = json.loads((base/'release.json').read_text())
    if (config.get('platform'), config.get('architecture'), config.get('version'), config.get('appId')) != (
            'osx', runtime.split('-')[1], version, 'tech.afonasev.star-tournament.'+runtime):
        raise ValueError('Application release identity mismatch')
    requirements = validate_compatibility(config.get('compatibility'), 'osx')
    minimum = requirements['minimumOsVersion']
    if info.get('LSMinimumSystemVersion') != minimum:
        raise ValueError('Finder minimum OS must match authenticated payload requirements')
    if info.get('CFBundleExecutable') != 'StarTournamentLauncher':
        raise ValueError('Finder must start the launcher, not the nested Player')
    if info.get('CFBundleVersion') != version or info.get('CFBundleShortVersionString') != version.split('-')[0]:
        raise ValueError('Finder release version mismatch')
    player = config.get('gameExecutable', '')
    player_path = PurePosixPath(player)
    if (not player.startswith('game/Player.app/Contents/MacOS/') or '..' in player_path.parts or
            player_path.as_posix() != player or not (base/player).resolve().is_relative_to(root)):
        raise ValueError('Nested Player executable required')
    for executable in [base/'StarTournamentLauncher', base/'UpdateMac', base/player]:
        if not executable.is_file() or not os.access(executable, os.X_OK):
            raise ValueError('Runnable launcher, updater and Player required')
    if not (base/'update-public.pem').is_file() or not (contents/'Resources/sq.version').is_file():
        raise ValueError('Pinned update trust and Velopack bundle metadata required')
    if (base/'update-public.pem').read_bytes() != Path(__file__).with_name('update-public.pem').read_bytes():
        raise ValueError('Application update key differs from distribution trust')
    return config


def settings(app, background):
    return dict(format='UDZO', filesystem='HFS+', files=[(str(app), APP_NAME)],
                symlinks={'Applications': '/Applications'}, icon_locations=ICON_LOCATIONS.copy(),
                background=str(background), window_rect=((100, 100), WINDOW),
                default_view='icon-view', icon_size=140, text_size=20,
                show_status_bar=False, show_tab_view=False, show_toolbar=False,
                show_pathbar=False, show_sidebar=False, include_icon_view_settings=True,
                include_list_view_settings=False, arrange_by=None)


def render_background(output, config):
    # The icons and their filenames are drawn by Finder, not painted into this image.
    from PIL import Image, ImageDraw, ImageFont
    image = Image.new('RGB', WINDOW, '#fbfbfd')
    draw = ImageDraw.Draw(image)
    font_path = '/System/Library/Fonts/Supplemental/Arial.ttf'
    bold_path = '/System/Library/Fonts/Supplemental/Arial Bold.ttf'
    def text(y, value, size, color, bold=False):
        font = ImageFont.truetype(bold_path if bold else font_path, size)
        draw.text((WINDOW[0]/2, y), value, font=font, fill=color, anchor='mt')
    text(44, 'Перетащите игру в «Программы»', 27, '#252936', True)
    text(88, 'Затем откройте Star Tournament из этой папки', 20, '#6b7281')
    draw.line([(460, 230), (590, 230)], fill='#979fad', width=4)
    draw.line([(565, 208), (590, 230), (565, 252)], fill='#979fad', width=4)
    arch = {'arm64': 'Apple Silicon', 'x64': 'Intel x64'}[config['architecture']]
    text(404, 'Afonasev · macOS '+config['compatibility']['minimumOsVersion']+'+ · '+arch, 17, '#7b8190')
    draw.rounded_rectangle((229, 444, 859, 488), radius=7, fill='#fff6df', outline='#f8d695')
    text(457, 'Тестовая поставка · без подписи Apple', 18, '#9c6500')
    image.save(output, 'PNG')


def bundle_identity(app):
    result = {'.': ('directory', stat.S_IMODE(Path(app).stat().st_mode))}
    for path in Path(app).rglob('*'):
        relative = path.relative_to(app).as_posix()
        if path.is_symlink(): result[relative] = ('link', os.readlink(path))
        elif path.is_file():
            with path.open('rb') as stream: digest = hashlib.file_digest(stream, 'sha256').hexdigest()
            result[relative] = ('file', stat.S_IMODE(path.stat().st_mode), digest)
        elif path.is_dir(): result[relative] = ('directory', stat.S_IMODE(path.stat().st_mode))
        else: raise ValueError('Unexpected application filesystem node')
    return result


def verify_image(image, app, *, mount_parent=None):
    """Read back the actual mounted image before releasing its immutable output."""
    temporary = Path(tempfile.mkdtemp(prefix='.dmg-readback-', dir=mount_parent or Path(image).parent))
    mount = temporary/'volume'; mount.mkdir()
    attached = False
    try:
        retained_image = temporary/'readback.dmg'
        os.link(image, retained_image)
        subprocess.run(['/usr/bin/hdiutil','attach','-readonly','-nobrowse','-mountpoint',str(mount),str(retained_image)], check=True, capture_output=True)
        attached = True
        installed = mount/APP_NAME
        if not installed.is_dir() or installed.is_symlink(): raise ValueError('DMG application missing')
        target = mount/'Applications'
        if not target.is_symlink() or os.readlink(target) != '/Applications':
            raise ValueError('DMG Applications link mismatch')
        verify_signature(installed)
        if bundle_identity(installed) != bundle_identity(app):
            raise ValueError('DMG bundle differs from complete portable input')
    finally:
        try:
            if attached or os.path.ismount(mount):
                subprocess.run(['/usr/bin/hdiutil','detach',str(mount)], check=True, capture_output=True)
                attached = False
        finally:
            # Failed detach retains the owned mount path for explicit recovery;
            # never recurse into a volume that may still be mounted.
            if not attached and not os.path.ismount(mount): shutil.rmtree(temporary)


def build(app, output, runtime, version, *, builder=None, verifier=None):
    """Create an unsigned test DMG; retain the intact input app byte-for-byte."""
    app, output = Path(app), Path(output)
    if output.exists() or output.is_symlink():
        raise ValueError('Immutable DMG output already exists')
    config = inspect_app(app, runtime, version)
    verify_signature(app)
    # Signing/notarization needs a distinct, actually verified delivery policy.
    # Both test and production channels currently use the explicitly approved
    # unsigned test distribution, so the notice must never imply Apple trust.
    output.parent.mkdir(parents=True, exist_ok=True)
    if builder is None:
        from dmgbuild import build_dmg
        builder = build_dmg
    with tempfile.TemporaryDirectory(prefix='.dmg-stage-', dir=output.parent) as temporary:
        stage = Path(temporary)
        background, candidate = stage/'background.png', stage/'candidate.dmg'
        render_background(background, config)
        builder(str(candidate), 'Star Tournament · '+version, settings=settings(app.resolve(), background))
        if not candidate.is_file() or candidate.is_symlink() or candidate.stat().st_size == 0:
            raise ValueError('DMG builder produced no disk image')
        if verifier is not None: verifier(candidate, app)
        else: verify_image(candidate, app, mount_parent=output.parent)
        # Link gives no-overwrite semantics even if an output appeared mid-build.
        os.link(candidate, output)
    return output
