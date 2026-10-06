"""Preserve attribution, source evidence, and checksums beside the local deliverable."""
import hashlib,json,struct,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
LIB=ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper'
OUT=LIB/'animation-pipeline-v2';EVIDENCE=ROOT/'docs/evidence/trooper-animation-pipeline'
raw=(LIB/'trooper-2k.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];gltf=json.loads(raw[20:20+n])
provenance={'sourceFile':'../trooper-2k.glb','sourceSha256':hashlib.sha256(raw).hexdigest(),
    'embeddedAssetMetadata':gltf['asset'],'sourceRecord':(LIB/'SOURCE.md').read_text(),
    'verificationDate':'2026-09-19','webVerification':{'sketchfabModelPage':'403 Forbidden','sketchfabApi':'unavailable through web tool',
    'creativeCommonsDeed':'https://creativecommons.org/licenses/by/4.0/ read successfully'},
    'changes':['Baked imported transforms; normalized to 1.8m total height, feet origin',
    'Welded coincident vertices preserving UV face corners and materials',
    'Added humanoid armature, linear skinning, authored in-place and one-shot actions',
    'v2: 30 digit bones, 4 forearm twist bones, topology-aware glove/pelvis weights, fixed-grip fitting'],
    'externalAnimationSources':[],'status':'offline candidate; no runtime or manifest adoption'}
(OUT/'provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2))
(OUT/'ATTRIBUTION.md').write_text('''# Attribution — candidate derivative

“Sci-Fi Soldier / Futuristic Combat Trooper” by ART_LOLL (evgenytvidov).

Source: https://sketchfab.com/3d-models/sci-fi-soldier-futuristic-combat-trooper-de876bfdce1c47a4aa67670faee7208e
Author: https://sketchfab.com/evgenytvidov
License: Creative Commons Attribution 4.0 International — https://creativecommons.org/licenses/by/4.0/

Modifications: transformed/normalized geometry, coincident vertex weld, humanoid
skeleton, skin weights, locally authored idle/walk/run/aim/fire/hit/death clips.
v2 adds articulated fingers, forearm twist distribution, harmonic weights and grip fitting.
No external animation pack was used. No endorsement by the original author is implied.
Retain original attribution, source and license links, and the modification notice
when redistributing the derivative or its previews. See provenance.json for evidence.

The original SOURCE.md and source GLB are retained unchanged in the parent directory.
The previous rigid-part compatible-rig GLB was not used as source.
''')
for name in ['source-audit.json','provenance.json','ATTRIBUTION.md','grip-audit.json','grip-recipe.json']:
    shutil.copyfile(OUT/name,EVIDENCE/name)
files=[LIB/'trooper-2k.glb',LIB/'SOURCE.md']+sorted(p for p in OUT.iterdir() if p.is_file() and p.name not in ['SHA256SUMS.json'] and not p.name.endswith('.blend1'))
checksums={str(p.relative_to(LIB)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
checksums.update({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((ROOT/'scripts/trooper').iterdir()) if p.suffix in ('.py','.sh')})
(OUT/'SHA256SUMS.json').write_text(json.dumps(checksums,indent=2))
print('Preserved attribution, source evidence, and',len(checksums),'checksums')

evidence_names=['clip-overview.png','motion-phases.png','source-and-combat.png','rig-skeleton.png','export-readback-run.png','source-audit.json','export-audit.json','hands-and-grip.png','grip-audit.json','grip-recipe.json']
(EVIDENCE/'evidence-index.json').write_text(json.dumps({'candidate':'trooper-humanoid-v2','glbSha256':checksums['animation-pipeline-v2/trooper-skinned-animated.glb'],'qualityAccepted':False,'sha256':{name:hashlib.sha256((EVIDENCE/name).read_bytes()).hexdigest() for name in evidence_names}},indent=2))
