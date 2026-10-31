"""Independent before/after GLB audit. Baseline is read from a Git revision.

--prepare-baseline creates only reconstructible Unity import inputs for the
explicit graphical fixture. Delete Art/WeaponRenderBaseline after that review.
"""
import argparse
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import subprocess
import optimize_weapon_meshes as opt


def verify(raw_before, raw_after):
    before, bb = opt.read(raw_before)
    after, ab = opt.read(raw_after)
    def normalized(doc, binary):
        doc = deepcopy(doc)
        # Preserve actual animation/skin bytes independently of reindexed IDs.
        def accessor(i):
            meta = {k:v for k,v in doc['accessors'][i].items() if k not in ('bufferView','byteOffset')}
            return {'metadata':meta, 'bytes':hashlib.sha256(b''.join(opt.values(doc,binary,i))).hexdigest()}
        for skin in doc.get('skins',[]):
            if 'inverseBindMatrices' in skin:skin['inverseBindMatrices']=accessor(skin['inverseBindMatrices'])
        for animation in doc.get('animations',[]):
            for sampler in animation['samplers']:
                for key in ('input','output'):sampler[key]=accessor(sampler[key])
        for image in doc.get('images',[]):
            if 'bufferView' in image:
                v=doc['bufferViews'][image.pop('bufferView')];offset=v.get('byteOffset',0)
                image['content_sha256']=hashlib.sha256(binary[offset:offset+v['byteLength']]).hexdigest()
        for mesh in doc['meshes']:
            for p in mesh['primitives']:
                attrs={k:opt.values(doc,binary,i) for k,i in p['attributes'].items()}
                order=opt.indices(doc,binary,p)
                p['triangle_signature']=sorted((k.hex(),v) for k,v in opt.signature(attrs,order).items())
                p['attribute_metadata']={k:{a:v for a,v in doc['accessors'][i].items() if a not in ('bufferView','byteOffset','count')} for k,i in p.pop('attributes').items()}
                p.pop('indices')
        for key in ('accessors','bufferViews','buffers'):doc.pop(key,None)
        return doc
    assert normalized(before,bb)==normalized(after,ab), 'Asset semantic/byte preservation failed'
    return {'complete_oriented_triangle_attributes':'exact', 'materials_images_nodes_skins_animations':'exact'}


def main():
    parser=argparse.ArgumentParser(__doc__)
    parser.add_argument('--baseline',required=True)
    parser.add_argument('--report',type=Path,required=True)
    parser.add_argument('--prepare-baseline',action='store_true')
    args=parser.parse_args()
    report=[]
    manifest=json.loads((opt.ART/'asset-manifest.json').read_text())
    mounts=json.loads((opt.ART/'trooper-mounts.json').read_text())
    for name in opt.FILES:
        path=opt.ART/name;raw=subprocess.check_output(['git','show',args.baseline+':'+path.as_posix()]);candidate=path.read_bytes()
        row={'file':name,**verify(raw,candidate)};digest=hashlib.sha256(candidate).hexdigest()
        entries=[e for e in manifest['entries'] if e.get('path','').endswith(name)]
        # Manifest schema uses file rather than path in current project.
        if not entries:entries=[e for e in manifest['entries'] if e.get('file','').endswith(name)]
        if not entries:
            assert name == "vector-shotgun-lod1.glb", name
            row["manifest_reference"]="not referenced by current manifest (preserved standalone LOD companion)"
        assert all(e['sha256']==digest for e in entries),name
        row['manifest_sha256']=digest
        if name.startswith('trooper-'):
            assert mounts['body' if 'body' in name else 'arms']['sha256']==digest
        if args.prepare_baseline:
            baseline=opt.ART/'WeaponRenderBaseline'/name;baseline.parent.mkdir(parents=True,exist_ok=True);baseline.write_bytes(raw)
        report.append(row)
    args.report.parent.mkdir(parents=True,exist_ok=True);args.report.write_text(json.dumps({'baseline':args.baseline,'files':report},indent=2)+'\n')
    print('PASS:',len(report),'assets; exact triangle attributes, materials, image bytes, transforms, skins, animations and hashes')


if __name__=='__main__':main()
