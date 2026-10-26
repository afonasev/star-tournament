#!/usr/bin/env python3
"""Profile the two immutable P6 Well Players; verify never launches Unity.

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

def permitted(now, deadline, remaining, free):
    return deadline-now >= remaining*PER_CASE and free >= (2*remaining+5)*GIB

def require_guardian(parent, session_leader):
    if str(REAL) not in parent or '--exclusive' not in parent or not session_leader:
        raise RuntimeError('NotRun: real exclusive guardian required')

def command(app, output, reference):
    with (app/'Contents/Info.plist').open('rb') as stream:
        executable = plistlib.load(stream)['CFBundleExecutable']
    return [str(app/'Contents/MacOS'/executable), '-noaudio', '-muted',
            '-renderingBaseline', '-baselineMap', 'well-laboratory-v1',
            '-baselineEvidence', str(output), '-baselineReference', str(reference),
            '-screen-width', '1920', '-screen-height', '1080',
            '-logFile', str(output/'player.log'), '-profiler-enable',
            '-profiler-log-file', str(output/'cpu.raw'),
            '-profiler-maxusedmemory', str(256*1024**2)]

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
    for kind in ORDER:
        assert validator.inventory(Path(m[kind]['app']))['tree_sha256'] == m[kind]['tree_sha256']
    reference = json.loads(Path(m['reference']).read_text())
    assert reference['source'] == m['control']['source'] and reference['map'] == 'well-laboratory-v1'
    assert reference['status'] == 'COMPLETE_DIAGNOSTIC_NOT_ACCEPTANCE'
    return m, validator

def host(pid):
    def read(args):
        result = subprocess.run(args, capture_output=True, text=True, timeout=5)
        return {'exit': result.returncode, 'output': result.stdout}
    return {'unix': time.time(), 'player': read(['/bin/ps', '-p', str(pid), '-o', 'pid=,pcpu=,rss=,etime=']),
            'vm': read(['/usr/bin/vm_stat']), 'swap': read(['/usr/sbin/sysctl', 'vm.swapusage'])}

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
    if args.operation == 'verify':
        assert not args.admitted
        print(json.dumps({'PlayerLaunched': False, 'retainedArtifactsVerified': True,
                          'cases': ORDER, 'requestedWindowSeconds': 360,
                          'freeBytes': shutil.disk_usage('/System/Volumes/Data').free}))
        return 0
    assert args.output and args.output.is_absolute() and not args.output.exists()
    assert args.deadline and args.human_source
    date = datetime.datetime.fromisoformat(args.deadline.replace('Z', '+00:00'))
    assert date.tzinfo is not None, 'Timezone-aware deadline required'
    deadline = date.timestamp()
    if not permitted(time.time(), deadline, len(ORDER), shutil.disk_usage('/System/Volumes/Data').free):
        print('NotRun: complete scenario budget or storage headroom')
        return 75
    if not args.admitted:
        wait = deadline-time.time()-len(ORDER)*PER_CASE
        cmd = [str(REAL), '--exclusive', '--timeout', str(wait), '--', sys.executable, '-B',
               str(Path(__file__).resolve()), 'run', '--admitted', '--manifest', str(args.manifest),
               '--output', str(args.output), '--deadline', args.deadline, '--human-source', args.human_source]
        return subprocess.run(cmd).returncode
    parent = subprocess.check_output(['ps', '-p', str(os.getppid()), '-o', 'command='], text=True)
    require_guardian(parent, os.getsid(0) == os.getpid())
    args.output.mkdir(parents=True)
    journal = {'authority': args.human_source, 'deadline': args.deadline, 'status': 'RUNNING',
               'purpose': 'CPU/host cause diagnosis only, profiling overhead not FPS qualification', 'cases': []}
    def save():
        (args.output/'scenario.json').write_text(json.dumps(journal, indent=2))
    save()
    for number, kind in enumerate(ORDER):
        if not permitted(time.time(), deadline, len(ORDER)-number, shutil.disk_usage('/System/Volumes/Data').free):
            journal['status'] = 'NotRun_REMAINING_BUDGET_OR_STORAGE'; save(); return 75
        output = args.output/kind
        output.mkdir()
        cmd = command(Path(m[kind]['app']), output, Path(m['reference']))
        record = {'kind': kind, 'command': cmd, 'source': m[kind]['source'], 'startedUnix': time.time()}
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
            validator.validate(output, m[kind]['source'], 'well-laboratory-v1', Path(m['reference']))
            assert (output/'cpu.raw').stat().st_size > 0, 'Profiler output missing/empty'
            record['status'] = 'STRICT_PROTOCOL_PASS_PROFILE_READBACK_PENDING'
            record['finishedUnix'] = time.time(); save()
            print(kind, record['status'], flush=True)
        except Exception as error:
            journal['status'] = 'FAIL_STOP'; record['error'] = str(error); save()
            print(str(error), flush=True); return 1
        finally:
            if proc is not None:
                stop_owned(proc)
    journal['status'] = 'COMPLETE_DIAGNOSIS_CAPTURE_NOT_PERFORMANCE_ACCEPTANCE'; save()
    return 0

if __name__ == '__main__':
    sys.exit(main())
