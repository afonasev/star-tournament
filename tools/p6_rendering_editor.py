#!/usr/bin/env python3
"""P6 bounded Editor operations. Never launches a Player or publishes anything."""
import argparse, hashlib, json, os, shutil, subprocess, sys, time
from pathlib import Path
import xml.etree.ElementTree as ET
WARM=Path('/Users/eaafonasev/.codex/worktrees/rendering-assets-followup/star-tournament')
PROJECT=WARM/'unity'
WRAPPER=PROJECT/'tools.sh'
FILTERS={'edit':'StarTournament.ProvingGround.Tests.RenderingBaselineContractTests','play':'StarTournament.ProvingGround.Tests.PlayMode.RenderingBaselineRestoreTests'}
def git(*args):return subprocess.check_output(['git','-C',str(WARM),*args],text=True).strip()
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('operation',choices=['edit','play','build']);p.add_argument('--app',type=Path);p.add_argument('--sha',required=True);p.add_argument('--branch',required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 assert Path.cwd()==WARM and Path(__file__).resolve()==WARM/'tools/p6_rendering_editor.py'
 assert WRAPPER.parent==PROJECT and git('rev-parse','HEAD')==a.sha and git('branch','--show-current')==a.branch
 assert not git('status','--porcelain'), 'Record/preserve generated settings and restore only owned diffs before each invocation'
 assert a.output.is_absolute() and not a.output.exists()
 free=shutil.disk_usage('/System/Volumes/Data').free;required=(5 if a.operation=='build' else 3)*1024**3
 assert free>=required, f'NotRun capacity {free} < {required}'
 a.output.mkdir(parents=True)
 args=['-batchmode','-noaudio','-muted','-logFile',str(a.output/'unity.log')]
 if a.operation=='build':
  assert a.app and a.app.is_absolute() and not a.app.exists(), 'New absolute retained QA artifact required'
  args+=['-quit','-executeMethod','StarTournament.ProvingGround.Editor.WellPlayerDiagnosticBuild.BuildRenderingFinal']
 else:args+=['-nographics','-runTests','-testPlatform','EditMode' if a.operation=='edit' else 'PlayMode','-testFilter',FILTERS[a.operation],'-testResults',str(a.output/'tests.xml')]
 env=os.environ.copy();env.update(UNITY_RUN_MODE='shared',STAR_TOURNAMENT_QA_LAB_HISTORY=str(a.output/'lab-history.json'),STAR_TOURNAMENT_QA_WIDTH='1920',STAR_TOURNAMENT_QA_HEIGHT='1080')
 assert not env.get('STAR_TOURNAMENT_QA_ARENA_FAMILY')
 if a.operation=='build':env['STAR_TOURNAMENT_WELL_PLAYER_OUTPUT']=str(a.app)
 cmd=[str(WRAPPER),'editor',*args]
 rec={'operation':a.operation,'source':a.sha,'branch':a.branch,'tree':git('rev-parse','HEAD^{tree}'),'command':cmd,'cwd':str(WARM),'runner_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'freeBytes':free,'requiredFreeBytes':required,'startedUnix':time.time(),'environment':{k:v for k,v in env.items() if k.startswith('STAR_TOURNAMENT_') or k=='UNITY_RUN_MODE'},'status':'RUNNING'}
 (a.output/'command.json').write_text(json.dumps(rec,indent=2))
 with (a.output/'wrapper.log').open('w') as log:result=subprocess.run(cmd,cwd=WARM,env=env,stdout=log,stderr=subprocess.STDOUT)
 rec.update(exitCode=result.returncode,finishedUnix=time.time(),postStatus=git('status','--porcelain'))
 ok=result.returncode==0
 if a.operation!='build':
  if (a.output/'tests.xml').exists():
   xml=ET.parse(a.output/'tests.xml').getroot();cases=list(xml.iter('test-case'));rec['testCases']=[{'name':c.get('fullname'),'result':c.get('result')} for c in cases];ok=ok and bool(cases) and all(c.get('result')=='Passed' for c in cases)
  else:ok=False;rec['missingXml']=True
 else:ok=ok and a.app.is_dir() and 'WELL_PLAYER_DIAGNOSTIC_BUILD_PASS' in (a.output/'unity.log').read_text(errors='replace')
 rec['status']='PASS_AFFECTED_NOT_FULL' if ok else 'NotRun' if rec.get('missingXml') else 'FAIL'
 (a.output/'command.json').write_text(json.dumps(rec,indent=2));print(json.dumps({k:v for k,v in rec.items() if k not in ['environment','testCases']}));return 0 if ok else 1
if __name__=='__main__':sys.exit(main())
