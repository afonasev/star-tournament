import struct,json,subprocess,hashlib
from pathlib import Path
def read(b):
 n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);return j,b[28+n:]
def accessor(j,b,i):
 a=j['accessors'][i];v=j['bufferViews'][a['bufferView']];start=v.get('byteOffset',0)+a.get('byteOffset',0);size={5126:4,5123:2,5125:4,5121:1}[a['componentType']]*{'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];stride=v.get('byteStride',size)
 return b''.join(b[start+k*stride:start+k*stride+size] for k in range(a['count']))
report={}
for kind in ['arms','body']:
 p=f'unity/Assets/StarTournament/Art/trooper-{kind}-animated.glb'
 old,ob=read(subprocess.check_output(['git','show','ba8bf6b7e2e92140d4f9d98cfd05b646295ebe94:'+p]));p=f'.local/vector-f11/candidate-{kind}.glb';new,nb=read(Path(p).read_bytes())
 def nodes(j):return [{k:v for k,v in n.items() if k!='mesh'} for n in j['nodes']]
 assert nodes(old)==nodes(new),'nodes differ'
 assert len(old['animations'])==len(new['animations'])==7
 for a,c in zip(old['animations'],new['animations']):
  assert a['name']==c['name'] and a['channels']==c['channels']
  for s,t in zip(a['samplers'],c['samplers']):
   for k in ['input','output']:assert accessor(old,ob,s[k])==accessor(new,nb,t[k]),(a['name'],k)
 report[kind]={'hierarchy':'exact','all_7_animation_arrays':'exact','sha256':hashlib.sha256(Path(p).read_bytes()).hexdigest()}
Path('.local/vector-f11/glb-verification.json').write_text(json.dumps(report,indent=2)+'\n');print(report)
