#!/usr/bin/env python3
"""One OS lockfile: four shared Unity commands or one exclusive command."""
import argparse
import errno
import fcntl
import hashlib
import json
import os
from pathlib import Path
import select
import signal
import subprocess
import sys
import time

SLOTS = 4
LOCK = Path(os.environ.get('UNITY_RUN_LOCK_FILE', str(Path.home() / '.local/state/unity-run/host.lock')))

class Cancelled(Exception):
    pass


def alive(control):
    if select.select([control], [], [], 0)[0]:
        return bool(os.read(control, 1))
    return True


def acquire(fd, start, length, control, deadline):
    while True:
        if not alive(control):
            raise Cancelled('queued client exited')
        try:
            fcntl.lockf(fd, fcntl.LOCK_EX | fcntl.LOCK_NB, length, start, os.SEEK_SET)
            return
        except OSError as e:
            if e.errno not in (errno.EAGAIN, errno.EACCES):
                raise
        if deadline is not None and time.monotonic() >= deadline:
            raise TimeoutError('Unity capacity unavailable before timeout')
        select.select([control], [], [], 0.1)


def unlock(fd, start, length):
    fcntl.lockf(fd, fcntl.LOCK_UN, length, start, os.SEEK_SET)


def project_byte(project):
    # Different canonical paths use different bytes; no project lock directory/database.
    digest = hashlib.sha256(str(Path(project).resolve()).encode()).digest()
    return 8 + (int.from_bytes(digest[:8], 'big') & ((1 << 60) - 1))


def admit(fd, exclusive, control, deadline, project):
    if project:
        acquire(fd, project_byte(project), 1, control, deadline)
    # Exclusive holds admission while existing commands drain. No upgrades/nested locks.
    acquire(fd, 0, 1, control, deadline)
    if exclusive:
        acquire(fd, 1, SLOTS, control, deadline)
        unlock(fd, 0, 1)
        return 'exclusive'
    while True:
        for slot in range(1, SLOTS + 1):
            try:
                fcntl.lockf(fd, fcntl.LOCK_EX | fcntl.LOCK_NB, 1, slot, os.SEEK_SET)
                unlock(fd, 0, 1)
                return f'shared:{slot}'
            except OSError as e:
                if e.errno not in (errno.EAGAIN, errno.EACCES):
                    raise
        # Holding admission while full does not block active command exits.
        if not alive(control):
            raise Cancelled('queued client exited')
        if deadline is not None and time.monotonic() >= deadline:
            raise TimeoutError('Unity capacity unavailable before timeout')
        select.select([control], [], [], 0.1)


def status(fd):
    busy = []
    for byte in range(0, SLOTS + 1):
        try:
            fcntl.lockf(fd, fcntl.LOCK_EX | fcntl.LOCK_NB, 1, byte, os.SEEK_SET)
            unlock(fd, byte, 1)
        except OSError as e:
            if e.errno not in (errno.EAGAIN, errno.EACCES):
                raise
            busy.append(byte)
    print(json.dumps({'lock_file': str(LOCK), 'shared_limit': SLOTS,
                      'occupied_slots': len([b for b in busy if b]),
                      'admission_busy': 0 in busy}))


def guard(args, control):
    os.setsid()  # Session/client cancellation cannot silently free a live command's locks.
    workload = None
    cancelled = False

    def stop(signum, _frame):
        nonlocal cancelled
        cancelled = True
        if workload is not None:
            try:
                os.killpg(workload.pid, signum)
            except ProcessLookupError:
                pass
        else:
            raise Cancelled('cancelled before launch')

    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    # Diagnostic output must not crash the lock holder if its original reader disappeared.
    def notice(message):
        try:
            print(message, file=sys.stderr, flush=True)
        except (BrokenPipeError, OSError):
            pass

    LOCK.parent.mkdir(parents=True, exist_ok=True)
    fd = os.open(LOCK, os.O_RDWR | os.O_CREAT, 0o600)
    try:
        deadline = None if args.timeout is None else time.monotonic() + args.timeout
        notice(f'unity-run: waiting {"exclusive" if args.exclusive else "shared"}; project={args.project or "none"}')
        slot = admit(fd, args.exclusive, control, deadline, args.project)
        if not alive(control):
            raise Cancelled('client exited before launch')
        signals = {signal.SIGTERM, signal.SIGINT}
        old_mask = signal.pthread_sigmask(signal.SIG_BLOCK, signals)
        try:
            workload = subprocess.Popen(args.command, start_new_session=True,
                                        preexec_fn=lambda: signal.pthread_sigmask(signal.SIG_SETMASK, old_mask))
        finally:
            signal.pthread_sigmask(signal.SIG_SETMASK, old_mask)
        notice(f'unity-run: started {slot}; guardian={os.getpid()} pid={workload.pid}')
        while True:
            try:
                result = workload.wait(timeout=0.2)
                break
            except subprocess.TimeoutExpired:
                if cancelled:
                    try:
                        result = workload.wait(timeout=5)
                        break
                    except subprocess.TimeoutExpired:
                        os.killpg(workload.pid, signal.SIGKILL)
                        result = workload.wait()
                        break
        notice(f'unity-run: finished pid={workload.pid} exit={result}; capacity released')
        return result if result >= 0 else 128 - result
    except Cancelled:
        return 130
    except TimeoutError as e:
        notice(f'unity-run: {e}')
        return 75
    except OSError as e:
        notice(f'unity-run: {e}')
        return 127
    finally:
        os.close(fd)
        os.close(control)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--shared', action='store_true')
    mode.add_argument('--exclusive', action='store_true')
    mode.add_argument('--status', action='store_true')
    parser.add_argument('--project', help='Unity project directory; canonical aliases serialize')
    parser.add_argument('--timeout', type=float, help='optional admission timeout, seconds')
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.status:
        LOCK.parent.mkdir(parents=True, exist_ok=True)
        fd = os.open(LOCK, os.O_RDWR | os.O_CREAT, 0o600)
        try:
            status(fd)
        finally:
            os.close(fd)
        return 0
    if not args.shared and not args.exclusive:
        parser.error('choose --shared or --exclusive')
    if args.timeout is not None and args.timeout < 0:
        parser.error('--timeout must be nonnegative')
    if args.command and args.command[0] == '--':
        args.command.pop(0)
    if not args.command:
        parser.error('provide the direct Editor/Player executable after --')
    if Path(args.command[0]).name in {'open', 'nohup'}:
        parser.error('launch the direct executable, not a detached launcher')
    if not args.project and '-projectPath' in args.command:
        i = args.command.index('-projectPath')
        if i + 1 >= len(args.command):
            parser.error('-projectPath requires a directory')
        args.project = args.command[i + 1]
    read_fd, write_fd = os.pipe()
    child = os.fork()
    if child == 0:
        os.close(write_fd)
        result = guard(args, read_fd)
        os._exit(result)
    os.close(read_fd)

    def cancel(signum, _frame):
        try:
            os.kill(child, signum)
        except ProcessLookupError:
            pass

    signal.signal(signal.SIGTERM, cancel)
    signal.signal(signal.SIGINT, cancel)
    try:
        _, result = os.waitpid(child, 0)
        return os.waitstatus_to_exitcode(result) if os.WIFEXITED(result) else 128 + os.WTERMSIG(result)
    finally:
        os.close(write_fd)


if __name__ == '__main__':
    sys.exit(main())
