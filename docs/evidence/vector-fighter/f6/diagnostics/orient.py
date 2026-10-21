import csv,numpy as np
from scipy.spatial.transform import Rotation as R
from scipy.optimize import least_squares
from pathlib import Path
root=Path.cwd();out=[]
for weapon,oldyaw in [('RocketLauncher',40),('Cutter',35)]:
 d={x[0]:np.array([float(v) for v in x[1:]]) for x in list(csv.reader((root/'docs/evidence/vector-fighter/f4-diagnostics/neutral'/f'{weapon}-grip.csv').open()))[1:] if len(x)==4}
 long=d['LeftMiddleProximal']-d['LeftHand'];long/=np.linalg.norm(long)
 row=d['LeftLittleProximal']-d['LeftIndexProximal'];row-=long*row.dot(long);row/=np.linalg.norm(row)
 def rot(axis,deg):return R.from_rotvec(np.array(axis)*np.deg2rad(deg)).as_matrix()
 base=rot([0,1,0],-oldyaw);lb=base@long;rb=base@row
 targetlong=np.array([.18,.20,.963]);targetlong/=np.linalg.norm(targetlong)
 targetrow=np.array([0,-1,.20]);targetrow-=targetlong*targetrow.dot(targetlong);targetrow/=np.linalg.norm(targetrow)
 def matrix(x):return rot([1,0,0],x[0])@rot([0,1,0],x[1])@rot(lb,-x[2])@base
 def loss(x):q=matrix(x);return np.r_[q@long-targetlong,q@row-targetrow]
 fit=least_squares(loss,[0,0,70],bounds=([-120,-100,-35],[120,100,145]))
 x=np.round(fit.x);print(weapon,'pitch,yaw,totalroll',x,'residual',np.linalg.norm(loss(x)))
 for k,v in [('supportPitch',x[0]),('supportYaw',x[1]),('supportRoll',x[2]-55),('supportX',-.08),('supportY',-.16),('supportZ',.49 if weapon=='RocketLauncher' else .406),('thumbOpposition',0)]:out.append(f'grip.{weapon}.{k}={v}')
 for digit in ['Thumb','Index','Middle','Ring','Little']:
  for segment in ['Proximal','Intermediate','Distal','Spread']:out.append(f'grip.{weapon}.left.{digit}.{segment}=0')
(root/'.local/vector-f6/vertical-neutral.txt').write_text('\n'.join(out)+'\n')
