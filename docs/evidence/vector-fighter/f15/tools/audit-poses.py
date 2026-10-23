import json,csv
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation
root=Path('.local/vector-f15/review');old=Path('.local/vector-f14/final-geometry');out={}
def bones(p):
 d={}
 for l in p.read_text().splitlines():
  a=l.split()
  if a[0]=='b':d[a[1]]=(np.array(list(map(float,a[2:5]))),Rotation.from_quat(list(map(float,a[5:9]))))
 return d
def relative(d,side,name):
 p,r=d[name];wp,wr=d[side+'Hand'];return wr.inv().apply(p-wp),wr.inv()*r
for weapon in ['Rifle','Shotgun','RocketLauncher','Cutter']:
 b=bones(root/(weapon+'-vector-armored-hands-skin.txt'));base=bones(old/(weapon+'-vector-armored-hands-skin.txt'))
 fixed={}
 for name in ['LeftUpperArm','LeftLowerArm','LeftHand','RightUpperArm','RightLowerArm','RightHand']:
  fixed[name]={'position_delta':float(np.linalg.norm(b[name][0]-base[name][0])),'angle_degrees':float(np.degrees((b[name][1].inv()*base[name][1]).magnitude()))}
 poses={}
 for phase in ['fire','rising','falling','landing']:
  d=bones(root/'poses'/(weapon+'-'+phase)/(weapon+'-vector-armored-hands-skin.txt'));diff={}
  for side in ['Left','Right']:
   for digit in ['Thumb','Index','Middle','Ring','Little']:
    values=[]
    for seg in ['Proximal','Intermediate','Distal']:
     p,r=relative(b,side,side+digit+seg);q,t=relative(d,side,side+digit+seg)
     values.append({'segment':seg,'position_delta':float(np.linalg.norm(p-q)),'angle_degrees':float(np.degrees((r.inv()*t).magnitude()))})
    diff[side+digit]=values
  poses[phase]=diff
 out[weapon]={'fixed_arms_vs_F14':fixed,'digit_pose_deltas_relative_to_wrist':poses}
Path('.local/vector-f15/pose-audit.json').write_text(json.dumps(out,indent=2)+'\n')
for w,x in out.items():
 print(w,'arm delta',max(d['position_delta'] for d in x['fixed_arms_vs_F14'].values()))
 for phase,dd in x['digit_pose_deltas_relative_to_wrist'].items():print(' ',phase,'max finger translation/angle',max(v['position_delta'] for d in dd.values() for v in d),max(v['angle_degrees'] for d in dd.values() for v in d))
