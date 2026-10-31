"""Remove camera-facing upper sleeves from the view asset, retaining full world arms.
Only index lists change: bones, clips, hand/forearm vertices, weapon and textures
are byte-exact. Rebuild from the recorded Git input, never trim an already trimmed file.
"""
import argparse,copy,hashlib,json,struct,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/vector_fighter'))
from audit_runtime import Glb

def trim(raw):
    g=Glb(raw);data=copy.deepcopy(g.data);size=data['buffers'][0]['byteLength'];blob=bytearray(g.binary[:size]);report=[]
    for node in data['nodes']:
        if node.get('name')!='vector-armored-hands':continue
        names=[data['nodes'][j]['name'] for j in data['skins'][node['skin']]['joints']]
        for p in data['meshes'][node['mesh']]['primitives']:
            attrs=p['attributes'];joints=g.array(attrs['JOINTS_0']);weights=g.array(attrs['WEIGHTS_0'])
            # Upper sleeves behind the elbow belong to the world silhouette.
            # The camera-local mesh retains the entire lower sleeve, glove and wrist.
            upper=[names[js[max(range(4),key=lambda k:ws[k])]].endswith(('UpperArm','Shoulder')) for js,ws in zip(joints,weights)]
            ids=[v[0] for v in g.array(p['indices'])];kept=[]
            for i in range(0,len(ids),3):
                tri=ids[i:i+3]
                if not any(upper[v] for v in tri):kept.extend(tri)
            if len(kept)==len(ids):continue
            blob.extend(b'\0'*(-len(blob)%4));view=len(data['bufferViews']);encoded=struct.pack('<'+'I'*len(kept),*kept)
            data['bufferViews'].append({'buffer':0,'byteOffset':len(blob),'byteLength':len(encoded),'target':34963});blob.extend(encoded)
            p['indices']=len(data['accessors']);data['accessors'].append({'bufferView':view,'componentType':5125,'count':len(kept),'type':'SCALAR','min':[min(kept)],'max':[max(kept)]})
            report.append({'material':data['materials'][p['material']]['name'],'removed_triangles':(len(ids)-len(kept))//3,'retained_triangles':len(kept)//3})
    assert report
    for key in ['nodes','skins','animations','materials','images','textures']:assert data.get(key)==g.data.get(key),key
    assert bytes(blob[:size])==g.binary[:size]
    data['buffers'][0]['byteLength']=len(blob);js=json.dumps(data,separators=(',',':')).encode();js+=b' '*(-len(js)%4);blob.extend(b'\0'*(-len(blob)%4))
    result=struct.pack('<III',0x46546c67,2,28+len(js)+len(blob))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(blob),0x004e4942)+blob
    return result,report

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--ref',required=True);parser.add_argument('--report',required=True);args=parser.parse_args()
    path='unity/Assets/StarTournament/Art/trooper-arms-animated.glb';raw=subprocess.check_output(['git','show',args.ref+':'+path],cwd=ROOT)
    result,report=trim(raw);(ROOT/path).write_bytes(result)
    Path(args.report).write_text(json.dumps({'input':args.ref,'input_sha256':hashlib.sha256(raw).hexdigest(),'output_sha256':hashlib.sha256(result).hexdigest(),'changes':report,'preserved':'original binary, hand/forearm attributes, weapon, skins, clips, textures; world asset untouched'},indent=2))

    digest=hashlib.sha256(result).hexdigest()
    manifest=ROOT/'unity/Assets/StarTournament/Art/asset-manifest.json';data=json.loads(manifest.read_text())
    next(e for e in data['entries'] if e['key']=='player-hands')['sha256']=digest
    manifest.write_text(json.dumps(data,indent=2)+'\n')
    mounts=ROOT/'unity/Assets/StarTournament/Art/trooper-mounts.json';data=json.loads(mounts.read_text());data['arms']['sha256']=digest
    mounts.write_text(json.dumps(data,indent=2)+'\n')
