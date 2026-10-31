"""Check the actual exported ORM at textile triangle UV centroids (Pillow)."""
import io,json,sys
from pathlib import Path
from PIL import Image
from audit_runtime import Glb,ART

g=Glb((ART/'trooper-body-animated.glb').read_bytes());samples=[]
for node in g.data['nodes']:
    if 'skin' not in node or 'mesh' not in node:continue
    for p in g.data['meshes'][node['mesh']]['primitives']:
        m=g.data['materials'][p['material']]
        if m['name']!='vector-body-b01-textile-baked':continue
        tex=g.data['textures'][m['pbrMetallicRoughness']['metallicRoughnessTexture']['index']]
        im=g.data['images'][tex['source']];v=g.data['bufferViews'][im['bufferView']];start=v.get('byteOffset',0)
        image=Image.open(io.BytesIO(g.binary[start:start+v['byteLength']])).convert('RGB')
        uv=g.array(p['attributes']['TEXCOORD_0']);ids=[x[0] for x in g.array(p['indices'])]
        for i in range(0,len(ids),3):
            u=sum(uv[j][0] for j in ids[i:i+3])/3;v=sum(uv[j][1] for j in ids[i:i+3])/3
            # glTF UV origin and encoded image rows are both top-left.
            x=min(image.width-1,max(0,int(u*image.width)));y=min(image.height-1,max(0,int(v*image.height)))
            samples.append(image.getpixel((x,y))[2])
assert samples,'Actual textile primitive required'
samples.sort();median=samples[len(samples)//2];p95=samples[int(.95*(len(samples)-1))]
assert median==0 and p95<=1,('Textile ORM metallic should be dielectric',median,p95)
result={'result':'PASS','textile_samples':len(samples),'metallic_byte_median':median,'metallic_byte_p95':p95,'source':'actual exported glTF material and UV triangle centroids; B channel of packed ORM'}
Path(sys.argv[1]).write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
