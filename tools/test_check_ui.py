"""Guard the scope boundary and prevent false greens from empty/partial XML."""
from pathlib import Path
import tempfile
import unittest
from check_ui import SUITES, validate_results


class UiGateTests(unittest.TestCase):
    def validate(self, text, fixtures=('FourPlayerMenuTests',)):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'tests.xml'
            path.write_text(text)
            return validate_results(path, fixtures)

    def test_scope_excludes_combat_and_bot_fixtures(self):
        self.assertEqual(SUITES, {'EditMode': ('LocalSeatLayoutTests',),
                                 'PlayMode': ('FourPlayerMenuTests', 'IndependentPauseMenuTests')})

    def test_passed_fixture_is_required(self):
        case = '<test-case fullname="Tests.FourPlayerMenuTests.Start" result="Passed"/>'
        self.assertEqual(self.validate('<test-run result="Passed">' + case * 3 + '</test-run>'), 3)
        with self.assertRaises(ValueError):
            self.validate('<test-run result="Passed">' + case + '</test-run>', ('IndependentPauseMenuTests',))

        with self.assertRaises(ValueError):
            self.validate('<test-run result="Passed">' + case + '</test-run>')

    def test_empty_failed_skipped_and_malformed_runs_fail(self):
        for xml in ['<test-run result="Passed"/>',
                    '<test-run result="Failed"><test-case result="Passed"/></test-run>',
                    '<test-run result="Passed"><test-case result="Skipped"/></test-run>']:
            with self.assertRaises(ValueError):
                self.validate(xml)
        from xml.etree.ElementTree import ParseError
        with self.assertRaises(ParseError):
            self.validate('invalid xml')


if __name__ == '__main__':
    unittest.main()
