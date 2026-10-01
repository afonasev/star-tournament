#!/usr/bin/env python3
"""UI-only gate. Scope review and native visual QA are recorded separately."""
import argparse
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

MIN_CASES = {'LocalSeatLayoutTests': 3, 'FourPlayerMenuTests': 3, 'IndependentPauseMenuTests': 4}

def validate_results(path, fixtures):
    root = ET.parse(path).getroot()
    cases = root.findall('.//test-case')
    if not cases or root.get('result') != 'Passed':
        raise ValueError('UI gate requires a nonempty Passed test run')
    for case in cases:
        if case.get('result') != 'Passed':
            raise ValueError('Every selected UI test must pass: ' + case.get('fullname', '?'))
    for fixture in fixtures:
        if sum('.' + fixture + '.' in case.get('fullname', '') for case in cases) < MIN_CASES[fixture]:
            raise ValueError('Missing or incomplete UI fixture: ' + fixture)
    return len(cases)


def run():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true', help='Print selection without running Unity')
    args = parser.parse_args()
    plan = {'scope': 'ui-only', 'natural_bot_matches': False, 'suites': SUITES,
            'policy': '.agents/references/qa-scope.md',
            'additional_gates': 'affected-screen tests; native build and visual smoke for visible changes'}
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
