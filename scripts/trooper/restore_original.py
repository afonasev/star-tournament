"""Restore ART_LOLL skin surfaces while preserving current equipment and animation.

B01 is retained byte-for-byte outside Unity under assets/source/vector-b01.
Run with Python; --promote writes the shipping GLBs and manifest hashes.
"""
import argparse
import copy
import hashlib
import json
import struct
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'scripts/vector_fighter'))
from audit_runtime import Glb, COMPONENTS, WIDTHS
from export_heavy_agile import retain_current_weapon
from port_b01_arms import write

BASE = '78fc2d7851faac7b11a588b9f584ec88f178d84c'
OLD = '1d4444bffd79933b1613c214d719e74268d141e3'
ART = ROOT/'unity/Assets/StarTournament/Art'
SAVED = ROOT/'assets/source/vector-b01'


def git_asset(ref, name):
    return subprocess.check_output(['git', 'show', ref+':'+str((ART/name).relative_to(ROOT))], cwd=ROOT)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def skin_surfaces(g):
    clone = Glb(write_bytes(g))
    for node in clone.data['nodes']:
        if 'skin' not in node:
            node.pop('mesh', None)
    return clone.primitives()


def write_bytes(g):
    data = json.dumps(g.data, separators=(',', ':')).encode(); data += b' '*(-len(data)%4)
    binary = bytes(g.binary); binary += b'\0'*(-len(binary)%4)
    return struct.pack('<4sII', b'glTF', 2, 28+len(data)+len(binary))+struct.pack('<I4s',len(data),b'JSON')+data+struct.pack('<I4s',len(binary),b'BIN\0')+binary


def restore(kind, candidate):
    name=f'trooper-{kind}-animated.glb'
    current=Glb(git_asset(BASE,name)); old=Glb(git_asset(OLD,name))
    data=current.data; binary=bytearray(current.binary); cache={}
    byname={n.get('name'):i for i,n in enumerate(data['nodes'])}
    parents={c:i for i,n in enumerate(old.data['nodes']) for c in n.get('children',[])}
    for node in data['nodes']:
        if 'skin' in node:
            node.pop('skin');node.pop('mesh')

    def append_view(payload):
        binary.extend(b'\0'*(-len(binary)%4)); offset=len(binary);binary.extend(payload)
        data['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(payload)})
        return len(data['bufferViews'])-1

    def item(table,index):
        key=table,index
        if key in cache:return cache[key]
        value=copy.deepcopy(old.data[table][index])
        if table=='bufferViews':
            start=value.get('byteOffset',0);result=append_view(old.binary[start:start+value['byteLength']]);cache[key]=result;return result
        if table in ('accessors','images'):value['bufferView']=item('bufferViews',value['bufferView'])
        elif table=='textures':
            value['source']=item('images',value['source'])
            if 'sampler' in value:value['sampler']=item('samplers',value['sampler'])
        elif table=='materials':
            def remap(v):
                if isinstance(v,dict):
                    if 'index' in v:v['index']=item('textures',v['index'])
                    for child in v.values():remap(child)
                elif isinstance(v,list):
                    for child in v:remap(child)
            remap(value)
        data.setdefault(table,[]).append(value);cache[key]=len(data[table])-1;return cache[key]

    removed=0
    for old_index,node in enumerate(old.data['nodes']):
        if 'skin' not in node:continue
        skin=copy.deepcopy(old.data['skins'][node['skin']]);joint_names=[old.data['nodes'][j]['name'] for j in skin['joints']]
        skin['joints']=[byname[n] for n in joint_names]
        if 'skeleton' in skin:skin['skeleton']=byname[old.data['nodes'][skin['skeleton']]['name']]
        skin['inverseBindMatrices']=item('accessors',skin['inverseBindMatrices'])
        data['skins'].append(skin);skin_index=len(data['skins'])-1
        mesh=copy.deepcopy(old.data['meshes'][node['mesh']]);mesh['name']='original-fps-arms' if kind=='arms' else 'original-body-'+node['name']
        for p in mesh['primitives']:
            source_attrs=p['attributes'].copy();source_indices=p['indices']
            p['attributes']={k:item('accessors',v) for k,v in source_attrs.items()};p['indices']=item('accessors',source_indices);p['material']=item('materials',p['material'])
            if kind=='arms':
                joints=old.array(source_attrs['JOINTS_0']);weights=old.array(source_attrs['WEIGHTS_0'])
                upper={i for i,(js,ws) in enumerate(zip(joints,weights)) if joint_names[js[max(range(4),key=lambda k:ws[k])]].endswith(('UpperArm','Shoulder'))}
                indices=[v[0] for v in old.array(source_indices)];kept=[]
                for i in range(0,len(indices),3):
                    tri=indices[i:i+3]
                    if any(v in upper for v in tri):removed+=1
                    else:kept.extend(tri)
                used=sorted(set(kept));mapping={v:i for i,v in enumerate(used)}
                for attr,index in source_attrs.items():
                    accessor=copy.deepcopy(old.data['accessors'][index]);all_values=old.array(index);values=[all_values[i] for i in used]
                    fmt=COMPONENTS[accessor['componentType']][0];width=WIDTHS[accessor['type']]
                    payload=b''.join(struct.pack('<'+fmt*width,*v) for v in values)
                    accessor.update(bufferView=append_view(payload),byteOffset=0,count=len(values));accessor.pop('sparse',None)
                    if 'min' in accessor:accessor['min']=[min(v[i] for v in values) for i in range(width)]
                    if 'max' in accessor:accessor['max']=[max(v[i] for v in values) for i in range(width)]
                    data['accessors'].append(accessor);p['attributes'][attr]=len(data['accessors'])-1
                values=[mapping[i] for i in kept];view=append_view(struct.pack('<'+'I'*len(values),*values))
                data['accessors'].append({'bufferView':view,'componentType':5125,'count':len(values),'type':'SCALAR','min':[min(values)],'max':[max(values)]});p['indices']=len(data['accessors'])-1
        data['meshes'].append(mesh)
        restored=copy.deepcopy(node);restored['name']='vector-armored-hands' if kind=='arms' else 'original-trooper-'+node['name'];restored['mesh']=len(data['meshes'])-1;restored['skin']=skin_index
        # Keep the established tooling name only on the replacement FPS node.
        if kind=='arms':
            for n in data['nodes']:
                if n.get('name')=='vector-armored-hands':n['name']='retired-b01-fps-node'
        data['nodes'].append(restored);new_index=len(data['nodes'])-1
        parent=old.data['nodes'][parents[old_index]]['name'];data['nodes'][byname[parent]].setdefault('children',[]).append(new_index)

    for table,key in [('meshes','mesh'),('skins','skin')]:
        used=sorted({n[key] for n in data['nodes'] if key in n});mapping={v:i for i,v in enumerate(used)};data[table]=[data[table][i] for i in used]
        for n in data['nodes']:
            if key in n:n[key]=mapping[n[key]]
    write(candidate,data,binary)
    # Existing copier also compacts all orphan maps, accessors and buffer bytes.
    retain_current_weapon(candidate,SAVED/name)
    result=Glb(candidate.read_bytes());baseline=Glb(git_asset(BASE,name))
    assert result.primitives(weapon=True)==baseline.primitives(weapon=True)
    def exact_materials(reference,names):
        def contract(g,material):
            def resolve(value):
                if isinstance(value,dict):
                    out={k:resolve(v) for k,v in value.items()}
                    if 'index' in value:
                        texture=g.data['textures'][value['index']];image=g.data['images'][texture['source']]
                        view=g.data['bufferViews'][image['bufferView']];start=view.get('byteOffset',0)
                        out['index']=digest(g.binary[start:start+view['byteLength']])
                        out['sampler']=g.data.get('samplers',[])[texture['sampler']] if 'sampler'in texture else None
                    return out
                if isinstance(value,list):return [resolve(v) for v in value]
                return value
            return resolve(next(m for m in g.data['materials'] if m['name']==material))
        # Embedded image names are not unique across source files; compare the
        # actual texture-index references and bytes, never a name-keyed map.
        for material in names:assert contract(result,material)==contract(reference,material),material
    exact_materials(baseline,result.primitives(weapon=True))
    for before,after in zip(baseline.data['nodes'],result.data['nodes']):
        for k in ('matrix','translation','rotation','scale'):assert before.get(k)==after.get(k),(before.get('name'),k)
    def clips(g):
        return [(a['name'],a['channels'],[(g.array(s['input']),g.array(s['output']),s.get('interpolation')) for s in a['samplers']]) for a in g.data['animations']]
    assert clips(result)==clips(baseline),'Animation payload must remain exact'
    surfaces=skin_surfaces(result);original=skin_surfaces(old)
    if kind=='body':assert surfaces==original,'All original body surfaces must remain exact'
    else:
        for material,triangles in surfaces.items():
            allowed=set(original[material]);assert all(t in allowed for t in triangles),'FPS must be an exact subset of original arms'
    exact_materials(old,surfaces)
    used_materials={p['material'] for n in result.data['nodes'] if 'skin'in n for p in result.data['meshes'][n['mesh']]['primitives']}
    return {'before_triangles':baseline.triangles(),'after_triangles':result.triangles(),'original_triangles':old.triangles(),'upper_arm_triangles_removed':removed,'skin_material_slots':len(used_materials),'bytes':candidate.stat().st_size,'sha256':digest(candidate.read_bytes()),'exact_current_weapon_animation_transforms':True,'exact_original_body_or_fps_subset':True}


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--promote',action='store_true');args=parser.parse_args()
    SAVED.mkdir(parents=True,exist_ok=True);out=ROOT/'.local/restore-original-trooper';out.mkdir(parents=True,exist_ok=True)
    retained={}
    for kind in ['body','arms']:
        name=f'trooper-{kind}-animated.glb';raw=git_asset(BASE,name);p=SAVED/name
        if p.exists():assert p.read_bytes()==raw
        else:p.write_bytes(raw)
        retained[name]=digest(raw)
    (SAVED/'manifest.json').write_text(json.dumps({'variant':'B01, retained inactive','revision':BASE,'files':retained,'editable_sources':['scripts/vector_fighter/vector-body-b01.blend','scripts/vector_fighter/f23-military-color.blend'],'reproduction':['scripts/vector_fighter/export_body.py','scripts/vector_fighter/port_b01_arms.py'],'runtime':'Not imported by Unity; restoring requires explicit promotion and prefab regeneration'},indent=2)+'\n')
    report={'baseline':BASE,'original_revision':subprocess.check_output(['git','rev-parse',OLD],cwd=ROOT,text=True).strip(),'retained_b01':retained,'models':{}}
    for kind in ['body','arms']:
        name=f'trooper-{kind}-animated.glb';candidate=out/name;report['models'][kind]=restore(kind,candidate)
        if args.promote:
            target=ART/name;assert target.read_bytes() in (git_asset(BASE,name),candidate.read_bytes()),'Do not overwrite other work';target.write_bytes(candidate.read_bytes())
    report['optimization']={'geometry':'Preserve every old world triangle; FPS excludes obstructing upper-arm triangles and compacts unused vertices. No lossy world reduction.','resources':'Prune orphan mesh/skin/material/texture/accessor tables and binary data; share body material subassets with FPS in TrooperPreparation','shadows':'Existing world shadow and FPS shadow-off policy retained','LOD':'No new distance LOD for FPS; world retains existing single-mesh policy at original 30757 triangles','performance':'NotRun; resource counts are not FPS/VRAM measurements'}
    if args.promote:
        mounts=json.loads((ART/'trooper-mounts.json').read_text());manifest=json.loads((ART/'asset-manifest.json').read_text())
        for kind,key in [('body','player-body'),('arms','player-hands')]:
            h=report['models'][kind]['sha256'];mounts[kind]['sha256']=h;entry=next(e for e in manifest['entries'] if e['key']==key);entry['sha256']=h;entry['source']='ART_LOLL original costume restored with current equipment/rig; scripts/trooper/restore_original.py'
        (ART/'trooper-mounts.json').write_text(json.dumps(mounts,indent=2)+'\n');(ART/'asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (out/'asset-audit.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report['models'],indent=2))

if __name__=='__main__':main()
