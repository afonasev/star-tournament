#!/usr/bin/env python3
"""Run inside unity-run --exclusive. Isolated muted map and real F8 history migration QA."""
import argparse,gzip,hashlib,json,os,plistlib,subprocess,time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--baseline',type=Path,required=True);p.add_argument('--candidate',type=Path,required=True);a=p.parse_args();root=Path(__file__).resolve().parent;events=[]
def save():(root/'native-events.json').write_text(json.dumps(events,indent=2)+'\n')
def run(app,name,flags,profiles):
 app=app.resolve();out=root/name;out.mkdir(parents=True,exist_ok=True)
 exe=plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
 argv=[str(app/'Contents/MacOS'/exe),*flags,'-profilesPath',str(profiles),'-logFile',str(out/(name+'-'+str(len(events))+'.log'))]
 env=dict(os.environ,STAR_TOURNAMENT_QA_LAB_HISTORY=str(out/'qa-lab-history.json'))
 proc=subprocess.Popen(argv,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT);e={'name':name,'app':str(app),'pid':proc.pid,'argv':argv,'start_unix':time.time()};events.append(e);save();print('BEGIN',name,proc.pid,flush=True)
 try:code=proc.wait(timeout=300)
 except subprocess.TimeoutExpired:proc.terminate();proc.wait(timeout=30);code=124
 e.update(exit_code=code,end_unix=time.time());save();print('END',name,code,flush=True);assert code==0,e
lab=root/'legacy-lab';lab.mkdir(exist_ok=True)
run(a.baseline,'legacy-lab',['-labReview',str(lab),'-screen-width','1440','-screen-height','900','-screen-fullscreen','0'],lab/'legacy-profiles.json')
assert json.loads((lab/'first-run-complete.json').read_text())['muted'] is True
before=(lab/'history.json').read_bytes();old=json.loads(before)
gzip.open(lab/'before-f8-history.json.gz','wb').write(before)
original={(p['Id'],r['Number']):r for p in old['Profiles'] for r in p['Revisions']}
run(a.candidate,'legacy-lab',['-labReview',str(lab),'-screen-width','1440','-screen-height','900','-screen-fullscreen','0'],lab/'current-profiles.json')
after=(lab/'history.json').read_bytes();new=json.loads(after);migrated={(p['Id'],r['Number']):r for p in new['Profiles'] for r in p['Revisions']}
for key,r in original.items():
 assert migrated[key]['Hash']==r['Hash'],key
 for oldp,newp in zip(r['Snapshot']['Profiles'],migrated[key]['Snapshot']['Profiles']):assert oldp['values']==newp['values'],(key,oldp['id'])
assert new['SelectedId']==old['SelectedId'],'migration lost selected profile'
assert new['SelectedRevision']>old['SelectedRevision'],'selected legacy revision was not upgraded'
oldselected=original[(old['SelectedId'],old['SelectedRevision'])]['Snapshot']
selected=migrated[(new['SelectedId'],new['SelectedRevision'])]['Snapshot']
values=lambda bundle:{v['Path']:v['Value'] for profile in bundle['Profiles'] for v in profile['values']}
oldvalues=values(oldselected);newvalues=values(selected)
for key in ['rifle.damage','armor.pickupAmount']:assert newvalues[key]==oldvalues[key],key
assert next(p['version'] for p in selected['Profiles'] if p['id']=='lunar-laboratory-authoring-v1')==2
for key,expected in [('cargo-f2-south-west-wall',-8),('cargo-south-west',-7.6),('cargo-north-east',9)]:assert abs(newvalues['lunar.map.'+key+'.position.z']-expected)<1e-5,key
selected1=(lab/'restart-complete.txt').read_text();gzip.open(lab/'after-migration-history.json.gz','wb').write(after)
run(a.candidate,'legacy-lab',['-labReview',str(lab),'-screen-width','1440','-screen-height','900','-screen-fullscreen','0'],lab/'current-profiles.json')
assert (lab/'history.json').read_bytes()==after,'restart changed history bytes'
assert (lab/'restart-complete.txt').read_text()==selected1,'restart changed selection'
(lab/'migration-result.json').write_text(json.dumps({'result':'PASS','muted':True,'original_revisions_preserved':len(original),'original_hashes_and_values_preserved':True,'restart_byte_identical':True,'selected':selected1,'after_sha256':hashlib.sha256(after).hexdigest(),'human_acceptance':False},indent=2)+'\n')
visual=root/'exact-visual';visual.mkdir(exist_ok=True)
run(a.candidate,'exact-visual',['-lunarReview','-lunarF6','-lunarF7','-lunarF9','-lunarF10','-lunarSilentReview','-lunarEvidence',str(visual)],visual/'qa-profiles.json')
assert json.loads((visual/'complete.json').read_text())['status']=='PASS'
print('ALL NATIVE QA PASS',flush=True)
