from pathlib import Path
import subprocess,os,time,json
root=Path('/Users/eaafonasev/Projects/star-tournament-menu-feedback');out=root/'.local/f1/native-final';out.mkdir(parents=True,exist_ok=True)
exe=root/'unity/Builds/StarTournamentProvingGround.app/Contents/MacOS/Star Tournament Unity Proving Ground'
cases=[('menu',['-menuStyleReview'],'MENU_STYLE_REVIEW_COMPLETE'),('setup',['-setupSettingsReview'],'NATIVE_SETUP_SETTINGS_REVIEW_COMPLETE'),('pause1',['-pauseReview'],'NATIVE_PAUSE_REVIEW_COMPLETE'),('pause4',['-pauseReview'],'NATIVE_PAUSE_REVIEW_COMPLETE'),('results',['-scoreboardReview','-scoreboardEvidence'],'NATIVE_SCOREBOARD_REVIEW_COMPLETE'),('lab',['-labReview'],'')]
results=[]
for name,flags,marker in cases:
 d=out/name;d.mkdir(exist_ok=True);log=d/'player.log';args=[str(exe),*flags,str(d),'-profilesPath',str(d/'profiles.json'),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-logFile',str(log)]
 if name.startswith('pause'):args+=['-pauseReviewCount',name[-1]]
 if name=='results':args+=['-resultsActionsOnly']
 env=dict(os.environ,STAR_TOURNAMENT_QA_LAB_HISTORY=str(d/'history.json'),STAR_TOURNAMENT_QA_SOURCE_REVISION=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip())
 with (d/'stdout.log').open('w') as stream:
  proc=subprocess.Popen(args,cwd=root,env=env,stdout=stream,stderr=subprocess.STDOUT);start=time.monotonic();terminated=False
  while proc.poll() is None:
   text=log.read_text(errors='replace') if log.exists() else ''
   if marker and marker in text:
    time.sleep(1)
    if proc.poll() is None:proc.terminate();terminated=True
    break
   if time.monotonic()-start>180 or 'InvalidOperationException:' in text or 'NullReferenceException:' in text:
    proc.terminate();terminated=True;break
   time.sleep(1)
  try:code=proc.wait(timeout=10)
  except subprocess.TimeoutExpired:proc.kill();code=proc.wait()
 text=log.read_text(errors='replace') if log.exists() else ''
 passed=marker in text if marker else (d/'first-run-complete.json').exists()
 result={'name':name,'passed':passed,'exit':code,'stoppedAfterMarker':terminated and passed,'seconds':round(time.monotonic()-start,1),'screenshots':len(list(d.glob('*.png')))};results.append(result)
 print(json.dumps(result),flush=True)
 (out/'results.json').write_text(json.dumps(results,indent=2))
 # A failed case is recorded; continue independent coverage.
raise SystemExit(0 if all(x['passed'] for x in results) else 1)
