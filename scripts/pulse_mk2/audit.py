"""Source/GLB audit only. Does not certify runtime grip or human art acceptance."""
from pathlib import Path
import json,struct,hashlib,subprocess
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
def document(data):return json.loads(data[20:20+struct.unpack_from('<I',data,12)[0]])
def metrics(d):
 return {'triangles':sum(d['accessors'][p['indices']]['count']//3 for m in d['meshes'] for p in m['primitives']), 'primitives':sum(len(m['primitives']) for m in d['meshes']),'materials':len(d['materials']),'textures':len(d.get('textures',[]))}
manifest=json.loads((ART/'asset-manifest.json').read_text());report=[]
for key,name in [('pulse-lod0','pulse-launcher-lod0.glb'),('pulse-lod1','pulse-launcher-lod1.glb'),('pulse-projectile','pulse-projectile.glb')]:
 path=ART/name;data=path.read_bytes();d=document(data);entry=next(e for e in manifest['entries'] if e['key']==key)
 assert hashlib.sha256(data).hexdigest()==entry['sha256'],name
 old=document(subprocess.check_output(['git','show','efc73a7a:unity/Assets/StarTournament/Art/'+name],cwd=ROOT))
 semantic=['pulse-projectile-nozzle'] if key=='pulse-projectile' else ['pulse-grip','pulse-muzzle','pulse-support-grip']
 for marker in semantic:
  a=next(n for n in old['nodes'] if n.get('name')==marker);b=next(n for n in d['nodes'] if n.get('name')==marker)
  for field in ['translation','rotation','scale','matrix']:assert a.get(field)==b.get(field),(name,marker,field,a.get(field),b.get(field))
 assert len(d.get('images',[]))>=4,'Missing packed material textures'
 for material in d['materials']:
  assert 'pbrMetallicRoughness' in material
 for mesh in d['meshes']:
  for primitive in mesh['primitives']:
   assert 'NORMAL' in primitive['attributes']
   assert 'TEXCOORD_0' in primitive['attributes']
 report.append({'key':key,'file':name,'sha256':entry['sha256'],'bytes':len(data),'baseline':metrics(old),'candidate':metrics(d),'attachment_transforms':'unchanged','detail_reduction':'explicitly deferred; both LOD keys retain detailed source'})
out=ROOT/'.local/pulse-mk2/asset-audit.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
