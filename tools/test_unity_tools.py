"""Verify focused invocations preserve the confirmation and history isolation boundaries."""
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
        source = Path(__file__).resolve().parents[1]
        shutil.copyfile(source / 'unity/tools.sh', self.root / 'unity/tools.sh')
        (self.root / 'tools').mkdir()
        shutil.copyfile(source / 'tools/unity.sh', self.root / 'tools/unity.sh')
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        subprocess.run(['git', '-C', str(self.root), '-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '--allow-empty', '-qm', 'baseline'], check=True)
        self.runner = self.root / 'runner'
        self.runner.write_text('#!/usr/bin/env python3\nimport json,os,sys\nfrom pathlib import Path\nPath(os.environ["ARGS_FILE"]).write_text(json.dumps({"args":sys.argv[1:], "history":os.environ.get("STAR_TOURNAMENT_QA_LAB_HISTORY"), "stage_history":os.environ.get("STAR_TOURNAMENT_LAB_RELEASE_HISTORY")}))\n')
        self.runner.chmod(0o755)
        self.args_file = self.root / 'args.json'

    def tearDown(self):
        self.temp.cleanup()

    def invoke(self, *args, env=None, direct=False):
        environment = {k: v for k, v in os.environ.items()
                       if k not in ('CONFIRM_FULL_TESTS', 'STAR_TOURNAMENT_QA_LAB_HISTORY')}
        environment.update({'UNITY_RUNNER': str(self.runner), 'ARGS_FILE': str(self.args_file)})
        environment.update(env or {})
        return subprocess.run(['sh', str(self.root / ('tools/unity.sh' if direct else 'unity/tools.sh')), *args], env=environment, text=True, capture_output=True)

    def test_focused_preserves_literal_filter_and_separates_each_run(self):
        filter_text = 'Some.Tests.Method(1);Other.* with spaces'
        paths = []
        for platform, command in [('EditMode', 'test-edit-filter'), ('PlayMode', 'test-play-filter'), ('EditMode', 'test-edit-filter')]:
            result = self.invoke(command, filter_text)
            self.assertEqual(result.returncode, 0, result.stderr)
            receipt = json.loads(self.args_file.read_text())
            args = receipt["args"]
            self.assertEqual(args[args.index('-testFilter') + 1], filter_text)
            self.assertEqual(args[args.index('-testPlatform') + 1], platform)
            path = Path(args[args.index('-testResults') + 1]).parent
            paths.append(path)
            self.assertIn('focused-', path.name)
            self.assertEqual(receipt['history'], str(path / 'qa-lab-history.json'))
            self.assertFalse(Path(receipt['history']).exists())
            self.assertIn('lab_history=' + receipt['history'], (path / 'scope.txt').read_text())
            self.assertEqual(Path(args[args.index('-logFile') + 1]).parent, path)
            self.assertIn('NOT a full', result.stdout)
            self.assertIn('filter=' + filter_text, (path / 'scope.txt').read_text())
        self.assertEqual(len(set(paths)), 3)

    def test_batch_routes_are_silent_and_wrappers_default_to_silent_with_human_opt_in(self):
        for command in [('prepare',), ('build',), ('test-edit-filter', 'fixture'), ('test-play-filter', 'fixture')]:
            self.assertEqual(self.invoke(*command).returncode, 0)
            args = json.loads(self.args_file.read_text())['args']
            self.assertEqual(args.count('-noaudio'), 1)
            self.assertEqual(args.count('-muted'), 1)
        self.assertEqual(self.invoke('editor').returncode, 0)
        editor_args = json.loads(self.args_file.read_text())['args']
        self.assertEqual(editor_args.count('-noaudio'), 1)
        self.assertEqual(editor_args.count('-muted'), 1)
        self.assertEqual(self.invoke('editor', '-noaudio', '-humanAudio').returncode, 0)
        editor_args = json.loads(self.args_file.read_text())['args']
        self.assertEqual(editor_args.count('-noaudio'), 1)
        self.assertEqual(editor_args.count('-muted'), 1)
        self.assertIn('-humanAudio', json.loads(self.args_file.read_text())['args'])
        self.assertEqual(self.invoke('editor', '-humanAudio').returncode, 0)
        editor_args = json.loads(self.args_file.read_text())['args']
        self.assertNotIn('-noaudio', editor_args)
        self.assertNotIn('-muted', editor_args)
        self.assertIn('-humanAudio', editor_args)
        self.assertEqual(self.invoke('editor', '-humanAudio', '-musicReview').returncode, 0)
        review_args = json.loads(self.args_file.read_text())['args']
        self.assertEqual(review_args.count('-noaudio'), 1)
        self.assertEqual(review_args.count('-muted'), 1)
        import plistlib
        app = self.root / 'fake.app'
        (app / 'Contents').mkdir(parents=True)
        with (app / 'Contents/Info.plist').open('wb') as file:
            plistlib.dump({'CFBundleExecutable': 'Fake Player'}, file)
        self.assertEqual(self.invoke('run', env={'UNITY_PLAYER_APP': str(app)}).returncode, 0)
        player_args = json.loads(self.args_file.read_text())['args']
        self.assertEqual(player_args.count('-noaudio'), 1)
        self.assertEqual(player_args.count('-muted'), 1)
        self.assertEqual(self.invoke('run', '-humanAudio', env={'UNITY_PLAYER_APP': str(app)}).returncode, 0)
        player_args = json.loads(self.args_file.read_text())['args']
        self.assertNotIn('-noaudio', player_args)
        self.assertNotIn('-muted', player_args)
        self.assertIn('-humanAudio', player_args)
        self.assertEqual(self.invoke('run', '-humanAudio', '-musicReview', env={'UNITY_PLAYER_APP': str(app)}).returncode, 0)
        review_args = json.loads(self.args_file.read_text())['args']
        self.assertEqual(review_args.count('-noaudio'), 1)
        self.assertEqual(review_args.count('-muted'), 1)

        direct_editor = self.invoke('shared', direct=True)
        self.assertEqual(direct_editor.returncode, 0, direct_editor.stderr)
        direct_args = json.loads(self.args_file.read_text())['args']
        self.assertEqual(direct_args.count('-noaudio'), 1)
        self.assertEqual(direct_args.count('-muted'), 1)
        direct_human = self.invoke('shared', '-humanAudio', direct=True)
        self.assertEqual(direct_human.returncode, 0, direct_human.stderr)
        direct_args = json.loads(self.args_file.read_text())['args']
        self.assertNotIn('-noaudio', direct_args)
        self.assertNotIn('-muted', direct_args)
        self.assertIn('-humanAudio', direct_args)
        direct_review = self.invoke('shared', '-humanAudio', '-musicReview', direct=True)
        self.assertEqual(direct_review.returncode, 0, direct_review.stderr)
        review_args = json.loads(self.args_file.read_text())['args']
        self.assertEqual(review_args.count('-noaudio'), 1)
        self.assertEqual(review_args.count('-muted'), 1)

        player = self.invoke('player', '/fake/player', direct=True)
        self.assertEqual(player.returncode, 0, player.stderr)
        player_args = json.loads(self.args_file.read_text())['args']
        self.assertEqual(player_args.count('-noaudio'), 1)
        self.assertEqual(player_args.count('-muted'), 1)

    def test_invalid_focused_arguments_never_launch_unity(self):
        for args in [('test-edit-filter',), ('test-play-filter', ''), ('test-edit-filter', 'fixture', 'extra')]:
            self.assertEqual(self.invoke(*args).returncode, 2)
            self.assertFalse(self.args_file.exists())

    def test_full_gates_still_use_canonical_paths_without_filter(self):
        for command, stem in [('test-edit', 'editmode'), ('test-play', 'playmode')]:
            self.assertEqual(self.invoke(command, env={'CONFIRM_FULL_TESTS': '1'}).returncode, 0)
            receipt = json.loads(self.args_file.read_text())
            args = receipt["args"]
            self.assertNotIn('-testFilter', args)
            self.assertEqual(Path(args[args.index('-testResults') + 1]).name, stem + '.xml')
            self.assertEqual(Path(args[args.index('-testResults') + 1]).parent.name, 'unity-evidence')

    def test_unconfirmed_full_routes_never_launch_runner(self):
        for command in ('test-edit', 'test-play'):
            for confirmation in (None, '', '0', 'true'):
                with self.subTest(command=command, confirmation=confirmation):
                    env = {} if confirmation is None else {'CONFIRM_FULL_TESTS': confirmation}
                    result = self.invoke(command, env=env)
                    self.assertEqual(result.returncode, 2)
                    self.assertIn('human confirmation', result.stderr)
                    self.assertFalse(self.args_file.exists())
        self.assertFalse((self.root / '.local').exists())

    def test_explicit_history_override_is_preserved(self):
        history = self.root / 'history with spaces.json'
        history.write_bytes(b'user history must remain byte identical')
        for command in ('test-edit-filter', 'test-play-filter', 'test-edit', 'test-play'):
            args = (command, 'SomeFixture') if command.endswith('-filter') else (command,)
            result = self.invoke(*args, env={'CONFIRM_FULL_TESTS': '1',
                                            'STAR_TOURNAMENT_QA_LAB_HISTORY': str(history)})
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(json.loads(self.args_file.read_text())['history'], str(history))
            self.assertEqual(history.read_bytes(), b'user history must remain byte identical')

    def test_project_staging_is_silent_and_only_routes_to_explicit_editor_source_tool(self):
        for extra in [(), (str(self.root / 'unity/DesignLab/authoring-history-v1.json'),)]:
            result = self.invoke('lab-stage-releases', *extra)
            self.assertEqual(result.returncode, 0, result.stderr)
            receipt = json.loads(self.args_file.read_text())
            args = receipt['args']
            self.assertEqual(args[args.index('-executeMethod') + 1],
                             'StarTournament.ProvingGround.Editor.LabReleaseBuild.PromoteMarked')
            self.assertEqual(receipt['stage_history'], extra[0] if extra else '')
            self.assertIsNone(receipt['history'])
            self.assertIn('-noaudio', args)
            self.assertIn('-muted', args)
            self.assertNotIn('-runTests', args)
            self.assertEqual(args[0], '--shared')
        self.args_file.unlink()
        self.assertEqual(self.invoke('lab-stage-releases', 'one', 'two').returncode, 2)
        self.assertFalse(self.args_file.exists())

    def test_non_test_routes_do_not_inject_history(self):
        for command in ('prepare', 'build', 'editor'):
            result = self.invoke(command)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIsNone(json.loads(self.args_file.read_text())['history'])

    def test_direct_wrapper_guards_unfiltered_tests(self):
        for args in [('-runTests',), ('-runTests', '-testFilter', ''),
                     ('-runTests', '-testFilter')]:
            self.assertEqual(self.invoke('shared', *args, direct=True).returncode, 2)
            self.assertFalse(self.args_file.exists())
        result = self.invoke('shared', '-runTests', env={'CONFIRM_FULL_TESTS': '1'}, direct=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('direct-tests.', json.loads(self.args_file.read_text())['history'])

    def test_direct_focused_history_is_unique_and_non_test_route_is_untouched(self):
        paths = []
        for _ in range(2):
            result = self.invoke('shared', '-runTests', '-testFilter', 'SomeFixture', direct=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            paths.append(json.loads(self.args_file.read_text())['history'])
        self.assertNotEqual(paths[0], paths[1])
        result = self.invoke('shared', '-batchmode', '-executeMethod', 'Some.Build', direct=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIsNone(json.loads(self.args_file.read_text())['history'])

    def test_direct_focused_wrapper_preserves_filter_and_override(self):
        history = str(self.root / 'specific history.json')
        result = self.invoke('shared', '-runTests', '-testFilter', 'SomeFixture;OtherFixture',
                             env={'STAR_TOURNAMENT_QA_LAB_HISTORY': history}, direct=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        receipt = json.loads(self.args_file.read_text())
        self.assertEqual(receipt['history'], history)
        self.assertEqual(receipt['args'][-2:], ['-testFilter', 'SomeFixture;OtherFixture'])


if __name__ == '__main__':
    unittest.main()
