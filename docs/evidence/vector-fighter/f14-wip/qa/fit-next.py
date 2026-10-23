from pathlib import Path
import csv,json,numpy as np
from scipy.spatial.transform import Rotation as R
from scipy.optimize import least_squares
root=Path('.local/vector-f13/final-native');out=Path('.local/vector-f14/fit-next');out.mkdir(exist_ok=True);report={};lines=[]
def rot(axis,deg):return R.from_rotvec(np.asarray(axis)*np.deg2rad(deg)).as_matrix()
def sdf(p,c,e):
 d=np.abs(p-c)-e;return np.linalg.norm(np.maximum(d,0),axis=-1)+np.minimum(np.max(d,axis=-1),0)
lefts=[ [[-25,45,-36,-62],[-11,-14,45,-36],[-15,30,25,-32],[2,45,4,-48],[2,45,-3,-32]], [[10,33,-40,-65],[-10,-22,45,-41],[-15,24,33,-37],[1,45,3,-52],[4,45,-2,-36]], [[25,7,33,-32],[2,-6,9,-12],[7,1,-10,-4],[22,-21,-12,-9],[10,-33,10,1]], [[19,12,43,-31],[2,-7,10,-12],[8,-2,-17,-5],[22,-22,-17,-9],[4,-37,-4,-1]] ]
rights=[ [[-15,10,8,-10],[-8,-12,-8,3],[0,0,0,0],[3,4,2,-2],[6,6,4,-3]], [[-13,12,9,-10],[-10,-14,-10,4],[0,0,0,0],[4,3,4,-2],[8,7,5,-4]], [[-10,14,10,-10],[-6,-10,-6,2],[0,0,0,0],[2,5,3,-1],[5,8,5,-3]], [[-11,13,11,-10],[-7,-9,-5,2],[0,0,0,0],[3,6,4,-2],[7,9,6,-4]] ]
for wi,weapon in enumerate(['Rifle','Shotgun','RocketLauncher','Cutter']):
 axes={a['name']:a for a in csv.DictReader((root/(weapon+'-axes.csv')).open())};report[weapon]={}
 for side in ['Left','Right']:
  for di,digit in enumerate(['Thumb','Index','Middle','Ring','Little']):
   names=[side+digit+s for s in ['Proximal','Intermediate','Distal']];data=[axes[n] for n in names];p0=[np.array([float(a[k]) for k in ['px','py','pz']]) for a in data];r0=[R.from_quat([float(a[k]) for k in ['qx','qy','qz','qw']]).as_matrix() for a in data];curl=[np.array([float(a['c'+k]) for k in 'xyz']) for a in data];spread=np.array([float(data[0]['s'+k]) for k in 'xyz']);old=np.array((lefts if side=='Left' else rights)[wi][di],float)
   common=(0 if di==0 else (7 if wi<2 else 32)) if side=='Left' else (7.5 if di==0 else 15)
   oldtotal=old[0]+common
   end=r0[2]@r0[1].T@(p0[2]-p0[1])*(.97 if di else 1.02)
   def model(x):
    ds=x-old;ps=[p0[0]];rs=[r0[0]@rot(curl[0],oldtotal)@rot(spread,ds[3])@rot(curl[0],-oldtotal-ds[0])]
    for j in [1,2]:ps.append(ps[-1]+rs[-1]@r0[j-1].T@(p0[j]-p0[j-1]));rs.append(rs[-1]@r0[j-1].T@r0[j]@rot(curl[j],-ds[j]))
    ps.append(ps[-1]+rs[-1]@r0[2].T@end);return np.array(ps)
   if side=='Right':c=np.array([0,-.19,-.03]);e=np.array([.055,.16,.075]);plane=1
   elif wi<2:c=np.array([0,-.032,.42 if wi==0 else .52]);e=np.array([.105,.055,.18]);plane=2
   else:c=np.array([0,-.154 if wi==2 else -.168,.49 if wi==2 else .415]);e=np.array([.0336 if wi==2 else .0385,.1085 if wi==2 else .126,.0441 if wi==2 else .0455]);plane=1
   radius=(.0115 if di==0 else [.0,.0095,.010,.0093,.008][di])*3.13
   def residual(x):
    ps=model(x);vs=np.diff(ps,axis=0);vs/=np.linalg.norm(vs,axis=1)[:,None];dots=np.sum(vs[:-1]*vs[1:],axis=1);ang=np.rad2deg(np.arccos(np.clip(dots,-1,1)))
    pts=np.concatenate([ps[j]+(ps[j+1]-ps[j])*np.linspace(.15,.90,6)[:,None] for j in range(3)])
    dist=sdf(pts,c,e);res=list(np.maximum(radius*.75-dist,0)*4)
    # Each phalanx has a surface-contact opportunity; no extreme joint loops.
    res.extend([max(0,np.min(dist[j*6:j*6+6])-radius)*2 for j in range(3)])
    res.extend(np.maximum(ang-[80,65],0)*.008)
    # Successive flexion joints must bend in the same direction, never zig-zag.
    bends=np.cross(vs[:-1],vs[1:]);res.append(max(0,-np.dot(bends[0],bends[1]))*.35)
    if di!=0:
     res.extend((ps[1:,plane]-ps[0,plane])*.8)
     # Keep all finger segments in one flexion plane.
     res.append(np.dot(np.cross(vs[0],vs[1]),vs[2])*.10)
    res.extend((x-old)*.0001)
    return res
   fits=[least_squares(residual,np.clip(seed,[-40,-40,-40,-70],[45,45,45,70]),bounds=([-40,-40,-40,-70],[45,45,45,70]),max_nfev=160) for seed in [old,[0,0,0,0],[-15,-20,-20,-20]]]
   f=min(fits,key=lambda f:np.linalg.norm(f.fun));x=np.round(f.x);ps=model(x);report[weapon][side+digit]={'values':x.tolist(),'joints':ps.tolist(),'residual':float(np.linalg.norm(residual(x)))}
   for j,k in enumerate(['Proximal','Intermediate','Distal','Spread']):lines.append(f'grip.{weapon}.{side.lower()}.{digit}.{k}={int(x[j])}')
(out/'grip.txt').write_text('\n'.join(lines)+'\n');(out/'fit.json').write_text(json.dumps(report,indent=2))
