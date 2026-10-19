import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest

RUNNER = Path(__file__).with_name('unity_run.py')
WORKER = r"""
import json,os,sys,time
from pathlib import Path
root=Path(sys.argv[1]);name=sys.argv[2];duration=float(sys.argv[3])
if len(sys.argv)>4:
    os.closerange(3,256)
(root/(name+'.start')).write_text(json.dumps({'at':time.monotonic(),'pid':os.getpid()}))
time.sleep(duration)
(root/(name+'.end')).write_text(str(time.monotonic()))
"""

class LockTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.env = {**os.environ, 'UNITY_RUN_LOCK_FILE':str(self.root/'host.lock')}
        self.jobs = []
        self.logs = []

    def tearDown(self):
        for p in self.jobs:
            if p.poll() is None:
                p.terminate()
                try:p.wait(timeout=7)
                except subprocess.TimeoutExpired:p.kill();p.wait()
        for f in self.logs:f.close()
        self.tmp.cleanup()

    def launch(self, name, mode='shared', seconds=0.6, project=None, close_fds=False, timeout=None, command=None):
        argv=[sys.executable,str(RUNNER),'--'+mode]
        if project:argv+=['--project',str(project)]
        if timeout is not None:argv+=['--timeout',str(timeout)]
        argv+=['--']+(command or [sys.executable,'-c',WORKER,str(self.root),name,str(seconds)]+(['close'] if close_fds else []))
        log=(self.root/(name+'.log')).open('w');self.logs.append(log)
        p=subprocess.Popen(argv,env=self.env,stdout=log,stderr=log);self.jobs.append(p)
        return p

    def started(self, name, timeout=8):
        path=self.root/(name+'.start');end=time.monotonic()+timeout
        while time.monotonic()<end:
            if path.exists():
                try:return json.loads(path.read_text())
                except json.JSONDecodeError:pass
            time.sleep(0.02)
        self.fail('did not start '+name+' '+(self.root/(name+'.log')).read_text())

    def interval(self,name):
        return self.started(name)['at'],float((self.root/(name+'.end')).read_text())

    def finish(self,p,expected=0):
        self.assertEqual(p.wait(timeout=12),expected)

    def test_shared_limit_four(self):
        ps=[self.launch(str(i)) for i in range(5)]
        for p in ps:self.finish(p)
        events=[]
        for i in range(5):
            a,b=self.interval(str(i));events.extend([(a,1),(b,-1)])
        active=peak=0
        for _,delta in sorted(events):active+=delta;peak=max(active,peak)
        self.assertEqual(peak,4)

    def test_exclusive_blocks_new_shared_while_draining(self):
        a=self.launch('a',seconds=0.7);self.started('a')
        x=self.launch('x','exclusive',seconds=0.4)
        time.sleep(0.15)
        b=self.launch('b')
        for p in (a,x,b):self.finish(p)
        self.assertGreaterEqual(self.interval('x')[0],self.interval('a')[1])
        self.assertGreaterEqual(self.interval('b')[0],self.interval('x')[1])

    def test_exclusive_no_overlap_with_exclusive(self):
        a=self.launch('a','exclusive');self.started('a')
        b=self.launch('b','exclusive')
        self.finish(a);self.finish(b)
        self.assertGreaterEqual(self.interval('b')[0],self.interval('a')[1])

    def test_same_project_symlink_serializes(self):
        project=self.root/'project';project.mkdir();alias=self.root/'alias';alias.symlink_to(project)
        a=self.launch('a',project=project);self.started('a')
        b=self.launch('b',project=alias)
        self.finish(a);self.finish(b)
        self.assertGreaterEqual(self.interval('b')[0],self.interval('a')[1])

    def test_killed_client_keeps_live_workload_locked_even_if_it_closes_fds(self):
        a=self.launch('a','exclusive',seconds=0.8,close_fds=True);self.started('a')
        a.kill();a.wait()
        b=self.launch('b',seconds=0.1)
        self.finish(b)
        self.assertGreaterEqual(self.interval('b')[0],self.interval('a')[1])

    def test_killed_queued_client_never_launches(self):
        a=self.launch('a','exclusive',seconds=0.7);self.started('a')
        b=self.launch('b');time.sleep(0.12);b.kill();b.wait()
        self.finish(a);time.sleep(0.2)
        self.assertFalse((self.root/'b.start').exists())
        c=self.launch('c',seconds=0.1);self.finish(c)

    def test_normal_cancel_stops_workload_and_releases(self):
        a=self.launch('a','exclusive',seconds=30);pid=self.started('a')['pid']
        a.terminate();self.finish(a,143)
        with self.assertRaises(ProcessLookupError):os.kill(pid,0)
        b=self.launch('b',seconds=0.1);self.finish(b)

    def test_workload_signal_releases(self):
        a=self.launch('a','exclusive',seconds=30);os.kill(self.started('a')['pid'],signal.SIGKILL)
        self.finish(a,137)
        b=self.launch('b',seconds=0.1);self.finish(b)

    def test_failed_spawn_nonzero_and_timeout_release(self):
        a=self.launch('missing','exclusive',command=['/no/such/unity']);self.finish(a,127)
        b=self.launch('bad','exclusive',command=[sys.executable,'-c','raise SystemExit(9)']);self.finish(b,9)
        c=self.launch('c','exclusive');self.started('c')
        d=self.launch('d',timeout=0.1);self.finish(d,75);self.finish(c)
        e=self.launch('e',seconds=0.1);self.finish(e)

if __name__=='__main__':unittest.main(verbosity=2)
