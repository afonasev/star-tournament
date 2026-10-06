#!/usr/bin/env python3
"""Drive the actual installed launcher's explicit IPC commands for package QA."""
import argparse,json,time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--sessions',type=Path,required=True);p.add_argument('--installed-release',type=Path,required=True);p.add_argument('--old-version',required=True);p.add_argument('--mode',choices=['restart','next'],required=True);p.add_argument('--evidence',type=Path,required=True);a=p.parse_args()
end=time.monotonic()+1200;events=[];sent=False;ready=False;ack=False;initial=a.installed_release.read_bytes();failure=None
try:
 while time.monotonic()<end:
  paths=list(a.sessions.glob('*/status.json'))
  if paths:
   try:
    status=max(paths,key=lambda item:item.stat().st_mtime_ns)
    d=json.loads(status.read_text())
   except (OSError,json.JSONDecodeError):time.sleep(.1);continue
   if not events or d!=events[-1]['status']:events.append({'time':time.time(),'status':d});print(d,flush=True)
   command=status.parent/'command.json'
   def send(action):
    tmp=command.with_suffix('.qa-next');tmp.write_text(json.dumps({'action':action}));tmp.replace(command)
   if d['state']=='available' and not sent:send('download');sent=True
   if d['state']=='staged':
    assert a.installed_release.read_bytes()==initial,'Installed identity changed while Player alive'
    assert json.loads(initial)['version']==a.old_version
    ready=True
    if a.mode=='next':break
    if not d.get('restartAcknowledged'):send('restart')
   if d.get('restartAcknowledged'):ack=True;break
   if d['state']=='error':raise RuntimeError(d['error'])
  elif sent:raise RuntimeError('Player ended before a verified package was staged')
  time.sleep(.1)
 assert ready and (a.mode=='next' or ack),'No staged/acknowledged update'
except Exception as ex:
 failure=str(ex);raise
finally:
 a.evidence.parent.mkdir(parents=True,exist_ok=True);a.evidence.write_text(json.dumps({'mode':a.mode,'download_sent':sent,'ready':ready,'restart_acknowledged':ack,'failure':failure,'events':events},indent=2)+'\n')
