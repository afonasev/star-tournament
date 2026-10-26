"""Rebind existing raised glove stitches to their underlying textile triangles.
Stitch topology/detail, materials, UVs, bind skeleton, clips and weapon are retained.
Only thread position/skin and rigid digit-pad placement change; anatomical
textile, other armor, skeleton, clips and the weapon are byte exact.
Requires numpy/scipy; run with --ref <input commit>. Source GLBs are retained in Git.
"""
import argparse,copy,hashlib,json,struct,subprocess,sys
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/vector_fighter'))
from audit_runtime import Glb

def nearest(points,triangles):
    # Triangle centroids select local candidates; include edge projections so
    # stitching outside the cloth UV boundary still uses the correct surface.
    tree=cKDTree(triangles.mean(axis=1));k=min(48,len(triangles))
    for point in points:
        ids=tree.query(point,k=k)[1];tri=triangles[ids];a=tri[:,0];ab=tri[:,1]-a;ac=tri[:,2]-a;ap=point-a
        d00=np.einsum('ij,ij->i',ab,ab);d01=np.einsum('ij,ij->i',ab,ac);d11=np.einsum('ij,ij->i',ac,ac)
        d20=np.einsum('ij,ij->i',ap,ab);d21=np.einsum('ij,ij->i',ap,ac);den=d00*d11-d01*d01
        u=np.divide(d11*d20-d01*d21,den,out=np.zeros_like(den),where=abs(den)>1e-20);v=np.divide(d00*d21-d01*d20,den,out=np.zeros_like(den),where=abs(den)>1e-20)
        bary=np.stack((1-u-v,u,v),axis=1);valid=np.all(bary>=0,axis=1)&(abs(den)>1e-20)
        candidates=[bary];dist=[np.where(valid,np.sum((a+u[:,None]*ab+v[:,None]*ac-point)**2,axis=1),np.inf)]
        for j,h in [(0,1),(1,2),(2,0)]:
            edge=tri[:,h]-tri[:,j];length=np.sum(edge*edge,axis=1);t=np.clip(np.divide(np.sum((point-tri[:,j])*edge,axis=1),length,out=np.zeros_like(length),where=length>0),0,1)
            b=np.zeros((k,3));b[:,j]=1-t;b[:,h]=t;candidates.append(b);dist.append(np.sum((tri[:,j]+t[:,None]*edge-point)**2,axis=1))
        choice,row=np.unravel_index(np.argmin(np.array(dist)),(4,k));yield int(ids[row]),candidates[choice][row],float(np.sqrt(dist[choice][row]))

def append_array(data,blob,array,kind,component):
    blob.extend(b'\0'*(-len(blob)%4));view=len(data['bufferViews']);data['bufferViews'].append({'buffer':0,'byteOffset':len(blob),'byteLength':array.nbytes});blob.extend(array.tobytes())
    index=len(data['accessors']);accessor={'bufferView':view,'componentType':component,'count':len(array),'type':kind}
    if kind=='VEC3':accessor.update(min=array.min(axis=0).tolist(),max=array.max(axis=0).tolist())
    data['accessors'].append(accessor);return index

def patch(raw):
    g=Glb(raw);data=copy.deepcopy(g.data);size=data['buffers'][0]['byteLength'];blob=bytearray(g.binary[:size]);report=[]
    for node in data['nodes']:
        if 'skin' not in node or 'mesh' not in node:continue
        primitives=data['meshes'][node['mesh']]['primitives'];cloth=next((p for p in primitives if data['materials'][p['material']]['name']=='orbital-restored-material'),None)
        if cloth is None:continue
        ca=cloth['attributes'];cp=np.array(g.array(ca['POSITION']));ct=np.array(g.array(cloth['indices']),dtype=int).reshape(-1,3);cj=np.array(g.array(ca['JOINTS_0']),dtype=int);cw=np.array(g.array(ca['WEIGHTS_0']));cn=np.array(g.array(ca['NORMAL']))
        for p in primitives:
            material=data['materials'][p['material']]['name']
            if material not in ['orbital-restored-vector-glove','orbital-restored-vector-chassis','vector-ceramic-orbital']:continue
            pa=p['attributes'];positions=np.array(g.array(pa['POSITION']));oldj=np.array(g.array(pa['JOINTS_0']));oldw=np.array(g.array(pa['WEIGHTS_0']));joints=[];weights=[];distances=[];seated=[];surface_normals=[]
            names=[data['nodes'][j]['name'] for j in data['skins'][node['skin']]['joints']]
            selected=np.ones(len(positions),dtype=bool) if material=='orbital-restored-vector-glove' else np.array([any(d in names[int(row[int(np.argmax(w))])] for d in ['Thumb','Index','Middle','Ring','Little']) for row,w in zip(oldj,oldw)])
            for vertex,(point,(face,bary,distance)) in enumerate(zip(positions,nearest(positions,cp[ct]))):
                if not selected[vertex]:
                    seated.append(point);joints.append(oldj[vertex]);weights.append(oldw[vertex]);surface_normals.append(np.zeros(3));continue
                surface=bary@cp[ct[face]];delta=point-surface
                # Existing strips are sewn to the textile rather than hovering
                # centimetres above it. Retain each triangle and its UV detail.
                if material=='orbital-restored-vector-glove':seated.append(surface+delta*min(1,.00035/max(distance,1e-12)))
                else:
                    normal=bary@cn[ct[face]];normal/=max(np.linalg.norm(normal),1e-12)
                    surface_normals.append(normal);seated.append(point)
                    joints.append(oldj[vertex]);weights.append(oldw[vertex]);distances.append(distance);continue
                ws={}
                for vertex,factor in zip(ct[face],bary):
                    for j,w in zip(cj[vertex],cw[vertex]):ws[int(j)]=ws.get(int(j),0)+float(w*factor)
                top=sorted(ws.items(),key=lambda x:(-x[1],x[0]))[:4];total=sum(w for j,w in top);top += [(0,0)]*(4-len(top));joints.append([j for j,w in top]);weights.append([w/total for j,w in top]);distances.append(distance)
            if material!='orbital-restored-vector-glove':
                seated=np.array(seated);surface_normals=np.array(surface_normals);dominant=oldj[np.arange(len(oldj)),oldw.argmax(axis=1)]
                for joint in sorted(set(dominant[selected])):
                    part=selected&(dominant==joint);normal=surface_normals[part].mean(axis=0);normal/=max(np.linalg.norm(normal),1e-12)
                    # Each closed digit-pad layer remains rigid. Move its whole
                    # authored outline above the folding thenar cloth, never warp it.
                    seated[part]+=normal*(.004 if 'Thumb' in names[int(joint)] else .002)
            ja=np.array(joints,dtype='<u2');wa=np.array(weights,dtype='<f4');assert np.allclose(wa.sum(axis=1),1,atol=1e-6)
            pa['POSITION']=append_array(data,blob,np.array(seated,dtype='<f4'),'VEC3',5126)
            pa['JOINTS_0']=append_array(data,blob,ja,'VEC4',5123);pa['WEIGHTS_0']=append_array(data,blob,wa,'VEC4',5126)
            report.append({'mesh':node['name'],'material':material,'selected_vertices':int(sum(selected)),'vertices':len(positions),'changed':int(np.sum(np.any(ja!=oldj,axis=1)|np.any(abs(wa-oldw)>1e-6,axis=1))),'position_vertices_changed':int(sum(np.linalg.norm(np.array(seated)-positions,axis=1)>1e-8)),'max_position_delta':float(max(np.linalg.norm(np.array(seated)-positions,axis=1))),'max_original_surface_distance':max(distances),'max_seated_surface_distance':.00035 if material=='orbital-restored-vector-glove' else None,'triangles_preserved':len(g.array(p['indices']))//3})
    for key in ['nodes','skins','animations','materials','images','textures']:assert data.get(key)==g.data.get(key),key
    assert bytes(blob[:size])==g.binary[:size];data['buffers'][0]['byteLength']=len(blob);js=json.dumps(data,separators=(',',':')).encode();js+=b' '*(-len(js)%4);blob.extend(b'\0'*(-len(blob)%4))
    return struct.pack('<III',0x46546c67,2,28+len(js)+len(blob))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(blob),0x004e4942)+blob,report

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--ref',required=True);a=parser.parse_args();art=ROOT/'unity/Assets/StarTournament/Art';reports={}
    for name in ['trooper-arms-animated.glb','trooper-body-animated.glb']:
        raw=subprocess.check_output(['git','show',a.ref+':unity/Assets/StarTournament/Art/'+name],cwd=ROOT);result,report=patch(raw);(art/name).write_bytes(result);reports[name]={'input_sha256':hashlib.sha256(raw).hexdigest(),'output_sha256':hashlib.sha256(result).hexdigest(),'patches':report}
    path=art/'asset-manifest.json';m=json.loads(path.read_text())
    for e in m['entries']:
        name=Path(e['path']).name
        if name in reports:e['sha256']=reports[name]['output_sha256']
    path.write_text(json.dumps(m,indent=2)+'\n');path=art/'trooper-mounts.json';m=json.loads(path.read_text())
    for key,name in [('arms','trooper-arms-animated.glb'),('body','trooper-body-animated.glb')]:m[key]['sha256']=reports[name]['output_sha256']
    path.write_text(json.dumps(m,indent=2)+'\n');dest=ROOT/'.local/vector-grip/stitch-weight-audit.json';dest.write_text(json.dumps({'input_ref':a.ref,'files':reports,'preserved':'original binary payload, topology, UVs, normals, material/texture/weapon/skeleton/animation contracts; only raised-thread position/skin and thumb-plate skin accessors replaced; cloth anatomy and non-thumb armor geometry unchanged; digit-pad assemblies raised4mm as rigid original layers; skin unchanged'},indent=2)+'\n');print(dest)
if __name__=='__main__':main()
