"""Verify focused invocations preserve the canonical full-gate artifacts."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


class UnityToolsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / 'unity').mkdir()
        shutil.copyfile(Path(__file__).resolve().parents[1] / 'unity/tools.sh', self.root / 'unity/tools.sh')
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        subprocess.run(['git', '-C', str(self.root), '-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '--allow-empty', '-qm', 'baseline'], check=True)
        self.runner = self.root / 'runner'
        self.runner.write_text('#!/usr/bin/env python3\nimport json,os,sys\nfrom pathlib import Path\nPath(os.environ["ARGS_FILE"]).write_text(json.dumps(sys.argv[1:]))\n')
        self.runner.chmod(0o755)
        self.args_file = self.root / 'args.json'

    def tearDown(self):
        self.temp.cleanup()

    def invoke(self, *args):
        return subprocess.run(['sh', str(self.root / 'unity/tools.sh'), *args], env={**os.environ, 'UNITY_RUNNER': str(self.runner), 'ARGS_FILE': str(self.args_file)}, text=True, capture_output=True)

    def test_focused_preserves_literal_filter_and_separates_each_run(self):
        filter_text = 'Some.Tests.Method(1);Other.* with spaces'
        paths = []
        for platform, command in [('EditMode', 'test-edit-filter'), ('PlayMode', 'test-play-filter'), ('EditMode', 'test-edit-filter')]:
            result = self.invoke(command, filter_text)
            self.assertEqual(result.returncode, 0, result.stderr)
            args = json.loads(self.args_file.read_text())
            self.assertEqual(args[args.index('-testFilter') + 1], filter_text)
            self.assertEqual(args[args.index('-testPlatform') + 1], platform)
            path = Path(args[args.index('-testResults') + 1]).parent
            paths.append(path)
            self.assertIn('focused-', path.name)
            self.assertEqual(Path(args[args.index('-logFile') + 1]).parent, path)
            self.assertIn('NOT a full', result.stdout)
            self.assertIn('filter=' + filter_text, (path / 'scope.txt').read_text())
        self.assertEqual(len(set(paths)), 3)

    def test_invalid_focused_arguments_never_launch_unity(self):
        for args in [('test-edit-filter',), ('test-play-filter', ''), ('test-edit-filter', 'fixture', 'extra')]:
            self.assertEqual(self.invoke(*args).returncode, 2)
            self.assertFalse(self.args_file.exists())

    def test_full_gates_still_use_canonical_paths_without_filter(self):
        for command, stem in [('test-edit', 'editmode'), ('test-play', 'playmode')]:
            self.assertEqual(self.invoke(command).returncode, 0)
            args = json.loads(self.args_file.read_text())
            self.assertNotIn('-testFilter', args)
            self.assertEqual(Path(args[args.index('-testResults') + 1]).name, stem + '.xml')
            self.assertEqual(Path(args[args.index('-testResults') + 1]).parent.name, 'unity-evidence')


if __name__ == '__main__':
    unittest.main()
