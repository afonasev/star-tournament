"""Unity-free resource/compatibility audit of actual exported VECTOR GLBs."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT/'unity/Assets/StarTournament/Art'
COMPONENTS = {5121: ('B',1), 5123: ('H',2), 5125: ('I',4), 5126: ('f',4)}
WIDTHS = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}


class Glb:
    def __init__(self, raw):
        assert raw[:4] == b'glTF'
        count = struct.unpack_from('<I',raw,12)[0]
        self.data = json.loads(raw[20:20+count])
        self.binary = raw[28+count:]

    def array(self, index):
        a = self.data['accessors'][index]
        view = self.data['bufferViews'][a['bufferView']]
        fmt, size = COMPONENTS[a['componentType']]
        width = WIDTHS[a['type']]
        start = view.get('byteOffset',0)+a.get('byteOffset',0)
        stride = view.get('byteStride',width*size)
        return [struct.unpack_from('<'+fmt*width,self.binary,start+i*stride) for i in range(a['count'])]

    def images(self):
        result = {}
        for image in self.data.get('images',[]):
            view = self.data['bufferViews'][image['bufferView']]
            offset = view.get('byteOffset',0)
            result[image['name']] = hashlib.sha256(self.binary[offset:offset+view['byteLength']]).hexdigest()
        return result

    def binds(self):
        result = {}
        for skin in self.data['skins']:
            matrices = self.array(skin['inverseBindMatrices'])
            for node, matrix in zip(skin['joints'],matrices):
                name = self.data['nodes'][node]['name']
                if name in result:
                    assert result[name] == matrix
                result[name] = matrix
        return result

    def triangles(self):
        return sum(self.data['accessors'][p['indices']]['count']//3
                   for n in self.data['nodes'] if 'mesh' in n and 'skin' in n
                   for p in self.data['meshes'][n['mesh']]['primitives'])

    def material_contract(self):
        def resolve(value):
            if isinstance(value,dict):
                out = {k:resolve(v) for k,v in value.items()}
                # Texture-info indices reference textures, not arbitrary GLB indices.
                if 'index' in out:
                    texture = self.data['textures'][out['index']]
                    out['index'] = self.data['images'][texture['source']]['name']
                    out['sampler'] = self.data.get('samplers',[])[texture['sampler']] if 'sampler' in texture else None
                return out
            if isinstance(value,list):
                return [resolve(v) for v in value]
            return value
        return {m['name']:resolve(m) for m in self.data['materials']}

    def primitives(self, glove=False, weapon=False):
        result = {}
        for node in self.data['nodes']:
            if 'mesh' not in node or (weapon and node['name']!='weapon:joined') or (glove and 'skin' not in node):
                continue
            for p in self.data['meshes'][node['mesh']]['primitives']:
                name = self.data['materials'][p['material']]['name']
                if glove and not (name.startswith('orbital-restored-') or name=='vector-ceramic-orbital'):
                    continue
                arrays = {k:self.array(v) for k,v in p['attributes'].items()}
                if 'JOINTS_0' in arrays:
                    joints = self.data['skins'][node['skin']]['joints']
                    arrays['JOINTS_0'] = [tuple(self.data['nodes'][joints[j]]['name'] for j in row) for row in arrays['JOINTS_0']]
                indices = [i[0] for i in self.array(p['indices'])]
                # Order-independent vertex records retain topology, UVs, normals and named skin weights.
                keys = sorted(arrays)
                vertices = [tuple((k,arrays[k][i]) for k in keys) for i in range(len(arrays[keys[0]]))]
                tris = sorted(tuple(vertices[i] for i in indices[t:t+3]) for t in range(0,len(indices),3))
                result.setdefault(name,[]).extend(tris)
        return {k:sorted(v) for k,v in result.items()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline',default='016664865206ef85d6a338e2cca86e275a7d5217')
    parser.add_argument('--output',type=Path,required=True)
    args = parser.parse_args()
    manifest = json.loads((ART/'asset-manifest.json').read_text())
    mounts = json.loads((ART/'trooper-mounts.json').read_text())
    report = {'baseline':args.baseline,'assets':{}}
    for kind,key,cap in [('body','player-body',32989),('arms','player-hands',45000)]:
        path = ART/('trooper-'+kind+'-animated.glb')
        rel = path.relative_to(ROOT).as_posix()
        before = Glb(subprocess.check_output(['git','show',args.baseline+':'+rel],cwd=ROOT))
        raw = path.read_bytes();after = Glb(raw)
        digest = hashlib.sha256(raw).hexdigest()
        assert digest == mounts[kind]['sha256'] == next(e['sha256'] for e in manifest['entries'] if e['key']==key)
        assert after.triangles() <= cap, (kind,'triangle budget')
        assert before.binds() == after.binds(), (kind,'bind skeleton changed')
        assert before.images() == after.images(), (kind,'embedded textures changed')
        assert before.material_contract() == after.material_contract(), (kind,'material contract changed')
        assert before.primitives(weapon=True) == after.primitives(weapon=True), (kind,'weapon geometry changed')
        for node in after.data['nodes']:
            if 'skin' not in node or 'mesh' not in node:
                continue
            for primitive in after.data['meshes'][node['mesh']]['primitives']:
                for weights in after.array(primitive['attributes']['WEIGHTS_0']):
                    assert abs(sum(weights)-1)<.0001, (kind,'unnormalized skin')
        if kind=='arms':
            assert before.primitives(glove=True) == after.primitives(glove=True), 'First-person glove anatomy/UVs/normals/weights changed'
        report['assets'][kind] = {'sha256':digest,'triangles_before':before.triangles(),'triangles_after':after.triangles(),
                                'binds_textures_materials_weapon':'unchanged','weights':'normalized','first_person_glove':'unchanged' if kind=='arms' else 'world resolution reduced'}
    source = ROOT/'scripts/vector_fighter/f16-orbital-hands.blend'
    original = subprocess.check_output(['git','show',args.baseline+':'+source.relative_to(ROOT).as_posix()],cwd=ROOT)
    assert source.read_bytes()==original, 'Editable high-poly source modified'
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))


if __name__=='__main__':
    main()
