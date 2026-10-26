"""Append-only equipment mesh replacement. All skin/clip/non-weapon bytes stay exact.
Run after author.py; uses --ref (default HEAD) as the compatible input revision.
"""
import argparse,copy,hashlib,json,struct,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'unity/Assets/StarTournament/Art';OUT=ROOT/'.local/vector-shotgun'
def read(raw):
 size=struct.unpack_from('<I',raw,12)[0];j=json.loads(raw[20:20+size]);size2,kind=struct.unpack_from('<II',raw,20+size);assert kind==0x004e4942;return j,raw[28+size:28+size+size2]
def pack(j,b):
 j['buffers']=[{'byteLength':len(b)}];t=json.dumps(j,separators=(',',':')).encode();t+=b' '*(-len(t)%4);b+=b'\0'*(-len(b)%4)
 return struct.pack('<III',0x46546c67,2,28+len(t)+len(b))+struct.pack('<II',len(t),0x4e4f534a)+t+struct.pack('<II',len(b),0x004e4942)+b

def main():
 args=argparse.ArgumentParser();args.add_argument('--ref',default='HEAD');a=args.parse_args()
 src,blob=read((OUT/'vector-petrol.glb').read_bytes())
 # Keep old original metal texture samples, including the established name and bytes.
 base,basebin=read(subprocess.check_output(['git','show',a.ref+':unity/Assets/StarTournament/Art/vector-shotgun-lod0.glb'],cwd=ROOT))
 metal=next(m for m in base['materials'] if m['name']=='vector-chassis');tex=base['textures'][metal['pbrMetallicRoughness']['baseColorTexture']['index']];im=base['images'][tex['source']];view=base['bufferViews'][im['bufferView']];payload=basebin[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
 blob+=b'\0'*(-len(blob)%4);src['bufferViews'].append({'buffer':0,'byteOffset':len(blob),'byteLength':len(payload)});blob+=payload
 src.setdefault('images',[]).append({'name':'restored-shotgun-metal','mimeType':im['mimeType'],'bufferView':len(src['bufferViews'])-1})
 src.setdefault('textures',[]).append({'source':len(src['images'])-1})
 factors={'vector-chassis':(.055,.061,.067),'vector-titanium':(.28,.32,.36),'vector-petrol-enamel':(.048,.125,.139),'vector-brushed-edge':(.32,.37,.4)}
 for m in src['materials']:
  if m['name'] in factors:
   p=m['pbrMetallicRoughness'];p['baseColorTexture']={'index':len(src['textures'])-1};p['baseColorFactor']=[*factors[m['name']],1]
 modelbytes=pack(src,blob)
 for name in ['vector-shotgun-lod0.glb','vector-shotgun-lod1.glb']:(ART/name).write_bytes(modelbytes)
 report={'input_ref':a.ref,'source_triangles':sum(src['accessors'][p['indices']]['count']//3 for p in src['meshes'][0]['primitives']),'materials':len(src['materials']),'detail_reduction':'none; user deferred','files':[]}
 for name in ['trooper-body-animated.glb','trooper-arms-animated.glb']:
  raw=subprocess.check_output(['git','show',a.ref+':unity/Assets/StarTournament/Art/'+name],cwd=ROOT);dst,db=read(raw);prior=copy.deepcopy(dst);oldbin=db
  db+=b'\0'*(-len(db)%4);bo=len(db);db+=blob
  keys=['bufferViews','accessors','images','samplers','textures','materials'];offset={k:len(dst.setdefault(k,[])) for k in keys}
  for v in src['bufferViews']:
   v=copy.deepcopy(v);v['byteOffset']=v.get('byteOffset',0)+bo;v['buffer']=0;dst['bufferViews'].append(v)
  for x in src['accessors']:
   x=copy.deepcopy(x)
   if 'bufferView' in x:x['bufferView']+=offset['bufferViews']
   assert 'sparse' not in x;dst['accessors'].append(x)
  for x in src.get('images',[]):
   x=copy.deepcopy(x);x['bufferView']+=offset['bufferViews'];dst['images'].append(x)
  dst['samplers']+=copy.deepcopy(src.get('samplers',[]))
  for x in src.get('textures',[]):
   x=copy.deepcopy(x)
   if 'source'in x:x['source']+=offset['images']
   if 'sampler'in x:x['sampler']+=offset['samplers']
   dst['textures'].append(x)
  def remap(obj):
   if isinstance(obj,dict):
    for k,v in obj.items():
     if k.endswith('Texture') and isinstance(v,dict) and 'index'in v:v['index']+=offset['textures']
     else:remap(v)
   elif isinstance(obj,list):
    for x in obj:remap(x)
  for m in src['materials']:
   m=copy.deepcopy(m);remap(m);dst['materials'].append(m)
  mesh=copy.deepcopy(src['meshes'][0]);mesh['name']='VectorPetrolDetailed'
  for p in mesh['primitives']:
   p['indices']+=offset['accessors'];p['material']+=offset['materials']
   p['attributes']={k:v+offset['accessors'] for k,v in p['attributes'].items()}
  idx=next(n['mesh'] for n in dst['nodes'] if n.get('name')=='weapon:joined');dst['meshes'][idx]=mesh
  for k in ['extensionsUsed','extensionsRequired']:dst[k]=sorted(set(dst.get(k,[])+src.get(k,[])))
  for k in ['nodes','skins','animations']:assert dst.get(k)==prior.get(k),k
  for i,m in enumerate(prior['meshes']):
   if i!=idx:assert dst['meshes'][i]==m
  assert db[:len(oldbin)]==oldbin
  out=pack(dst,db);(ART/name).write_bytes(out);report['files'].append({'file':name,'sha256':hashlib.sha256(out).hexdigest(),'original_payload_exact':True,'rig_clips_other_meshes_exact':True,'added_bytes':len(out)-len(raw)})
 manifest_path=ART/'asset-manifest.json';manifest=json.loads(manifest_path.read_text())
 for entry in manifest['entries']:
  if entry['key']in ['world-weapon','view-weapon','player-body','player-hands']:
   entry['sha256']=hashlib.sha256((ROOT/'unity'/entry['path']).read_bytes()).hexdigest();entry['source']='Vector/Petrol detailed original-reference model; scripts/vector_shotgun; preserved rig/gloves'
 manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
 path=ART/'trooper-mounts.json';m=json.loads(path.read_text())
 for key,file in [('body','trooper-body-animated.glb'),('arms','trooper-arms-animated.glb')]:m[key]['sha256']=hashlib.sha256((ART/file).read_bytes()).hexdigest()
 m['equipment']='Vector/Petrol detailed source, unchanged compatible rig/gloves; newly fitted weapon contact surfaces';path.write_text(json.dumps(m,indent=2)+'\n')
 (OUT/'asset-audit.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
