#!/usr/bin/env python3
"""Resume only unexecuted P6 cases under one real unity-run exclusive admission.

The compiled QA Players and strict validator remain immutable. No Player is
launched by verify. Run requires a NEW human deadline and authority.
"""
import argparse, datetime, hashlib, importlib.util, json, os, plistlib
import shutil, subprocess, sys, time
from pathlib import Path

WARM = Path('/Users/eaafonasev/.codex/worktrees/rendering-assets-followup/star-tournament')
CANON = Path('/Users/eaafonasev/Projects/star-tournament/planning/docs/evidence/verify-final-player-rendering')
REAL = Path('/Users/eaafonasev/.local/bin/unity-run')
PER_CASE = 120
GIB = 1024**3
ORDER = [('lunar-laboratory-v1', x) for x in ['B1', 'B2', 'A2']] + [('well-laboratory-v1', x) for x in ['A1', 'B1', 'B2', 'A2']]

def variant(label):
    return 'control' if label.startswith('A') else 'optimized'

def permitted(now, deadline, remaining_cases, free, initial=False):
    return deadline-now >= remaining_cases*PER_CASE and free >= (5 if initial else 3)*GIB

def outer_command(script, manifest, output, deadline, authority, now):
    wait = deadline-now-len(ORDER)*PER_CASE
    if wait <= 0:
        raise ValueError('NotRun: insufficient complete-scenario budget')
    return [str(REAL), '--exclusive', '--timeout', str(wait), '--', sys.executable, '-B', str(script), 'run', '--admitted', '--manifest', str(manifest), '--output', str(output), '--deadline', datetime.datetime.fromtimestamp(deadline, datetime.timezone.utc).isoformat(), '--human-source', authority]

def require_outer_admission(parent_command, own_session):
    if str(REAL) not in parent_command or '--exclusive' not in parent_command or not own_session:
        raise RuntimeError('Refuse direct --admitted: real exclusive guardian required')

def player_command(app, map_id, output, reference):
    with (app/'Contents/Info.plist').open('rb') as stream:
        executable = plistlib.load(stream)['CFBundleExecutable']
    cmd = [str(app/'Contents/MacOS'/executable), '-noaudio', '-muted', '-renderingBaseline', '-baselineMap', map_id, '-baselineEvidence', str(output), '-screen-width', '1920', '-screen-height', '1080', '-logFile', str(output/'player.log')]
    if reference:
        cmd += ['-baselineReference', str(reference)]
    return cmd

def load_inputs(path):
    prep = json.loads(path.read_text())
    assert subprocess.check_output(['git', '-C', str(WARM), 'rev-parse', 'HEAD'], text=True).strip() == prep['runnerSource']
    assert subprocess.check_output(['git', '-C', str(WARM), 'branch', '--show-current'], text=True).strip() == 'codex/verify-final-player-rendering'
    assert not subprocess.check_output(['git', '-C', str(WARM), 'status', '--porcelain'], text=True).strip()
    for name, digest in prep['scriptHashes'].items():
        assert hashlib.sha256(Path(name).read_bytes()).hexdigest() == digest, name
    build = json.loads((CANON/'comparison-manifest.json').read_text())
    spec = importlib.util.spec_from_file_location('p6_strict_unchanged', WARM/'tools/p6_player_compare.py')
    validator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validator)
    for key in ['control', 'optimized']:
        assert validator.inventory(Path(build[key]['app']))['tree_sha256'] == build[key]['tree_sha256']
    index = prep['existingCases'].copy()
    for map_id, cases in index.items():
        for label, output in cases.items():
            validator.validate(Path(output), build[variant(label)]['source'], map_id, None if label == 'A1' else Path(cases['A1'])/'report.json')
    assert sum(map(len, index.values())) == 9
    return prep, build, validator, index

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['verify', 'run'])
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--deadline')
    parser.add_argument('--human-source')
    parser.add_argument('--admitted', action='store_true', help=argparse.SUPPRESS)
    args = parser.parse_args()
    assert Path.cwd() == WARM and args.manifest.is_absolute()
    prep, build, validator, index = load_inputs(args.manifest)
    free = shutil.disk_usage('/System/Volumes/Data').free
    if args.operation == 'verify':
        assert not args.admitted
        print(json.dumps({'existingStrictPasses': 9, 'remainingCases': ORDER, 'minimumScenarioSeconds': len(ORDER)*PER_CASE, 'requestedWindowSeconds': 960, 'freeBytes': free, 'capacityReady': free >= 5*GIB, 'PlayerLaunched': False}))
        return 0
    assert args.output and args.output.is_absolute() and args.deadline and args.human_source
    deadline = datetime.datetime.fromisoformat(args.deadline.replace('Z', '+00:00')).timestamp()
    if not args.admitted:
        assert not args.output.exists()
        if not permitted(time.time(), deadline, len(ORDER), free, initial=True):
            print('NotRun_BEFORE_QUEUE: scenario budget or 5GiB headroom')
            return 75
        command = outer_command(Path(__file__).resolve(), args.manifest, args.output, deadline, args.human_source, time.time())
        return subprocess.run(command, cwd=WARM).returncode
    parent = subprocess.check_output(['ps', '-p', str(os.getppid()), '-o', 'command='], text=True)
    require_outer_admission(parent, os.getsid(0) == os.getpid())
    assert not args.output.exists()
    args.output.mkdir(parents=True)
    journal = {'humanSource': args.human_source, 'deadline': args.deadline, 'runnerSource': prep['runnerSource'], 'compiledSources': {k: build[k]['source'] for k in ['control', 'optimized']}, 'admittedUnix': time.time(), 'oneExclusiveScenario': True, 'status': 'RUNNING', 'events': []}
    def event(kind, **data):
        journal['events'].append({'kind': kind, 'atUnix': time.time(), **data})
        (args.output/'scenario.json').write_text(json.dumps(journal, indent=2))
        print(kind, data, flush=True)
    if not permitted(time.time(), deadline, len(ORDER), shutil.disk_usage('/System/Volumes/Data').free, initial=True):
        journal['status'] = 'NotRun_POST_ADMISSION'; event('STOP_BEFORE_PLAYER'); return 75
    for number, (map_id, label) in enumerate(ORDER):
        free = shutil.disk_usage('/System/Volumes/Data').free
        if not permitted(time.time(), deadline, len(ORDER)-number, free):
            journal['status'] = 'NotRun_REMAINING_BUDGET_OR_CAPACITY'; event('STOP_BEFORE_PLAYER', map=map_id, label=label, freeBytes=free); return 75
        output = args.output/map_id/label
        output.mkdir(parents=True, exist_ok=False)
        reference = None if label == 'A1' else Path(index[map_id]['A1'])/'report.json'
        item = build[variant(label)]
        cmd = player_command(Path(item['app']), map_id, output, reference)
        env = os.environ.copy()
        env.update(STAR_TOURNAMENT_QA_LAB_HISTORY=str(output/'lab-history.json'), STAR_TOURNAMENT_BASELINE_SOURCE=item['source'])
        record = {'command': cmd, 'source': item['source'], 'artifactTree': item['tree_sha256'], 'freeBytes': free, 'startedUnix': time.time(), 'humanSource': args.human_source, 'deadline': args.deadline, 'route': 'descendant of one real unity-run exclusive guardian'}
        (output/'command.json').write_text(json.dumps(record, indent=2)); event('DISPATCH', map=map_id, label=label)
        with (output/'player-stdout.log').open('w') as log:
            result = subprocess.run(cmd, cwd=WARM, env=env, stdout=log, stderr=subprocess.STDOUT)
        record.update(exitCode=result.returncode, finishedUnix=time.time()); (output/'command.json').write_text(json.dumps(record, indent=2))
        try:
            assert result.returncode == 0, 'Player exit '+str(result.returncode)
            validator.validate(output, item['source'], map_id, reference)
            assert time.time() <= deadline, 'Human window exceeded; stop remaining dispatch'
        except Exception as error:
            journal['status'] = 'FAIL_STOP'; event('STOP', map=map_id, label=label, error=str(error)); return 1
        index.setdefault(map_id, {})[label] = str(output)
        (output/'acceptance.json').write_text(json.dumps({'status': 'PASS_FOCUSED_DIAGNOSTIC_NOT_HUMAN_ACCEPTANCE'}))
        event('PASS', map=map_id, label=label)
        (args.output/'combined-case-index.json').write_text(json.dumps({'cases': index, 'crossWindowMaps': ['lunar-laboratory-v1'], 'limitations': 'Lunar A1 from first window; later controls/optimized runs retain strict inputs but time gap is additional variance, no significance claim'}, indent=2))
    journal['status'] = 'COMPLETE_REMAINING_CASES_NOT_HUMAN_ACCEPTANCE'; event('COMPLETE'); return 0

if __name__ == '__main__':
    sys.exit(main())
