#!/usr/bin/env python3
"""Reproduce F8 QA under one host-exclusive unity-run command. No acceptance implied."""
import argparse,json,os,plistlib,subprocess,time
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('--baseline',type=Path,required=True);parser.add_argument('--candidate',type=Path,required=True);args=parser.parse_args()
root=Path(__file__).resolve().parent;events=[]
def run(app,key,flags,plan=None):
 app=app.resolve();directory=root/key;directory.mkdir(exist_ok=True)
 with (app/'Contents/Info.plist').open('rb') as f:exe=plistlib.load(f)['CFBundleExecutable']
 command=[str(app/'Contents/MacOS'/exe),*flags,'-lunarEvidence',str(directory),'-profilesPath',str(directory/'qa-profiles.json'),'-lunarSilentReview','-logFile',str(directory/'player.log')]
 if plan:command+=['-lunarStudy',str(root/'plans'/plan)]
 env=dict(os.environ,STAR_TOURNAMENT_QA_LAB_HISTORY=str(directory/'qa-lab-history.json'))
 p=subprocess.Popen(command,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT)
 item={'name':key,'app':str(app),'pid':p.pid,'start_unix':time.time(),'argv':command,'qa_lab_history':env['STAR_TOURNAMENT_QA_LAB_HISTORY']};events.append(item);save();print('BEGIN',key,p.pid,flush=True)
 try:code=p.wait(timeout=1400)
 except subprocess.TimeoutExpired:
  p.kill();p.wait();code=124
 item.update(exit_code=code,end_unix=time.time());save();print('END',key,code,flush=True)
 if code:raise SystemExit(code)
def save():(root/'final-batch-events.json').write_text(json.dumps(events,indent=2))
run(args.baseline,'f7-exact-visual',['-lunarReview','-lunarF6','-lunarF7','-lunarVisualOnly'])
run(args.candidate,'exact-frozen',['-lunarPerformance','-screen-width','3456','-screen-height','2160','-lunarFullscreen'],'final-frozen.json')
# Keep rejected samples intact; repeat an invalid focus sample at most once.
report=json.loads((root/'exact-frozen/performance.json').read_text())
bad={s['name'] for s in report['samples'] if not s['focused']}
if bad:
 plan=json.loads((root/'plans/final-frozen.json').read_text());plan['steps']=[s for s in plan['steps'] if s['name'] in bad]
 (root/'plans/focus-repeat.json').write_text(json.dumps(plan,indent=2))
 run(args.candidate,'exact-focus-repeat',['-lunarPerformance','-screen-width','3456','-screen-height','2160','-lunarFullscreen'],'focus-repeat.json')
run(args.candidate,'exact-live-baseline',['-lunarPerformance','-screen-width','3456','-screen-height','2160','-lunarFullscreen'],'live-baseline.json')
report=json.loads((root/'exact-live-baseline/performance.json').read_text())
if any(not s['focused'] for s in report['samples']):
 run(args.candidate,'exact-live-baseline-repeat',['-lunarPerformance','-screen-width','3456','-screen-height','2160','-lunarFullscreen'],'live-baseline.json')
run(args.candidate,'exact-visual',['-lunarReview','-lunarF6','-lunarF7'])
