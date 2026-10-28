"""Guard the scope boundary and exact NUnit case contract of the UI gate."""
from collections import Counter
from pathlib import Path
import re
import tempfile
import unittest
import xml.etree.ElementTree as ET

from check_ui import EXPECTED_CASES, SUITES, _validate_cases, validate_results


ROOT = Path(__file__).resolve().parents[1]


def case_xml(fullname, result='Passed'):
    return f'<test-case fullname="{fullname}" result="{result}"/>'


def complete_xml(fixtures=('FourPlayerMenuTests',)):
    cases = [case_xml(name) for fixture in fixtures for name, count in EXPECTED_CASES[fixture].items() for _ in range(count)]
    return '<test-run result="Passed">' + ''.join(cases) + '</test-run>'


class UiGateTests(unittest.TestCase):
    def validate(self, text, fixtures=('FourPlayerMenuTests',)):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'tests.xml'
            path.write_text(text)
            return validate_results(path, fixtures)

    def test_scope_excludes_combat_and_bot_fixtures(self):
        self.assertEqual(SUITES, {'EditMode': ('LocalSeatLayoutTests',),
                                 'PlayMode': ('FourPlayerMenuTests', 'IndependentPauseMenuTests')})

    def test_exact_expected_case_set_passes(self):
        self.assertEqual(self.validate(complete_xml()), len(EXPECTED_CASES['FourPlayerMenuTests']))
        fixtures = ('FourPlayerMenuTests', 'IndependentPauseMenuTests')
        self.assertEqual(self.validate(complete_xml(fixtures), fixtures), 21)

    def test_expected_nunit_names_match_selected_test_methods(self):
        sources = {
            'LocalSeatLayoutTests': ROOT / 'unity/Assets/StarTournament/Tests/EditMode/LocalSeatLayoutTests.cs',
            'FourPlayerMenuTests': ROOT / 'unity/Assets/StarTournament/Tests/PlayMode/FourPlayerMenuTests.cs',
            'IndependentPauseMenuTests': ROOT / 'unity/Assets/StarTournament/Tests/PlayMode/IndependentPauseMenuTests.cs',
        }
        for fixture, source in sources.items():
            declared = set(re.findall(r'\[(?:Test|UnityTest)\]\s*(?:public\s+)?(?:IEnumerator|void)\s+(\w+)\s*\(',
                                      source.read_text()))
            configured = {fullname.rsplit('.', 1)[1]
                          for fullname in EXPECTED_CASES[fixture]}
            self.assertEqual(configured, declared, fixture)

    def test_duplicate_case_cannot_replace_missing_case(self):
        names = list(EXPECTED_CASES['FourPlayerMenuTests'])
        cases = [case_xml(name) for name in names[:-1]] + [case_xml(names[0]), case_xml(names[0])]
        with self.assertRaisesRegex(ValueError, 'missing:'):
            self.validate('<test-run result="Passed">' + ''.join(cases) + '</test-run>')

    def test_missing_duplicate_and_unexpected_cases_fail(self):
        names = list(EXPECTED_CASES['FourPlayerMenuTests'])
        invalid = [
            ''.join(case_xml(name) for name in names[:-1]),
            ''.join(case_xml(name) for name in names) + case_xml(names[0]),
            ''.join(case_xml(name) for name in names) + case_xml('Other.Namespace.ForeignFixture.Case'),
        ]
        for cases in invalid:
            with self.subTest(cases=cases[-100:]), self.assertRaises(ValueError):
                self.validate('<test-run result="Passed">' + cases + '</test-run>')

    def test_skipped_and_foreign_result_cases_fail(self):
        names = list(EXPECTED_CASES['FourPlayerMenuTests'])
        for status in ('Skipped', 'Failed'):
            cases = ''.join(case_xml(name, status if index == 0 else 'Passed') for index, name in enumerate(names))
            with self.subTest(status=status), self.assertRaises(ValueError):
                self.validate('<test-run result="Passed">' + cases + '</test-run>')

    def test_incomplete_failed_empty_and_malformed_runs_fail(self):
        for xml in ['<test-run result="Passed"/>',
                    '<test-run result="Failed"><test-case result="Passed"/></test-run>',
                    '<test-run result="Passed"><test-case result="Skipped"/></test-run>']:
            with self.assertRaises(ValueError):
                self.validate(xml)
        with self.assertRaises(ET.ParseError):
            self.validate('invalid xml')

    def test_saved_successful_editmode_xml_has_exact_saved_case_names(self):
        path = ROOT / 'docs/evidence/add-industrial-tunnels-map/full-check/editmode.xml'
        root = ET.parse(path).getroot()
        self.assertEqual(root.get('result'), 'Passed')
        cases = root.findall('.//test-case')
        selected = [case for case in cases if '.LocalSeatLayoutTests.' in case.get('fullname', '')]
        self.assertTrue(all(case.get('result') == 'Passed' for case in selected))
        expected = {
            'StarTournament.ProvingGround.Tests.LocalSeatLayoutTests.ApprovedViewportsCoverScreenWithoutOverlapping': 1,
            'StarTournament.ProvingGround.Tests.LocalSeatLayoutTests.RemovingKeyboardSeatReleasesMouseOwnership': 1,
            'StarTournament.ProvingGround.Tests.LocalSeatLayoutTests.ShrinkReleasesOnlyRemovedDevicesAndGrowRequiresFreshJoins': 1,
        }
        self.assertEqual(_validate_cases(selected, expected), 3)

    def test_saved_successful_playmode_xml_has_exact_saved_case_names(self):
        path = ROOT / 'docs/evidence/add-industrial-tunnels-map/full-check/playmode.xml'
        root = ET.parse(path).getroot()
        self.assertEqual(root.get('result'), 'Passed')
        cases = root.findall('.//test-case')
        selected = [case for case in cases if any('.' + fixture + '.' in case.get('fullname', '')
                                                  for fixture in SUITES['PlayMode'])]
        self.assertTrue(all(case.get('result') == 'Passed' for case in selected))
        saved_names = {
            'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.FourGamepadsCanJoinStartPauseAndResumeWithoutMouse': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.FpsSettingPersistsAndKeepsSamplingWhilePaused': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.FramesStayWithAssignedSeatAndEdgesAreConsumedOnce': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.NewBotsConfirmDefaultDifficultyWithOneSubmit': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.DisconnectedSeatCannotResumeThroughTheOtherMenu': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.GamepadNavigationSubmitsOnlyItsOwnViewport': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.MatchDisplayConfirmationHasOneOwnerAndRollsBackOnClose': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.PersonalSettingsAndGlobalConfirmationStayScoped': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.SectionPreviewChangesOnlyItsOwnViewportWithoutSubmit': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.SettingsHaveIdenticalCapabilitiesAndHelpInEveryViewport': 1,
            'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.TwoSeatMenusKeepOneFrozenMatchUntilTheLastCloses': 1,
        }
        self.assertEqual(_validate_cases(selected, Counter(saved_names)), 11)


if __name__ == '__main__':
    unittest.main()
