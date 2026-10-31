#!/usr/bin/env python3
"""Wrapper adapter: shared lock implementation, late admission fails before Player exec."""
import json,os,shutil,sys,time
from pathlib import Path
REAL=Path('/Users/eaafonasev/.local/bin/unity-run')
def permitted(now,deadline,minimum,free):
 return deadline-now>=minimum and free>=3*1024**3
if __name__=='__main__':
 deadline=float(os.environ['P6_HUMAN_DEADLINE']);minimum=float(os.environ['P6_LAUNCH_BUDGET']);assert os.environ['P6_HUMAN_AUTHORITY']
 if sys.argv[1]=='--admitted':
  cmd=sys.argv[2:];now=time.time();free=shutil.disk_usage('/System/Volumes/Data').free
  rec={'admittedUnix':now,'deadlineUnix':deadline,'remaining':deadline-now,'requiredSeconds':minimum,'freeBytes':free,'command':cmd,'status':'ADMITTED' if permitted(now,deadline,minimum,free) else 'NotRun_LATE_OR_CAPACITY'}
  Path(os.environ['P6_ADMISSION_RECORD']).write_text(json.dumps(rec,indent=2))
  if not permitted(now,deadline,minimum,free):sys.exit(75)
  assert '-noaudio' in cmd and '-muted' in cmd
  os.execv(cmd[0],cmd)
 else:
  assert sys.argv[1:3]==['--exclusive','--']
  wait=deadline-time.time()-minimum
  if wait<=0:sys.exit(75)
  os.execv(str(REAL),[str(REAL),'--exclusive','--timeout',str(wait),'--',sys.executable,str(Path(__file__).resolve()),'--admitted',*sys.argv[3:]])
