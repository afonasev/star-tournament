#!/usr/bin/env python3
"""UI-only gate. Scope review and native visual QA are recorded separately."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SUITES = {
    'EditMode': ('LocalSeatLayoutTests',),
    'PlayMode': ('FourPlayerMenuTests', 'IndependentPauseMenuTests'),
}

# Keep this in sync with the actual NUnit [Test]/[UnityTest] methods selected by
# SUITES. NUnit emits one test-case per method today; explicit counts make any
# future parameterized cases part of the contract rather than a threshold.
EXPECTED_CASES = {
    'LocalSeatLayoutTests': {
        'StarTournament.ProvingGround.Tests.LocalSeatLayoutTests.ApprovedViewportsCoverScreenWithoutOverlapping': 1,
        'StarTournament.ProvingGround.Tests.LocalSeatLayoutTests.RemovingKeyboardSeatReleasesMouseOwnership': 1,
        'StarTournament.ProvingGround.Tests.LocalSeatLayoutTests.ShrinkReleasesOnlyRemovedDevicesAndGrowRequiresFreshJoins': 1,
    },
    'FourPlayerMenuTests': {
        'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.FourGamepadsCanJoinStartPauseAndResumeWithoutMouse': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.FpsSettingPersistsAndKeepsSamplingWhilePaused': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.FramesStayWithAssignedSeatAndEdgesAreConsumedOnce': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.NewBotsConfirmDefaultDifficultyWithOneSubmit': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.FourPlayerMenuTests.StartLaunchesReadyRosterWithoutFocusAndDoesNotPauseWhileHeld': 1,
    },
    'IndependentPauseMenuTests': {
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.AntialiasingPropagatesAcrossOpenSeatSettingsAndAllCameras': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.BalanceIdentityFitsOneTwoAndFourViewportsAndOperatorPause': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.ColorGradingPropagatesBetweenSeatsWithoutAdvancingMatch': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.DisconnectedSeatCannotResumeThroughTheOtherMenu': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.GamepadNavigationSubmitsOnlyItsOwnViewport': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.GraphicsEffectsPropagateBetweenSeatsWithoutAdvancingMatch': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.MatchDisplayConfirmationHasOneOwnerAndRollsBackOnClose': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.PauseShowsTheFrozenSavedBalanceWithoutChangingMatch': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.PersonalSettingsAndGlobalConfirmationStayScoped': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.QualityChangesAcrossSeatsPreserveIndependentPreferencesAndFrozenBalance': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.SectionPreviewChangesOnlyItsOwnViewportWithoutSubmit': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.SettingsHaveIdenticalCapabilitiesAndHelpInEveryViewport': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.SplitScreenDrawsOneGlobalFpsCounter': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.TwoSeatMenusKeepOneFrozenMatchUntilTheLastCloses': 1,
        'StarTournament.ProvingGround.Tests.PlayMode.IndependentPauseMenuTests.VSyncPropagatesBetweenSeatsWithoutAdvancingMatch': 1,
    },
}


def _validate_cases(cases, expected):
    actual = Counter(case.get('fullname', '') for case in cases)
    if actual != Counter(expected):
        missing = Counter(expected) - actual
        extra = actual - Counter(expected)
        details = []
        if missing:
            details.append('missing: ' + ', '.join(f'{name} x{count}' for name, count in sorted(missing.items())))
        if extra:
            details.append('unexpected or duplicate: ' + ', '.join(f'{name or "<empty fullname>"} x{count}' for name, count in sorted(extra.items())))
        raise ValueError('UI test case set mismatch (' + '; '.join(details) + ')')
    return sum(actual.values())


def validate_results(path, fixtures):
    root = ET.parse(path).getroot()
    cases = root.findall('.//test-case')
    if not cases or root.get('result') != 'Passed':
        raise ValueError('UI gate requires a nonempty Passed test run')
    for case in cases:
        if case.get('result') != 'Passed':
            raise ValueError('Every selected UI test must pass: ' + case.get('fullname', '?'))
    expected = Counter()
    for fixture in fixtures:
        try:
            expected.update(EXPECTED_CASES[fixture])
        except KeyError as error:
            raise ValueError('Unknown UI fixture: ' + str(error.args[0])) from error
    _validate_cases(cases, expected)
    return len(cases)


def run():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true', help='Print selection without running Unity')
    args = parser.parse_args()
    plan = {'scope': 'ui-only', 'natural_bot_matches': False, 'suites': SUITES,
            'policy': '.agents/references/qa-scope.md',
            'additional_gates': 'affected-screen tests and Editor Play Mode; Player gates deferred unless explicitly requested or necessary'}
    if args.plan:
        print(json.dumps(plan, indent=2))
        return 0
    destination = ROOT / '.local/unity-evidence'
    destination.mkdir(parents=True, exist_ok=True)
    evidence = Path(tempfile.mkdtemp(prefix='ui-gate.', dir=destination))
    plan['revision'] = subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD'], text=True).strip()
    plan['dirty'] = subprocess.check_output(['git', '-C', str(ROOT), 'status', '--short'], text=True)
    plan['result'] = 'Failed'
    plan['checks'] = []
    try:
        for platform, fixtures in SUITES.items():
            stem = 'editmode' if platform == 'EditMode' else 'playmode'
            command = [str(ROOT / 'unity/tools.sh'), 'test-' + ('edit' if platform == 'EditMode' else 'play') + '-filter', ';'.join(fixtures)]
            result = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            print(result.stdout, end='', flush=True)
            (evidence / (stem + '-runner.log')).write_text(result.stdout)
            if result.returncode:
                raise ValueError(f'{platform} Unity exit {result.returncode}')
            artifact_dirs = [line.removeprefix('Evidence: ') for line in result.stdout.splitlines() if line.startswith('Evidence: ')]
            if len(artifact_dirs) != 1:
                raise ValueError('Missing unique Unity evidence path')
            xml = Path(artifact_dirs[0]) / (stem + '.xml')
            count = validate_results(xml, fixtures)
            plan['checks'].append({'platform': platform, 'xml': str(xml), 'passed': count})
        plan['result'] = 'Passed'
    except (ValueError, OSError, ET.ParseError) as error:
        plan['error'] = str(error)
        print('UI gate FAILED: ' + str(error), flush=True)
    finally:
        (evidence / 'result.json').write_text(json.dumps(plan, indent=2) + '\n')
        print('UI gate evidence: ' + str(evidence), flush=True)
    return 0 if plan['result'] == 'Passed' else 1


if __name__ == '__main__':
    raise SystemExit(run())
