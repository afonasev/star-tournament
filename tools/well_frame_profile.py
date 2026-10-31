#!/usr/bin/env python3
"""Focused Well CPU diagnosis or balanced unprofiled qualification.

Requires a fresh human focus deadline. Profiling is diagnosis, not a new FPS
qualification: capture overhead is present in both variants.
"""
import argparse
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import plistlib
import shutil
import signal
import subprocess
import sys
import time

REAL = Path('/Users/eaafonasev/.local/bin/unity-run')
GIB = 1024**3
PER_CASE = 150
ORDER = ('control', 'optimized')
MAX_PROFILE = 2*GIB

def permitted(now, deadline, remaining, free, per_case=PER_CASE, reserve_per_case=2*GIB):
    return deadline-now >= remaining*per_case and free >= 5*GIB+remaining*reserve_per_case

def require_guardian(parent, session_leader):
    if str(REAL) not in parent or '--exclusive' not in parent or not session_leader:
        raise RuntimeError('NotRun: real exclusive guardian required')

def reference_identity(reference, expected_source):
    assert reference['source'] == expected_source and reference['map'] == 'well-laboratory-v1'
    assert reference['status'] == 'COMPLETE_DIAGNOSTIC_NOT_ACCEPTANCE'

def case_order(manifest):
    if manifest.get('mode') in ('unprofiled-abba', 'unprofiled-abccba'):
        order = tuple(manifest.get('cases', ()))
        expected = ('A1', 'B1', 'C1', 'C2', 'B2', 'A2') if manifest['mode'] == 'unprofiled-abccba' else ('A1', 'B1', 'B2', 'A2')
        if order != expected:
            raise ValueError('Unprofiled qualification requires the exact complete balanced order')
        return order
    if manifest.get('mode', 'cpu') != 'cpu':
        raise ValueError('Unknown measurement mode')
    order = tuple(manifest.get('cases', ORDER))
    if order not in (ORDER, ('optimized',)):
        raise ValueError('Only the complete pair or its unexecuted optimized case is permitted')
    return order

def case_variant(label):
    if label in ORDER:
        return label
    return {'A1': 'control', 'B1': 'optimized', 'C1': 'corrected', 'C2': 'corrected', 'B2': 'optimized', 'A2': 'control'}[label]

def case_budget(manifest):
    return 120 if manifest.get('mode') in ('unprofiled-abba', 'unprofiled-abccba') else PER_CASE

def case_storage(manifest):
    # Unprofiled output has no large .raw file: reserve 512MiB per case plus
    # the unchanged 5GiB live floor. CPU capture still reserves full 2GiB each.
    return GIB//2 if manifest.get('mode') in ('unprofiled-abba', 'unprofiled-abccba') else 2*GIB

def command(app, output, reference, cpu_profile=True):
    with (app/'Contents/Info.plist').open('rb') as stream:
        executable = plistlib.load(stream)['CFBundleExecutable']
    cmd = [str(app/'Contents/MacOS'/executable), '-noaudio', '-muted',
            '-renderingBaseline', '-baselineMap', 'well-laboratory-v1',
            '-baselineEvidence', str(output), '-baselineReference', str(reference),
            '-screen-width', '1920', '-screen-height', '1080',
            '-logFile', str(output/'player.log')]
    if cpu_profile:
        cmd += ['-baselineCpuProfile', str(output)]
    return cmd

def inputs(manifest):
    m = json.loads(manifest.read_text())
    repo = Path(m['worktree'])
    assert Path.cwd() == repo
    assert subprocess.check_output(['git', '-C', str(repo), 'rev-parse', 'HEAD'], text=True).strip() == m['runnerSource']
    assert not subprocess.check_output(['git', '-C', str(repo), 'status', '--porcelain'], text=True).strip()
    for path, digest in m['hashes'].items():
        assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == digest, path
    spec = importlib.util.spec_from_file_location('strict_p6', repo/'tools/p6_player_compare.py')
    validator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validator)
    variants = (*ORDER, 'corrected') if m.get('mode') == 'unprofiled-abccba' else ORDER
    for kind in variants:
        assert validator.inventory(Path(m[kind]['app']))['tree_sha256'] == m[kind]['tree_sha256']
    reference = json.loads(Path(m['reference']).read_text())
    reference_identity(reference, m['referenceSource'])
    if case_order(m) == ('optimized',) or m.get('mode') in ('unprofiled-abba', 'unprofiled-abccba'):
        baseline = validator.validate(Path(m['qualifiedControl']), m['control']['source'], 'well-laboratory-v1')
        assert baseline == reference, 'Current comparison reference must be the unchanged completed control report'
        original = json.loads(Path(m['originalReference']).read_text())
        for actual, previous in zip(baseline['samples'], original['samples']):
            if actual['name'].startswith('frozen'):
                for field in ('stateStart', 'stateEnd', 'camerasStart', 'camerasEnd'):
                    assert actual[field] == previous[field], 'Original frozen contract changed: '+field
    return m, validator

def host(pid):
    started = time.time()
    def read(args):
        result = subprocess.run(args, capture_output=True, text=True, timeout=5)
        return {'exit': result.returncode, 'output': result.stdout}
    processes = read(['/bin/ps', '-A', '-o', 'pcpu=,comm='])
    top = sorted((line for line in processes['output'].splitlines() if line.strip()),
                 key=lambda line: float(line.split(maxsplit=1)[0]), reverse=True)[:8]
    result = {'unix': started, 'player': read(['/bin/ps', '-p', str(pid), '-o', 'pid=,pcpu=,rss=,etime=']),
              'vm': read(['/usr/bin/vm_stat']), 'swap': read(['/usr/sbin/sysctl', 'vm.swapusage', 'vm.loadavg']),
              'topProcessCPU': top}
    result['telemetryCostSeconds'] = time.time()-started
    return result

def stop_owned(proc):
    if proc.poll() is None:
        os.killpg(proc.pid, signal.SIGTERM)
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            os.killpg(proc.pid, signal.SIGKILL)
            proc.wait()

def monitor(proc, output, deadline):
    with (output/'host.jsonl').open('w') as log:
        while proc.poll() is None:
            profile = output/'cpu.raw'
            reason = ('human deadline' if time.time() >= deadline else
                      '5GiB storage reserve' if shutil.disk_usage('/System/Volumes/Data').free < 5*GIB else
                      '2GiB profile limit' if profile.exists() and profile.stat().st_size > MAX_PROFILE else None)
            if reason:
                raise RuntimeError('FAIL/NotRun stopped owned Player: '+reason)
            log.write(json.dumps(host(proc.pid))+'\n')
            log.flush()
            time.sleep(1)  # Deliberate 1Hz diagnostic host telemetry, not status polling.
    return proc.returncode

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['verify', 'run'])
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--deadline')
    parser.add_argument('--human-source')
    parser.add_argument('--admitted', action='store_true', help=argparse.SUPPRESS)
    args = parser.parse_args()
    m, validator = inputs(args.manifest)
    order = case_order(m)
    cpu_profile = m.get('mode', 'cpu') == 'cpu'
    budget = case_budget(m)
    reserve = case_storage(m)
    if args.operation == 'verify':
        assert not args.admitted
        print(json.dumps({'PlayerLaunched': False, 'retainedArtifactsVerified': True,
                          'cases': order, 'requestedWindowSeconds': m.get('requestedWindowSeconds', 360),
                          'freeBytes': shutil.disk_usage('/System/Volumes/Data').free}))
        return 0
    assert args.output and args.output.is_absolute() and not args.output.exists()
    assert args.deadline and args.human_source
    date = datetime.datetime.fromisoformat(args.deadline.replace('Z', '+00:00'))
    assert date.tzinfo is not None, 'Timezone-aware deadline required'
    deadline = date.timestamp()
    if not permitted(time.time(), deadline, len(order), shutil.disk_usage('/System/Volumes/Data').free, budget, reserve):
        print('NotRun: complete scenario budget or storage headroom')
        return 75
    if not args.admitted:
        wait = deadline-time.time()-len(order)*budget
        cmd = [str(REAL), '--exclusive', '--timeout', str(wait), '--', sys.executable, '-B',
               str(Path(__file__).resolve()), 'run', '--admitted', '--manifest', str(args.manifest),
               '--output', str(args.output), '--deadline', args.deadline, '--human-source', args.human_source]
        return subprocess.run(cmd).returncode
    parent = subprocess.check_output(['ps', '-p', str(os.getppid()), '-o', 'command='], text=True)
    require_guardian(parent, os.getsid(0) == os.getpid())
    args.output.mkdir(parents=True)
    journal = {'authority': args.human_source, 'deadline': args.deadline, 'status': 'RUNNING',
               'runnerSource': m['runnerSource'], 'manifestSHA256': hashlib.sha256(args.manifest.read_bytes()).hexdigest(),
               'measurementMode': m.get('mode', 'cpu'),
               'purpose': ('CPU cause diagnosis with profiling overhead' if cpu_profile else 'Matched unprofiled balanced variants, descriptive scenario timing only'), 'cases': []}
    def save():
        (args.output/'scenario.json').write_text(json.dumps(journal, indent=2))
    save()
    for number, label in enumerate(order):
        kind = case_variant(label)
        if not permitted(time.time(), deadline, len(order)-number, shutil.disk_usage('/System/Volumes/Data').free, budget, reserve):
            journal['status'] = 'NotRun_REMAINING_BUDGET_OR_STORAGE'; save(); return 75
        output = args.output/label
        output.mkdir()
        reference = Path(m['reference']) if cpu_profile or number == 0 else args.output/'A1/report.json'
        cmd = command(Path(m[kind]['app']), output, reference, cpu_profile)
        record = {'label': label, 'kind': kind, 'command': cmd, 'source': m[kind]['source'], 'startedUnix': time.time()}
        journal['cases'].append(record); save()
        env = os.environ.copy()
        env.update(STAR_TOURNAMENT_QA_LAB_HISTORY=str(output/'lab-history.json'), STAR_TOURNAMENT_BASELINE_SOURCE=m[kind]['source'])
        proc = None
        try:
            with (output/'stdout.log').open('w') as log:
                proc = subprocess.Popen(cmd, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
                record['pid'] = proc.pid; save()
                record['exit'] = monitor(proc, output, deadline)
            assert record['exit'] == 0, 'Player exit '+str(record['exit'])
            assert time.time() <= deadline, 'Human window exceeded'
            validator.validate(output, m[kind]['source'], 'well-laboratory-v1', reference)
            report = json.loads((output/'report.json').read_text())
            expected = [None, None, str(output/'cpu.raw')] if cpu_profile else [None, None, None]
            assert [s.get('cpuProfile') or None for s in report['samples']] == expected, 'Unexpected profiler sample scope'
            if cpu_profile:
                assert 0 < (output/'cpu.raw').stat().st_size <= MAX_PROFILE, 'Profiler output missing/empty/over cap'
                assert 'Skipping profile frame' not in (output/'player.log').read_text(errors='replace'), 'Profiler dropped frames'
            else:
                assert not (output/'cpu.raw').exists(), 'Unprofiled case unexpectedly recorded a CPU profile'
            record['status'] = 'STRICT_PROTOCOL_PASS_PROFILE_READBACK_PENDING' if cpu_profile else 'STRICT_PROTOCOL_PASS_UNPROFILED'
            record['finishedUnix'] = time.time(); save()
            print(label, record['status'], flush=True)
        except Exception as error:
            journal['status'] = 'FAIL_STOP'; record['error'] = str(error); save()
            print(str(error), flush=True); return 1
        finally:
            if proc is not None:
                stop_owned(proc)
    journal['status'] = ('COMPLETE_DIAGNOSIS_CAPTURE_NOT_PERFORMANCE_ACCEPTANCE' if cpu_profile else
                         'COMPLETE_ABCCBA_UNPROFILED_DESCRIPTIVE_NOT_ACCEPTANCE' if m['mode'] == 'unprofiled-abccba' else
                         'COMPLETE_ABBA_UNPROFILED_DESCRIPTIVE_NOT_ACCEPTANCE'); save()
    return 0

if __name__ == '__main__':
    sys.exit(main())
