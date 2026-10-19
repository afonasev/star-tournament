from pathlib import Path
import os,subprocess,time,json,uuid,plistlib
root=Path(__file__).resolve().parent
app=root.parent/'players/StarTournament.app'
info=plistlib.loads((app/'Contents/Info.plist').read_bytes())
exe=app/'Contents/MacOS'/info['CFBundleExecutable']
marker=root/'ready.json';assert not marker.exists();token=uuid.uuid4().hex
env=dict(os.environ,STAR_TOURNAMENT_STARTUP_READY=str(marker),STAR_TOURNAMENT_STARTUP_VERSION='0.1.6',STAR_TOURNAMENT_STARTUP_TOKEN=token,STAR_TOURNAMENT_QA_LAB_HISTORY=str(root/'isolated-history.json'))
args=[str(exe),'-batchmode','-noaudio','-muted','-logFile',str(root/'Player.log')]
p=subprocess.Popen(args,env=env,stdout=(root/'stdout.log').open('w'),stderr=subprocess.STDOUT)
started=time.monotonic();status='NotRun'
try:
 while p.poll() is None and time.monotonic()-started<120:
  if marker.exists():
   ready=json.loads(marker.read_text());assert ready=={'schema':1,'version':'0.1.6','token':token,'phase':'menu-ready'};status='passed-menu-ready';break
  time.sleep(.25)
 if status!='passed-menu-ready':raise RuntimeError('Player did not report current ready challenge')
finally:
 if p.poll() is None:p.terminate()
 try:p.wait(timeout=15)
 except subprocess.TimeoutExpired:p.kill();p.wait()
 (root/'result.json').write_text(json.dumps({'status':status,'args':args,'elapsedSeconds':time.monotonic()-started,'ready':json.loads(marker.read_text()) if marker.exists() else None,'exitCodeAfterOwnedQaTermination':p.returncode,'scope':'Release Player critical startup and two-loop menu-ready only; not rendering, installed launcher, Windows or human acceptance'},indent=2)+'\n')
print(status)
