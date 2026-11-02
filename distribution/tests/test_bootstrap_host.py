import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile
import unittest
import uuid

import bootstrap_host
import macos_dmg


class WindowsHost(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.current = self.root/'current'; self.current.mkdir()
        (self.root/'maintenance').mkdir()
        self.names = ['StarTournamentLauncher.exe', 'StarTournamentLauncher.dll',
                      'StarTournamentLauncher.deps.json', 'StarTournamentLauncher.runtimeconfig.json',
                      'update-public.pem', 'coreclr.dll', 'release.json']
        for name in self.names: (self.current/name).write_bytes(name.encode())
        (self.current/'release.json').write_text(json.dumps({'bootstrapProtocol': 1}))
        (self.current/'game').mkdir(); (self.current/'game/Player.exe').write_bytes(b'not bootstrap')

    def test_host_keeps_its_runtime_after_complete_current_replacement(self):
        bootstrap_host.install_windows(self.root)
        host = self.root/'maintenance/bootstrap'
        before = {p.name: p.read_bytes() for p in host.iterdir()}
        self.assertEqual(set(before), set(self.names))
        shutil.rmtree(self.current); self.current.mkdir()
        (self.current/'StarTournamentLauncher.exe').write_bytes(b'incompatible replacement')
        self.assertEqual(before, {p.name:p.read_bytes() for p in host.iterdir()})

    def test_incomplete_runtime_never_creates_host(self):
        (self.current/'StarTournamentLauncher.dll').unlink()
        with self.assertRaises(ValueError): bootstrap_host.install_windows(self.root)
        self.assertFalse((self.root/'maintenance/bootstrap').exists())

    def test_existing_or_redirected_host_is_never_overwritten(self):
        (self.root/'maintenance/bootstrap').symlink_to(self.current, target_is_directory=True)
        with self.assertRaises(ValueError): bootstrap_host.install_windows(self.root)
        self.assertEqual((self.current/'StarTournamentLauncher.exe').read_bytes(), b'StarTournamentLauncher.exe')

    def test_redirected_runtime_rejected_before_writing(self):
        (self.current/'coreclr.dll').unlink()
        (self.current/'coreclr.dll').symlink_to(self.current/'StarTournamentLauncher.dll')
        with self.assertRaises(ValueError): bootstrap_host.install_windows(self.root)
        self.assertFalse((self.root/'maintenance/bootstrap').exists())


@unittest.skipUnless(platform.system() == 'Darwin', 'macOS compiler and native entry required')
class MacEntry(unittest.TestCase):
    def setUp(self):
        # Native routing only; fake recorders cannot start Unity or apply an update.
        self.temp = tempfile.TemporaryDirectory(dir=Path.cwd()/'.local')
        self.addCleanup(self.temp.cleanup)
        self.app = Path(self.temp.name)/"Star ' Tournament.app"
        self.bin = self.app/'Contents/MacOS'; self.bin.mkdir(parents=True)
        self.app_id = 'tech.fixture.bootstrap.'+uuid.uuid4().hex
        bootstrap_host.compile_macos(self.bin, 'osx-arm64' if platform.machine() == 'arm64' else 'osx-x64', self.app_id)
        root_id = hashlib.sha256(str(self.app.resolve()).encode()).hexdigest()
        self.store = Path.home()/'Library/Application Support/StarTournament/launcher'/f'{self.app_id}.recovery-{root_id}'
        self.addCleanup(lambda: shutil.rmtree(self.store) if self.store.exists() else None)
        self.host = self.store/'host/payload/Contents/MacOS/StarTournamentLauncher'
        self.output = Path(self.temp.name)/'argv.txt'
        self.args = ['-muted', '-noaudio', "профиль ' two"]
        self.record(self.bin/'StarTournamentLauncher')

    def record(self, path):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text('#!/bin/sh\nprintf "%s\\n" "$@" > '+"'"+str(self.output).replace("'", "'\\''")+"'\n")
        path.chmod(0o755)

    def launch(self):
        return subprocess.run([str(self.bin/'StarTournamentBootstrap'), *self.args], capture_output=True)

    def test_initial_entry_preserves_argv_for_seed_launcher(self):
        self.assertEqual(self.launch().returncode, 0)
        self.assertEqual(self.output.read_text().splitlines(), self.args)

    def test_stable_entry_bypasses_missing_incompatible_payload_launcher(self):
        self.record(self.host); (self.bin/'StarTournamentLauncher').unlink()
        self.assertEqual(self.launch().returncode, 0)
        self.assertEqual(self.output.read_text().splitlines(), ['--bootstrap-payload', str(self.bin.resolve()), *self.args])

    def test_damaged_established_host_cannot_fall_back(self):
        self.host.parent.mkdir(parents=True)
        self.host.with_name('release.json').write_text('{}')
        self.assertNotEqual(self.launch().returncode, 0)
        self.assertFalse(self.output.exists())

    def test_linked_host_cannot_execute(self):
        self.host.parent.mkdir(parents=True)
        self.host.symlink_to(self.bin/'StarTournamentLauncher')
        self.assertNotEqual(self.launch().returncode, 0)
        self.assertFalse(self.output.exists())


class BundleContract(unittest.TestCase):
    def test_bootstrap_protocol_chooses_independent_finder_entry(self):
        config = dict(appId='tech.fixture', platform='osx', version='1.0.0', bootstrapProtocol=1,
                      compatibility=dict(minimumOsVersion='12.0', minimumUpdaterProtocol=2,
                                         payloadFormat='velopack-full-v1', userDataPolicy='profiles2-lab3-prefs1'))
        self.assertEqual(macos_dmg.outer_plist(config)['CFBundleExecutable'], 'StarTournamentBootstrap')
