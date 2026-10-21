import csv,math
from pathlib import Path
import numpy as np
R=Path(__file__).parent

def quat(q):
 x,y,z,w=q;return np.array([[1-2*y*y-2*z*z,2*x*y-2*w*z,2*x*z+2*w*y],[2*x*y+2*w*z,1-2*x*x-2*z*z,2*y*z-2*w*x],[2*x*z-2*w*y,2*y*z+2*w*x,1-2*x*x-2*y*y]])
def rot(axis,deg):
 a=np.array(axis,dtype=float);a/=np.linalg.norm(a);x,y,z=a;t=math.radians(deg);c=math.cos(t);s=math.sin(t);return c*np.eye(3)+(1-c)*np.outer(a,a)+s*np.array([[0,-z,y],[z,0,-x],[-y,x,0]])
def read(weapon,digit):
 data={d['name']:d for d in csv.DictReader((R/'neutral'/f'{weapon}-axes.csv').open())};out=[]
 for joint in ['Proximal','Intermediate','Distal']:
  d=data['Left'+digit+joint];out.append((np.array([float(d['p'+a]) for a in 'xyz']),quat([float(d['q'+a]) for a in 'xyzw']),np.array([float(d['c'+a]) for a in 'xyz']),np.array([float(d['s'+a]) for a in 'xyz'])))
 return out

def model(data,x,common):
 p=[data[0][0]];rs=[]
 for i,(oldp,oldr,c,s) in enumerate(data):
  if i==0:r=rot([0,0,1],x[4])@oldr@rot(c,common)@rot(s,x[3])@rot(c,-common-x[0])
  else:
   prev=data[i-1];p.append(p[-1]+rs[-1]@prev[1].T@(oldp-prev[0]));r=rs[-1]@prev[1].T@oldr@rot(c,-x[i])
  rs.append(r)
 # glTF finger bone local +Y is longitudinal in imported rotation; use segment
 # direction transformed through previous bone, preserves each digit rest bend.
 v=data[2][1].T@(data[2][0]-data[1][0]);v=v/np.linalg.norm(v)*.052
 p.append(p[-1]+rs[-1]@v)
 return np.array(p)

def optimize(data,target,common,thumb):
 def cost(x):
  p=model(data,x,common);err=np.sum((p[1:]-target)**2*np.array([[.8],[1],[.6]]))
  # Bias to relaxed ordinary joint bend; no extreme reversed distal hooks.
  err+=.000000008*np.sum(x[:3]**2)
  return err
 limits=[(-40,45),(-40,45),(-40,45),(-65,25),(-90,95) if thumb else (0,0)]
 rng=np.random.default_rng(42);best=(float('inf'),None)
 for trial in range(12):
  x=np.array([rng.uniform(a,b) if b>a else a for a,b in limits]);v=cost(x)
  for step in [20,10,5,2,1,.4]:
   for iteration in range(25):
    changed=False
    for i,(a,b) in enumerate(limits):
     for delta in [-step,step]:
      y=x.copy();y[i]=np.clip(y[i]+delta,a,b);cv=cost(y)
      if cv<v:x=y;v=cv;changed=True
    if not changed:break
  if v<best[0]:best=(v,x)
 return best[1],model(data,best[1],common),best[0]

out=[]
for weapon in ['Rifle','Shotgun','RocketLauncher','Cutter']:
 for digit in ['Thumb','Index','Middle','Ring','Little']:
  data=read(weapon,digit);p=data[0][0];thumb=digit=='Thumb';common=0 if thumb else (7 if weapon in ['Rifle','Shotgun'] else 2)
  # Targets on the outer support surface, different longitudinal spacing per digit.
  side=.16 if weapon=='Rifle' else .17 if weapon=='Shotgun' else .18
  if thumb:
   target=np.array([[-side-.015,p[1]+.105,p[2]+.025],[-side,p[1]+.198,p[2]+.035],[-side+.015,p[1]+.245,p[2]+.03]])
  else:
   dz=.015 if digit=='Index' else .004 if digit=='Middle' else -.009
   target=np.array([[max(p[0]+.055,side-.05),p[1]+.022,p[2]+dz],[side,p[1]+.105,p[2]+dz],[side-.025,p[1]+.15,p[2]+dz]])
  x,points,error=optimize(data,target,common,thumb)
  print(weapon,digit,'angles',np.round(x,1),'err',round(error,5),'points',np.round(points,3).tolist())
  for key,val in zip(['Proximal','Intermediate','Distal','Spread'],x):out.append(f'grip.{weapon}.left.{digit}.{key}={val:.2f}')
  if thumb:out.append(f'grip.{weapon}.thumbOpposition={x[4]:.2f}')
(R/'fit.txt').write_text('\n'.join(out)+'\n')
