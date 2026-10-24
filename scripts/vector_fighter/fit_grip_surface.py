"""Offline fitting aid against Unity-baked glove vertices and a measured grip box.
This is NOT a Unity execution test or visual acceptance. Always inspect native captures.
Input: CaptureGrip skin/axes exports in weapon coordinates; immutable override file.
"""
from pathlib import Path
import csv,json,sys
import numpy as np
from scipy.spatial.transform import Rotation
from scipy.optimize import least_squares
root=Path(sys.argv[1]);output=Path(sys.argv[2]);digits=['Thumb','Index','Middle','Ring','Little'];segments=['Proximal','Intermediate','Distal']
def rot(axis,angle):return Rotation.from_rotvec(np.array(axis)*np.deg2rad(angle)).as_matrix()
def sdf(points,center,extent):
    d=np.abs(points-center)-extent
    return np.linalg.norm(np.maximum(d,0),axis=1)+np.minimum(np.max(d,axis=1),0)
results={};lines=[]
for weapon,z in [('RocketLauncher',.49),('Cutter',.406)]:
    # The coordinates are generator dimensions times the measured 1.4 mount scale.
    center=np.array([0,-.154 if weapon=='RocketLauncher' else -.168,z]);extent=np.array([.0336,.1085,.0441]) if weapon=='RocketLauncher' else np.array([.0385,.126,.0455])
    axes={d['name']:d for d in csv.DictReader((root/f'{weapon}-axes.csv').open())}
    bones={};verts=[];weights=[]
    for file in root.glob(f'{weapon}-*-skin.txt'):
        for line in file.read_text().splitlines():
            a=line.split()
            if a[0]=='b':bones[a[1]]=(np.array(list(map(float,a[2:5]))),Rotation.from_quat(list(map(float,a[5:9]))).as_matrix())
            elif a[0]=='v':verts.append(list(map(float,a[1:4])));weights.append({k:float(v) for k,v in (x.split(':') for x in a[4:])})
    shift=np.array([0,.04,0]);verts=np.array(verts)+shift;bones={k:(p+shift,r) for k,(p,r) in bones.items()};results[weapon]={}
    lines.append(f'grip.{weapon}.supportY=-0.12')
    for digit in digits:
        names=['Left'+digit+s for s in segments];data=[bones[n] for n in names];thumb=digit=='Thumb'
        ids=[i for i,w in enumerate(weights) if sum(w.get(n,0) for n in names)>.45]
        # Deterministically reduce coincident normal/UV-split copies, not geometric coverage.
        ids=np.array(ids);_,unique=np.unique(np.round(verts[ids],5),axis=0,return_index=True);ids=ids[unique]
        original=verts[ids];ws=np.array([[weights[i].get(n,0) for n in names] for i in ids]);fixed=1-ws.sum(axis=1)
        curl=[np.array([float(axes[n]['c'+a]) for a in 'xyz']) for n in names];spread=np.array([float(axes[names[0]]['s'+a]) for a in 'xyz'])
        def model(x):
            ps=[];rs=[]
            for j,(p,r) in enumerate(data):
                if j==0:
                    ps.append(p);common=0 if thumb else 2
                    rs.append(rot([0,0,1],x[4])@r@rot(curl[0],common)@rot(spread,x[3])@rot(curl[0],-common-x[0]))
                else:
                    prev=data[j-1];ps.append(ps[-1]+rs[-1]@prev[1].T@(p-prev[0]));rs.append(rs[-1]@prev[1].T@r@rot(curl[j],-x[j]))
            posed=original*fixed[:,None]
            for j,(p,r) in enumerate(data):posed+=ws[:,j,None]*((original-p)@(rs[j]@r.T).T+ps[j])
            return np.array(ps),posed
        def residual(x):
            ps,p=model(x);dist=sdf(p,center,extent)
            out=list(np.maximum(-dist-.001,0)*5)
            out.append(min(float(dist.min()),0)*15)
            for j in range(3):
                select=ws[:,j]>.45
                if np.any(select):out.append(float(np.quantile(dist[select],.01))*2)
            # Keep knuckle rows aligned along the vertical handle and discourage extreme folds.
            if not thumb:
                y=np.clip(data[0][0][1],center[1]-extent[1]+.025,center[1]+extent[1]-.03)
                target=np.array([[-.015,y,z+.095],[.065,y,z+.06]])
                out.extend(((ps[1:]-target)*3).ravel())
            else:
                out.extend((ps[1:,1]-(center[1]+.025))*1.5)
            out.extend(x[:4]*.000035)
            return out
        bounds=([-40,-40,-40,-70 if thumb else -25,-90 if thumb else -.00001],[45,45,45,70 if thumb else 30,90 if thumb else .00001])
        best=None
        for seed in [[0,0,0,0,0],[20,20,10,0,0],[-20,15,15,-20,0],[20,20,20,25,40 if thumb else 0]]:
            fit=least_squares(residual,seed,bounds=bounds,max_nfev=130,diff_step=.002)
            cost=np.linalg.norm(residual(fit.x))
            if best is None or cost<best[0]:best=(cost,fit.x)
        x=np.round(best[1]);ps,p=model(x);dist=sdf(p,center,extent)
        result={'angles':x.tolist(),'surfaceMinRigUnits':float(dist.min()),'surfaceContactP04':float(np.quantile(dist,.04)),'vertices':len(ids),'joints':ps.tolist()};results[weapon][digit]=result
        print(weapon,digit,result,flush=True)
        for k,v in zip(segments+['Spread'],x):lines.append(f'grip.{weapon}.left.{digit}.{k}={int(v)}')
        if thumb:lines.append(f'grip.{weapon}.thumbOpposition={int(x[4])}')
output.write_text('\n'.join(lines)+'\n');output.with_suffix('.json').write_text(json.dumps(results,indent=2)+'\n')
