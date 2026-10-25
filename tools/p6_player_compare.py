#!/usr/bin/env python3
"""Strict P6 ABBA per-map runner; invoke only for an explicit fresh human focus window."""
import argparse,csv,datetime,hashlib,json,os,plistlib,shutil,struct,subprocess,sys,time
from pathlib import Path
WARM=Path('/Users/eaafonasev/.codex/worktrees/rendering-assets-followup/star-tournament')
MAPS=['combat-bowl-v1','industrial-tunnels-v1','lunar-laboratory-v1','well-laboratory-v1']
QUALITY=['width','height','quality','qualityName','aa','shadows','shadowDistance','shadowCascades','renderScale','vsync','frameCap','pipelineSettings','actualMap','composition']
def inventory(app):
 entries=[]
 for f in sorted(app.rglob('*')):
  if f.is_symlink():entries.append({'path':str(f.relative_to(app)),'symlink':os.readlink(f)})
  elif f.is_file():
   h=hashlib.sha256()
   with f.open('rb') as stream:
    for b in iter(lambda:stream.read(1024*1024),b''):h.update(b)
   entries.append({'path':str(f.relative_to(app)),'size':f.stat().st_size,'sha256':h.hexdigest()})
 return {'tree_sha256':hashlib.sha256(json.dumps(entries,sort_keys=True,separators=(',',':')).encode()).hexdigest(),'bytes':sum(x.get('size',0) for x in entries),'files':entries}
def validate(output,source,map_id,reference=None):
 report=json.loads((output/'report.json').read_text());assert report['status']=='COMPLETE_DIAGNOSTIC_NOT_ACCEPTANCE' and report['source']==source
 assert report['map']==map_id and report['seed']==20261025 and report['graphicsApi']=='Metal' and not report['batchMode']
 assert [s['name'] for s in report['samples']]==['ordinary1','frozen1','frozen4']
 ref=json.loads(reference.read_text()) if reference else None
 for s in report['samples']:
  name=s['name'];rows=list(csv.DictReader((output/(name+'.csv')).open()));dims=struct.unpack('>II',(output/(name+'.png')).read_bytes()[16:24]);state=json.loads(s['stateStart']);end=json.loads(s['stateEnd'])
  assert s['status']=='DIAGNOSTIC_NOT_ACCEPTANCE' and len(rows)==s['frameMs']['count']>0
  assert s['unfocusedFrames']==s['nonRunningFrames']==0 and all(r['focused']=='1' and r['running']=='1' for r in rows)
  assert (s['width'],s['height'])==dims==(1920,1080) and s['qualityName']=='Ultra' and s['renderScale']==1
  assert s['shadows']=='All' and s['shadowDistance']==150 and s['shadowCascades']==4 and s['vsync']==0 and s['frameCap']==-1
  assert state['Version']==end['Version']==10 and s['actualMap']==state['ArenaIdentity']==end['ArenaIdentity'] and s['actualMap'].split('@')[0]==map_id
  assert len(state['Poses'])==(4 if map_id=='industrial-tunnels-v1' else 8)
  assert s['gpuMs']['count']>0 and s['counters'][0]['count']>0 and s['counters'][0]['mean']>0 and s['counters'][2]['mean']>0
  assert s['presentationEnd']['realtime']-s['presentationStart']['realtime']>=(15 if name=='ordinary1' else 8)
  assert s['assetScope']['inclusiveOwnerMeshCount']>0 and s['meshBytes']>=s['assetScope']['inclusiveOwnerMeshBytes']>0
  if ref:
   other=next(x for x in ref['samples'] if x['name']==name)
   assert all(s[k]==other[k] for k in QUALITY), 'Matched quality/map/composition mismatch'
   rs=json.loads(other['stateStart']);assert state['DesignProfile']==rs['DesignProfile'] and state['Match']['TuningIdentity']==rs['Match']['TuningIdentity']
  if name.startswith('frozen'):
   assert s['stateStart']==s['stateEnd']==s['referenceState'] and s['camerasStart']==s['camerasEnd']
   if ref:assert s['stateStart']==other['stateStart'] and s['camerasStart']==other['camerasStart']
   visual=json.loads(s['visualStart']);vend=json.loads(s['visualEnd']);assert visual['motors']==vend['motors'] and all(m['position']==m['state']['Position'] for m in visual['motors'])
  else:assert s['simulationEnd']>s['simulationStart']
 return report
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',type=Path,required=True);p.add_argument('--map',choices=MAPS,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--deadline',required=True);p.add_argument('--human-source',required=True);a=p.parse_args()
 assert Path.cwd()==WARM and a.manifest.is_absolute() and a.output.is_absolute() and not a.output.exists()
 m=json.loads(a.manifest.read_text());assert subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()==m['optimized']['source']
 deadline=datetime.datetime.fromisoformat(a.deadline.replace('Z','+00:00')).timestamp();minimum=m['perLaunchBudgetSeconds'];assert minimum>=90 and deadline>time.time()
 for variant in ['control','optimized']:
  item=m[variant];app=Path(item['app']);assert inventory(app)['tree_sha256']==item['tree_sha256']
 assert (WARM/'tools/p6_player_gate.py').stat().st_mode&0o111
 a.output.mkdir(parents=True);journal={'humanSource':a.human_source,'deadline':a.deadline,'manifestSha256':hashlib.sha256(a.manifest.read_bytes()).hexdigest(),'events':[],'status':'RUNNING'}
 def event(kind,**data):
  journal['events'].append({'atUnix':time.time(),'kind':kind,**data});(a.output/'window.json').write_text(json.dumps(journal,indent=2));print(kind,flush=True)
 reference=None
 for label,variant in [('A1','control'),('B1','optimized'),('B2','optimized'),('A2','control')]:
  if deadline-time.time()<minimum or shutil.disk_usage('/System/Volumes/Data').free<3*1024**3:
   journal['status']='NotRun_REMAINING_WINDOW_OR_CAPACITY';event('NotRun_BEFORE_DISPATCH',label=label);return 75
  item=m[variant];output=a.output/label;output.mkdir();app=Path(item['app']);env=os.environ.copy();env.update(UNITY_RUN_MODE='exclusive',UNITY_RUNNER=str(WARM/'tools/p6_player_gate.py'),UNITY_PLAYER_APP=str(app),STAR_TOURNAMENT_QA_LAB_HISTORY=str(output/'lab-history.json'),STAR_TOURNAMENT_BASELINE_SOURCE=item['source'],P6_HUMAN_AUTHORITY=a.human_source,P6_HUMAN_DEADLINE=str(deadline),P6_LAUNCH_BUDGET=str(minimum),P6_ADMISSION_RECORD=str(output/'admission.json'))
  cmd=[str(WARM/'unity/tools.sh'),'run','-noaudio','-muted','-renderingBaseline','-baselineMap',a.map,'-baselineEvidence',str(output),'-screen-width','1920','-screen-height','1080','-logFile',str(output/'player.log')]
  if reference:cmd+=['-baselineReference',str(reference)]
  record={'source':item['source'],'app':str(app),'tree_sha256':item['tree_sha256'],'command':cmd,'environment':{k:v for k,v in env.items() if k.startswith(('P6_','STAR_TOURNAMENT_','UNITY_'))},'startedUnix':time.time()};(output/'command.json').write_text(json.dumps(record,indent=2));event('dispatch',label=label,variant=variant)
  with (output/'wrapper.log').open('w') as log:result=subprocess.run(cmd,env=env,cwd=WARM,stdout=log,stderr=subprocess.STDOUT)
  record.update(exitCode=result.returncode,finishedUnix=time.time());(output/'command.json').write_text(json.dumps(record,indent=2))
  try:
   assert result.returncode==0, f'Player/queue exit {result.returncode}'
   validate(output,item['source'],a.map,reference);assert time.time()<=deadline, 'Human window exceeded; never dispatch next run'
   (output/'acceptance.json').write_text(json.dumps({'status':'PASS_FOCUSED_DIAGNOSTIC_NOT_HUMAN_ACCEPTANCE'},indent=2))
  except Exception as error:
   journal['status']='FAIL_OR_NOTRUN_STOP';event('STOP',label=label,error=str(error));(output/'acceptance.json').write_text(json.dumps({'status':'NotRun' if result.returncode==75 else 'FAIL','error':str(error)},indent=2));return 1
  if reference is None:reference=output/'report.json'
  event('PASS',label=label,elapsedSeconds=record['finishedUnix']-record['startedUnix'])
 journal['status']='COMPLETE_ABBA_DESCRIPTIVE_NOT_SIGNIFICANCE';event('complete');return 0
if __name__=='__main__':sys.exit(main())
