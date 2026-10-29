"""F23 participant-color coverage. Material assignments only; no geometry reduction.
Retains F22 as immutable comparison and uses the existing identity/PBR material.
"""
import hashlib,json
from pathlib import Path
import bpy
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'scripts/vector_fighter/f22-reconstruction.blend'
TARGET=ROOT/'scripts/vector_fighter/f23-color-coverage.blend'
OUT=ROOT/'.local/vector-f23';OUT.mkdir(parents=True,exist_ok=True)

def identity_area():
    return sum(p.area for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('VECTOR')
               for p in o.data.polygons if o.data.materials[p.material_index].name.startswith('vector-fighter-identity'))

def main():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()=='2de5b6f918a09c861b79a62e3101b91a91bfca18ed85894593a7e3a0ab5f8167'
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    before=identity_area();paint=bpy.data.materials['vector-fighter-identity-reference'];changed=[]
    zones=('pectoral upper','pectoral machined face','scapula','shoulder cap','pauldron face insert','forearm main','quadriceps inset','tibial crest')
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or not obj.name.startswith('VECTOR F22 ') or 'perimeter wear' in obj.name:continue
        if not any(zone in obj.name for zone in zones):continue
        assert obj.data.materials[0].name=='vector-reference-coated-steel',obj.name
        obj.data.materials[0]=paint;changed.append(obj.name)
    assert len(changed)==16,changed
    after=identity_area()
    report={'baseline':str(SOURCE.relative_to(ROOT)),'changed_panels':changed,'identity_surface_area_m2':{'before':before,'after':after,'ratio':after/before},
            'geometry':'unchanged','skeleton':'unchanged','gloves':'unchanged; native grip evaluator required','materials':'reuses existing identity PBR; edge and seal slots unchanged',
            'optimization':'No added geometry, materials or textures. Reduction/performance deferred by user.'}
    (OUT/'color-coverage.json').write_text(json.dumps(report,indent=2)+'\n')
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(TARGET),compress=True)
    print('F23_COLOR_COVERAGE',report['identity_surface_area_m2'])
if __name__=='__main__':main()
