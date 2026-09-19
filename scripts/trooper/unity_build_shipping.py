"""Export the existing v2's weapon-adapted body and arms with bounded shared textures.
Run unity_grip_bake.py, unity_fit_contact.py, unity_grip_bake.py --fitted first.
"""
import bpy,bmesh,json,hashlib,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];WORK=ROOT/'.local/trooper-shipping';ART=ROOT/'unity/Assets/StarTournament/Art';E=ROOT/'docs/evidence/unity-trooper-2026-09-19';E.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(WORK/'trooper-gripped.blend'))
rig=bpy.data.objects['TrooperHumanoid'];meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.find_armature()==rig];gun=bpy.data.objects['weapon:joined'];mount=bpy.data.objects['trooper:rig:weapon-mount']
for image in bpy.data.images:
 if max(image.size)>1024:
  factor=1024/max(image.size);image.scale(max(1,round(image.size[0]*factor)),max(1,round(image.size[1]*factor)))
bpy.ops.file.pack_all()
# Both files originate in one Blender material set; no second gun import means no .001 copies.
rig.animation_data.action=None
for pb in rig.pose.bones:pb.matrix_basis.identity()
bpy.context.view_layer.update()
def export(path,objs):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs+[rig,gun,mount]:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=True,export_extras=True,export_yup=True,export_skins=True,export_all_influences=False)
body=ART/'trooper-body-animated.glb';armsfile=ART/'trooper-arms-animated.glb';export(body,meshes)
# Anatomical cross-section above the elbows, rather than a weight threshold
# through blended armor/cloth: the cut stays below the first-person frustum.
# Skeleton and animation tracks remain identical to the body.
source_audit=json.loads((ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2/source-audit.json').read_text())
source_scale=source_audit['scaleFactor'];source_floor=source_audit['sourceFloorZ']
prefixes=('LeftShoulder','RightShoulder','LeftUpperArm','RightUpperArm','LeftLowerArm','RightLowerArm','LeftHand','RightHand','LeftThumb','RightThumb','LeftIndex','RightIndex','LeftMiddle','RightMiddle','LeftRing','RightRing','LeftLittle','RightLittle');arms=[]
for src in meshes:
 duplicate=src.copy();duplicate.data=src.data.copy();bpy.context.collection.objects.link(duplicate)
 keep={v.index for v in duplicate.data.vertices if (abs(v.co.x)/source_scale-(v.co.z/source_scale+source_floor))/math.sqrt(2)>0 and sum(g.weight for g in v.groups if duplicate.vertex_groups[g.group].name.startswith(prefixes))>=.5}
 bm=bmesh.new();bm.from_mesh(duplicate.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index not in keep],context='VERTS');bm.to_mesh(duplicate.data);bm.free()
 if not duplicate.data.polygons:bpy.data.objects.remove(duplicate,do_unlink=True)
 else:arms.append(duplicate)
export(armsfile,arms)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta={'version':'unity-trooper-shipping-v2','sourceSha256':'53add01552d98244b75039c33319293c53e2eecc046b11890d6fa247fd13ef53','units':'meters','up':'+Y','front':'+Z','restHeightMeters':1.8,'pivot':'feet-center','body':{'path':body.name,'sha256':sha(body)},'arms':{'path':armsfile.name,'sha256':sha(armsfile)},'clips':['idle','walk','run','aim','fire','hit','death'],'weapon':'same current world shotgun in both derivatives; old robot hands excluded; actual grasp-center IK and contact-fit finger angles','gripRecipe':json.loads((WORK/'grip-recipe.json').read_text()),'contactFit':json.loads((WORK/'contact-fit.json').read_text()),'maxTextureDimension':1024,'rootMotion':'death skeleton Root only; applyRootMotion=false on visual Animator; motor authoritative'}
(ART/'trooper-mounts.json').write_text(json.dumps(meta,indent=2)+'\n');(E/'asset-shipping-audit.json').write_text(json.dumps({'status':'EXPORTED_REQUIRES_READBACK','bodyBytes':body.stat().st_size,'armsBytes':armsfile.stat().st_size,**meta},indent=2)+'\n')
lib=ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2'
(ART/'trooper-ATTRIBUTION.md').write_text((lib/'ATTRIBUTION.md').read_text()+'\nLicense: CC BY 4.0.\nUnity adaptation: texture resize to 1024px, existing shotgun geometry joined, existing arm/finger tracks fitted to that weapon, arms-only derivative. No new character or third-party animations.\n')
print('SHIPPING',body.stat().st_size,armsfile.stat().st_size)

manifest_path=ART/'asset-manifest.json'
manifest=json.loads(manifest_path.read_text())
for entry in manifest['entries']:
 if entry['key'] in ['player-body','player-hands']:entry['sha256']=sha(ROOT/'unity'/entry['path'])
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
