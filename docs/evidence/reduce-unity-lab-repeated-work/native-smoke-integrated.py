from pathlib import Path
import json,subprocess,hashlib,plistlib
root=Path.cwd();out=root/'docs/evidence/reduce-unity-lab-repeated-work';candidate=json.loads((out/'candidate.json').read_text());app=Path(candidate['app']);info=plistlib.loads((app/'Contents/Info.plist').read_bytes());executable=app/'Contents/MacOS'/info['CFBundleExecutable'];qa=root/'.local/native-lab-integrated';qa.mkdir(parents=True,exist_ok=True);results=[]
for index in [1,2]:
 command=[str(executable),'-labReview',str(qa),'-profilesPath',str(qa/'profiles.json'),'-screen-width','1440','-screen-height','900','-screen-fullscreen','0','-logFile',str(qa/f'player-{index}.log')]
 print('Native Lab run '+str(index),flush=True);run=subprocess.run(command,timeout=180);assert run.returncode==0
 if index==1:
  first=json.loads((qa/'first-run-complete.json').read_text());assert first['muted'] is True
  prior=(qa/'history.json').read_bytes();first_selection=first['selected']
 else:
  selected=(qa/'restart-complete.txt').read_text();assert selected.replace('\n',' ')==first_selection
  assert (qa/'history.json').read_bytes()==prior
 results.append({'command':command,'exit_code':run.returncode})
record={'candidate':candidate['identity'],'source_revision':candidate['source_revision'],'classification':'AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE','muted':True,'selection_preserved':True,'history_bytes_unchanged_after_restart':True,'history_sha256':hashlib.sha256(prior).hexdigest(),'runs':results}
(qa/'smoke.json').write_text(json.dumps(record,indent=2)+'\n');print('Native Lab save/branch/delete/restart PASSED',flush=True)
