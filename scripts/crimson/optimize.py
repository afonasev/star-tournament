"""Lossless Crimson export pass. Retain all oriented triangles, normals, UVs and semantic nodes.
Prune unused exporter texture copies; weld exact vertices and improve index locality.
Run after build.py. Only Pulse launcher keys are written, never the projectile or other guns.
"""
import sys,json,hashlib,struct,importlib.util
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('weapon_optimization',ROOT/'tools/optimize_weapon_meshes.py');opt=importlib.util.module_from_spec(spec);spec.loader.exec_module(opt)
art=ROOT/'unity/Assets/StarTournament/Art';out=ROOT/'.local/crimson/optimization';out.mkdir(parents=True,exist_ok=True)
def pack(doc,binary):
 doc['buffers'][0]['byteLength']=len(binary);a=json.dumps(doc,separators=(',',':')).encode();a+=b' '*((-len(a))%4);binary+=b'\0'*((-len(binary))%4)
 return struct.pack('<4sII',b'glTF',2,28+len(a)+len(binary))+struct.pack('<II',len(a),0x4e4f534a)+a+struct.pack('<II',len(binary),0x004e4942)+binary
reports=[]
for lod in (0,1):
 path=art/f'pulse-launcher-lod{lod}.glb';raw=path.read_bytes();doc,binary=opt.read(raw)
 before={'bytes':len(raw),'materials':len(doc['materials']),'storedImages':len(doc.get('images',[])),'vertices':sum(doc['accessors'][p['attributes']['POSITION']]['count'] for m in doc['meshes'] for p in m['primitives']),'triangles':sum(doc['accessors'][p['indices']]['count']//3 for m in doc['meshes'] for p in m['primitives'])}
 # All current materials are opaque metallic/roughness with ordinary texture slots.
 refs=[]
 def walk(v):
  if isinstance(v,dict):
   for k,x in v.items():
    if k.endswith('Texture') and isinstance(x,dict) and 'index' in x:refs.append(x)
    else:walk(x)
  elif isinstance(v,list):
   for x in v:walk(x)
 walk(doc['materials']);used=sorted({r['index'] for r in refs});mapping={v:i for i,v in enumerate(used)}
 textures=[doc['textures'][i] for i in used]
 for r in refs:r['index']=mapping[r['index']]
 images=sorted({t['source'] for t in textures});imap={v:i for i,v in enumerate(images)}
 doc['images']=[doc['images'][i] for i in images]
 for t in textures:t['source']=imap[t['source']]
 doc['textures']=textures
 candidate,rows=opt.optimize(pack(doc,binary))
 afterdoc,_=opt.read(candidate)
 report={'file':path.name,'before':before,'after':{'bytes':len(candidate),'materials':len(afterdoc['materials']),'storedImages':len(afterdoc['images']),'vertices':sum(afterdoc['accessors'][p['attributes']['POSITION']]['count'] for m in afterdoc['meshes'] for p in m['primitives']),'triangles':sum(afterdoc['accessors'][p['indices']]['count']//3 for m in afterdoc['meshes'] for p in m['primitives'])},'sha256Before':hashlib.sha256(raw).hexdigest(),'sha256After':hashlib.sha256(candidate).hexdigest(),'primitives':rows}
 (out/(path.stem+'-before.glb')).write_bytes(raw);(out/path.name).write_bytes(candidate);reports.append(report)
 if '--promote' in sys.argv:path.write_bytes(candidate)
if '--promote' in sys.argv:
 p=art/'asset-manifest.json';d=json.loads(p.read_text())
 for e in d['entries']:
  if e['key'] in ('pulse-lod0','pulse-lod1'):e['sha256']=hashlib.sha256((art/('pulse-launcher-lod'+e['key'][-1]+'.glb')).read_bytes()).hexdigest()
 p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
(out/'metrics.json').write_text(json.dumps(reports,indent=2)+'\n')
print(json.dumps([{k:v for k,v in r.items() if k!='primitives'} for r in reports],indent=2))
