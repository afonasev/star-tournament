import numpy as np
from scipy.spatial.transform import Rotation
from pathlib import Path
exec(Path('docs/evidence/vector-fighter/f15/tools/compare_glb.py').read_text().split('report={}')[0])
j,b=read(Path('.local/vector-f15/candidate-arms.glb').read_bytes());node=next(n for n in j['nodes'] if n.get('name')=='vector-armored-hands');skin=j['skins'][node['skin']]
def arr(i):
 a=j['accessors'][i];return np.frombuffer(accessor(j,b,i),{5126:np.float32,5123:np.uint16,5125:np.uint32,5121:np.uint8}[a['componentType']]).reshape(a['count'],-1)
F=np.diag([-1,1,1,1]);bind=arr(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1);bind=F@bind@F
root=Path('.local/vector-f14/final-geometry');out=Path('.local/vector-f15/preview-final');out.mkdir(exist_ok=True)
for weapon in ['Rifle','Shotgun','RocketLauncher','Cutter']:
 bones={}
 for line in (root/(weapon+'-vector-armored-hands-skin.txt')).read_text().splitlines():
  a=line.split()
  if a[0]=='b':
   m=np.eye(4);m[:3,:3]=Rotation.from_quat(list(map(float,a[5:9]))).as_matrix()*3.12;m[:3,3]=list(map(float,a[2:5]));bones[a[1]]=m
 matrices=np.array([bones[j['nodes'][n]['name']]@bm for n,bm in zip(skin['joints'],bind)])
 lines=['o vector-armored-hands'];offset=0
 for p in j['meshes'][node['mesh']]['primitives']:
  at=p['attributes'];v=arr(at['POSITION']);v=np.c_[-v[:,0],v[:,1:],np.ones(len(v))];ws=arr(at['WEIGHTS_0']);ids=arr(at['JOINTS_0']);posed=np.zeros((len(v),4))
  for k in range(4):posed+=np.einsum('nij,nj->ni',matrices[ids[:,k]],v)*ws[:,k,None]
  lines.extend('v '+' '.join(map(str,x[:3])) for x in posed);faces=arr(p['indices']).astype(np.int64).reshape(-1,3)+offset+1;lines.extend('f '+' '.join(map(str,f)) for f in faces);offset+=len(v)
 src=(root/(weapon+'-actual-mesh.obj')).read_text().splitlines();verts=[];parts={};name=''
 for line in src:
  a=line.split()
  if a[0]=='o':name=a[1];parts[name]=[]
  elif a[0]=='v':verts.append(line)
  elif a[0]=='f':parts[name].append([int(i)-1 for i in a[1:]])
 for name,faces in parts.items():
  if 'hands' in name:continue
  used=sorted(set(i for f in faces for i in f));mp={i:offset+k+1 for k,i in enumerate(used)};lines.append('o '+name);lines.extend(verts[i] for i in used);lines.extend('f '+' '.join(str(mp[i]) for i in f) for f in faces);offset+=len(used)
 (out/(weapon+'-actual-mesh.obj')).write_text('\n'.join(lines)+'\n')
