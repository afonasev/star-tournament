"""Prevent implicit Player builds in local and full-test routes."""
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]

class LocalWorkflowTests(unittest.TestCase):
    def route(self, target):
        return subprocess.check_output(['make', '-n', target], cwd=ROOT, text=True)

    def test_checks_and_editor_do_not_build_player(self):
        for target in ('check-local', 'check', 'check-full', 'unity-editor'):
            with self.subTest(target=target):
                route = self.route(target)
                for token in ('tools.sh build', '--suite build', '--suite player',
                              'project_checks.py build', 'PrototypeBuilder.BuildMac'):
                    self.assertNotIn(token, route)

    def test_check_is_local_without_full_match_suites(self):
        route = self.route('check')
        self.assertEqual(route, self.route('check-local'))
        for token in ('tools.sh test-edit', 'tools.sh test-play', '--suite all',
                      'check_qa.py full', 'RunWithFixtureEquivalence'):
            self.assertNotIn(token, route)

    def test_player_route_does_not_launch_tests(self):
        route = self.route('check-player')
        self.assertIn('tools.sh build', route)
        for token in ('test-edit', 'test-play', 'check_ui.py', 'check-full'):
            self.assertNotIn(token, route)

    def test_tooling_route_never_launches_unity(self):
        route = self.route('check-tooling')
        self.assertIn('test_unity_tools.py', route)
        self.assertNotIn('./unity/tools.sh', route)
        self.assertNotIn('python3 tools/check_ui.py', route)

    def test_full_make_route_requires_confirmation_before_either_suite(self):
        import os
        environment = {k: v for k, v in os.environ.items() if k != 'CONFIRM_FULL_TESTS'}
        for target in ('test', 'test-edit', 'test-play', 'check-full'):
            result = subprocess.run(['make', target], cwd=ROOT, env=environment,
                                    text=True, capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('human confirmation', result.stderr)
            self.assertNotIn('tools.sh test-play', result.stdout if target in ('test', 'check-full') else '')

    def test_build_remains_explicit(self):
        route = self.route('build')
        self.assertTrue(any(token in route for token in ('tools.sh build', '--suite build',
                        'project_checks.py build', 'PrototypeBuilder.BuildMac')), route)

    def test_default_make_only_shows_help(self):
        route = subprocess.check_output(['make', '-n'], cwd=ROOT, text=True)
        self.assertNotIn('tools/unity.sh', route)
        self.assertNotIn('python3 tools/', route)
        self.assertNotIn('./unity/tools.sh', route)

if __name__ == '__main__':
    unittest.main()
