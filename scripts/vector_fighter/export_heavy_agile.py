"""Optimize/export the F18 world body. First-person bytes are deliberately preserved.

Candidate-only by default; --promote updates the body and its manifest identity.
"""
import sys, json, hashlib, shutil,copy,struct,subprocess
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))

def retain_current_weapon(candidate,current):
    """Copy exact shipping equipment buffers; old F17 contains an obsolete shotgun.

    Mesh-local weapon coordinates and its attachment node are unchanged. This
    avoids any Blender re-sampling of the separately accepted weapon asset.
    """
    from audit_runtime import Glb
    source=Glb(current.read_bytes());target=Glb(candidate.read_bytes())
    data=target.data;binary=bytearray(target.binary);cache={}
    def item(kind,index):
        key=(kind,index)
        if key in cache:return cache[key]
        value=copy.deepcopy(source.data[kind][index])
        if kind=='bufferViews':
            offset=value.get('byteOffset',0);length=value['byteLength']
            binary.extend(b'\0'*(-len(binary)%4));value['byteOffset']=len(binary);value['buffer']=0
            binary.extend(source.binary[offset:offset+length])
        elif kind=='accessors':value['bufferView']=item('bufferViews',value['bufferView'])
        elif kind=='images':value['bufferView']=item('bufferViews',value['bufferView'])
        elif kind=='textures':
            value['source']=item('images',value['source'])
            if 'sampler' in value:value['sampler']=item('samplers',value['sampler'])
        elif kind=='materials':
            def textures(v):
                if isinstance(v,dict):
                    if 'index' in v:v['index']=item('textures',v['index'])
                    for child in v.values():textures(child)
                elif isinstance(v,list):
                    for child in v:textures(child)
            textures(value)
        data.setdefault(kind,[]).append(value);cache[key]=len(data[kind])-1;return cache[key]
    old_node=next(n for n in source.data['nodes'] if n['name']=='weapon:joined')
    node=next(n for n in data['nodes'] if n['name']=='weapon:joined')
    mesh=copy.deepcopy(source.data['meshes'][old_node['mesh']])
    for primitive in mesh['primitives']:
        primitive['indices']=item('accessors',primitive['indices'])
        primitive['attributes']={k:item('accessors',v) for k,v in primitive['attributes'].items()}
        primitive['material']=item('materials',primitive['material'])
    data['meshes'][node['mesh']]=mesh
    # Remove unused material/image references left by the old embedded weapon.
    used=sorted({p['material'] for m in data['meshes'] for p in m['primitives']});mapping={v:i for i,v in enumerate(used)}
    data['materials']=[data['materials'][i] for i in used]
    for m in data['meshes']:
        for p in m['primitives']:p['material']=mapping[p['material']]
    # Prune orphaned resource tables and bytes, including the superseded weapon.
    def compact(kind,used):
        indices=sorted(used);mapping={v:i for i,v in enumerate(indices)}
        data[kind]=[data.get(kind,[])[i] for i in indices];return mapping
    texture_infos=[]
    def collect(v):
        if isinstance(v,dict):
            if 'index' in v:texture_infos.append(v)
            for child in v.values():collect(child)
        elif isinstance(v,list):
            for child in v:collect(child)
    collect(data['materials'])
    mapping=compact('textures',{v['index'] for v in texture_infos})
    for v in texture_infos:v['index']=mapping[v['index']]
    mapping=compact('images',{t['source'] for t in data['textures']})
    for t in data['textures']:t['source']=mapping[t['source']]
    mapping=compact('samplers',{t['sampler'] for t in data['textures'] if 'sampler' in t})
    for t in data['textures']:
        if 'sampler' in t:t['sampler']=mapping[t['sampler']]
    refs=[]
    for m in data['meshes']:
        for p in m['primitives']:
            refs.append((p,'indices'));refs.extend((p['attributes'],k) for k in p['attributes'])
    refs.extend((skin,'inverseBindMatrices') for skin in data.get('skins',[]))
    for animation in data.get('animations',[]):
        for sampler in animation['samplers']:refs.extend([(sampler,'input'),(sampler,'output')])
    mapping=compact('accessors',{o[k] for o,k in refs})
    for o,k in refs:o[k]=mapping[o[k]]
    refs=[(a,'bufferView') for a in data['accessors']]+[(i,'bufferView') for i in data['images']]
    mapping=compact('bufferViews',{o[k] for o,k in refs})
    for o,k in refs:o[k]=mapping[o[k]]
    packed=bytearray()
    for view in data['bufferViews']:
        offset=view.get('byteOffset',0);payload=binary[offset:offset+view['byteLength']]
        packed.extend(b'\0'*(-len(packed)%4));view['byteOffset']=len(packed);packed.extend(payload)
    binary=packed
    data['buffers'][0]['byteLength']=len(binary);binary.extend(b'\0'*(-len(binary)%4))
    payload=json.dumps(data,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4)
    candidate.write_bytes(struct.pack('<4sII',b'glTF',2,28+len(payload)+len(binary))+
        struct.pack('<I4s',len(payload),b'JSON')+payload+struct.pack('<I4s',len(binary),b'BIN\0')+binary)

def main():
    import optimize_runtime as opt
    import author_assault
    if "--reference" in sys.argv and "--promote" in sys.argv:
        raise ValueError("Unoptimized reference cannot be promoted")
    root=Path(__file__).resolve().parents[2]
    source=root/'scripts/vector_fighter/f18-heavy-agile.blend'
    output=root/'.local/vector-heavy-agile'/('reference' if '--reference' in sys.argv else 'candidate');output.mkdir(parents=True,exist_ok=True)
    opt.SOURCE=source
    # F18 has authored continuous panel borders and identity insets. They must
    # not be replaced by F17's horizontal strip masks after reduction.
    palette=author_assault.palette
    author_assault.palette=lambda:palette(skip_prefix=('VECTOR V2',))
    original=opt.optimize
    def reduce(obj,ratio):
        if '--reference' in sys.argv:return original(obj,1)
        # New low-resolution shells already use explicit edge lands/chamfers.
        # Collapse would erase these; retain their purpose-built topology.
        if 'continuous lower garment' in obj.name:return original(obj,.35)
        return original(obj,1 if 'VECTOR V2' in obj.name else ratio)
    opt.optimize=reduce
    rows=opt.export('world','balanced',output/'candidate-body.glb')
    baseline=output/'baseline-equipment.glb'
    baseline.write_bytes(subprocess.check_output(['git','show','9d2e15bf:unity/Assets/StarTournament/Art/trooper-body-animated.glb'],cwd=root))
    retain_current_weapon(output/'candidate-body.glb',baseline)
    report={'source':str(source.relative_to(root)), 'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
            'objects':rows, 'before_triangles':sum(r['before'] for r in rows),
            'after_triangles':sum(r['after'] for r in rows),
            'first_person':'unchanged exact shipping bytes','lod':'existing separate world/first-person resolutions retained; no new distance LOD',
            'shadows':'existing renderer settings retained; no extra renderers'}
    (output/'optimization.json').write_text(json.dumps(report,indent=2)+'\n')
    if '--promote' in sys.argv:
        art=root/'unity/Assets/StarTournament/Art';target=art/'trooper-body-animated.glb'
        shutil.copy2(output/'candidate-body.glb',target);digest=hashlib.sha256(target.read_bytes()).hexdigest()
        manifest=json.loads((art/'asset-manifest.json').read_text());mounts=json.loads((art/'trooper-mounts.json').read_text())
        entry=next(e for e in manifest['entries'] if e['key']=='player-body');entry['sha256']=digest
        entry['source']='VECTOR F18 ASSAULT V2; scripts/vector_fighter/author_heavy_agile.py + export_heavy_agile.py'
        mounts['body']['sha256']=digest
        (art/'asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
        (art/'trooper-mounts.json').write_text(json.dumps(mounts,indent=2)+'\n')
    print('F18_OPTIMIZATION',report['before_triangles'],report['after_triangles'])

if __name__=='__main__':main()
