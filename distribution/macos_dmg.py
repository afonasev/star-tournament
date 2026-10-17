"""Approved Finder layout for an intact, already packaged Velopack application.

This module does not build a Player or publish anything. build() is invoked only
by an explicitly requested installer build, with the packaging Python runtime.
"""
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import tempfile

from release_compatibility import validate as validate_compatibility

APP_NAME = 'Star Tournament.app'
WINDOW = (1088, 528)
ICON_LOCATIONS = {APP_NAME: (314, 230), 'Applications': (762, 230)}


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
    if info.get('CFBundleShortVersionString') != version:
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


def build(app, output, runtime, version, *, builder=None):
    """Create an unsigned test DMG; retain the intact input app byte-for-byte."""
    app, output = Path(app), Path(output)
    if output.exists() or output.is_symlink():
        raise ValueError('Immutable DMG output already exists')
    config = inspect_app(app, runtime, version)
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
        # Link gives no-overwrite semantics even if an output appeared mid-build.
        os.link(candidate, output)
    return output
