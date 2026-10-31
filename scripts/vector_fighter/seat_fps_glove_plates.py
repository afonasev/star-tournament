"""Seat the existing wrist plate backing on the glove textile, preserving GLB topology/resources.
Run with Blender -b -noaudio --python scripts/vector_fighter/seat_fps_glove_plates.py.
The pinned Git asset is the editable baseline; only plate POSITION/NORMAL/skin
attributes change. No exporter, mesh collapse, texture conversion or world edit.
"""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
from audit_runtime import Glb, COMPONENTS, WIDTHS
BASE='035f6d5bf38e8614360a49cf2d777d92ff7e9734'
REL='unity/Assets/StarTournament/Art/trooper-arms-animated.glb'

def main():
    raw=subprocess.check_output(['git','show',BASE+':'+REL],cwd=ROOT)
    g=Glb(raw);data=g.data;binary=bytearray(g.binary)
    node=next(n for n in data['nodes'] if n.get('name')=='vector-armored-hands')
    primitives=data['meshes'][node['mesh']]['primitives']
    # Only the existing glove textile is a seating surface. Forearm shells,
    # hands, fingers and weapon geometry remain byte-exact.
    cv=[];ct=[];cw=[];cj=[]
    for cloth in primitives:
        if data['materials'][cloth['material']]['name'] not in ('orbital-restored-material','orbital-restored-vector-glove'):continue
        a=cloth['attributes'];base=len(cv);cv.extend(Vector(p) for p in g.array(a['POSITION']));cw.extend(g.array(a['WEIGHTS_0']));cj.extend(g.array(a['JOINTS_0']))
        ci=[i[0]+base for i in g.array(cloth['indices'])];ct.extend(ci[i:i+3] for i in range(0,len(ci),3))
    tree=BVHTree.FromPolygons(cv,ct,all_triangles=True)
    plates=[p for p in primitives if data['materials'][p['material']]['name']=='orbital-restored-vector-chassis']
    bone_names=[data['nodes'][j]['name'] for j in data['skins'][node['skin']]['joints']]
    def write(index,rows):
        a=data['accessors'][index];v=data['bufferViews'][a['bufferView']];fmt,size=COMPONENTS[a['componentType']];width=WIDTHS[a['type']]
        start=v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',width*size)
        for i,row in enumerate(rows):struct.pack_into('<'+fmt*width,binary,start+i*stride,*row)
        if 'min' in a:a['min']=[min(x[k] for x in rows) for k in range(width)]
        if 'max' in a:a['max']=[max(x[k] for x in rows) for k in range(width)]
    report=[]
    for p in plates:
        a=p['attributes'];positions=list(g.array(a['POSITION']));normals=list(g.array(a['NORMAL']));joints=list(g.array(a['JOINTS_0']));weights=list(g.array(a['WEIGHTS_0']));moves=[]
        ix=[i[0] for i in g.array(p['indices'])];adj=[set() for _ in positions]
        for i in range(0,len(ix),3):
            for j in ix[i:i+3]:adj[j].update(ix[i:i+3])
        left=set(range(len(positions)));chosen=[]
        while left:
            todo=[left.pop()];part=[]
            while todo:
                i=todo.pop();part.append(i);more=adj[i]&left;left-=more;todo.extend(more)
            if len(part)!=140:continue
            if any(bone_names[joints[i][max(range(4),key=lambda k:weights[i][k])]] not in ('LeftHand','RightHand') for i in part):continue
            center=sum((Vector(positions[i]) for i in part),Vector())/len(part)
            contact_normal=tree.find_nearest(center)[1]
            # Retain the original smooth ceramic and the entire upper frame.
            # Extend only this existing closed frame's underside to the glove.
            # Its connected side walls then form a real mounting base rather
            # than an air gap. No new overlay, renderer or topology is added.
            for i in part:
                if Vector(normals[i]).dot(contact_normal)>=-.25:continue
                chosen.append(i);point=Vector(positions[i])
                hit,cn,tri,_=tree.ray_cast(point+contact_normal*.1,-contact_normal,.2)
                assert hit is not None,"Wrist mounting base left the actual glove surface"
                target=hit+contact_normal*.0004
                positions[i]=tuple(target);moves.append((target-point).length)
                aa_index,bb_index,cc_index=ct[tri];aa,bb,cc=cv[aa_index],cv[bb_index],cv[cc_index];x=bb-aa;y=cc-aa;z=hit-aa;den=x.dot(x)*y.dot(y)-x.dot(y)**2
                u=(y.dot(y)*z.dot(x)-x.dot(y)*z.dot(y))/den;v=(x.dot(x)*z.dot(y)-x.dot(y)*z.dot(x))/den
                # Preserve the authored smooth corner normals and tangent-map frame.
                # Textile seams must not imprint faceted shading into ceramic armor.
                blend={}
                for index,factor in [(aa_index,1-u-v),(bb_index,u),(cc_index,v)]:
                    for joint,weight in zip(cj[index],cw[index]):blend[joint]=blend.get(joint,0)+max(0,factor)*weight
                best=sorted(blend.items(),key=lambda x:-x[1])[:4];total=sum(w for _,w in best);best+=[(0,0)]*(4-len(best))
                joints[i]=tuple(j for j,_ in best);weights[i]=tuple(w/total for _,w in best)
        for attr,rows in [('POSITION',positions),('NORMAL',normals),('JOINTS_0',joints),('WEIGHTS_0',weights)]:write(a[attr],rows)
        report.append({'material':data['materials'][p['material']]['name'],'edited_vertices':len(chosen),'max_move_m':max(moves),'median_move_m':sorted(moves)[len(moves)//2]})
    payload=json.dumps(data,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4)
    result=struct.pack('<4sII',b'glTF',2,28+len(payload)+len(binary))+struct.pack('<I4s',len(payload),b'JSON')+payload+struct.pack('<I4s',len(binary),b'BIN\0')+binary
    final=Glb(result)
    assert final.triangles()==g.triangles()
    assert final.images()==g.images() and final.binds()==g.binds()
    assert final.material_contract()==g.material_contract()
    edited={p['attributes'][key] for p in plates for key in ('POSITION','NORMAL','JOINTS_0','WEIGHTS_0')}
    for i in range(len(data['accessors'])):
        if i not in edited:assert g.array(i)==final.array(i),i
    path=ROOT/REL
    if "--check" in sys.argv:
        assert path.read_bytes()==result,"Run the authoring pass to update the asset"
        print("GLOVE_SEAT_EXACT: topology, nonplate arrays, images, materials, binds and reproducible bytes PASS");return
    path.write_bytes(result);sha=hashlib.sha256(result).hexdigest()
    manifest=path.parent/'asset-manifest.json';m=json.loads(manifest.read_text());entry=next(e for e in m['entries'] if e['key']=='player-hands');entry['sha256']=sha;entry['source']='Preserved Orbital FPS hands; seat_fps_glove_plates.py: existing wrist chassis underside seated on textile; original ceramic/upper frame preserved; topology/images/equipment unchanged';manifest.write_text(json.dumps(m,indent=2)+'\n')
    mounts=path.parent/'trooper-mounts.json';m=json.loads(mounts.read_text());m['arms']['sha256']=sha;mounts.write_text(json.dumps(m,indent=2)+'\n')
    out=ROOT/'.local/fps-fix';out.mkdir(parents=True,exist_ok=True);(out/'glove-edit.json').write_text(json.dumps({'baseline':BASE,'source_sha256':hashlib.sha256(raw).hexdigest(),'sha256':sha,'plates':report,'triangles_before':g.triangles(),'triangles_after':Glb(result).triangles(),'image_bytes_equal':g.images()==Glb(result).images(),'strategy':'extend the existing closed chassis underside to textile; ceramic and upper frame unchanged','mounting_clearance_m':.0004,'ceramic_vertices_byte_exact':True},indent=2)+'\n')
if __name__=='__main__':main()
