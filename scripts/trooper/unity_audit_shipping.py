"""Independent GLB readback: bindings, actual hand surfaces vs actual weapon, visual frames.
Surface distances are diagnostics on the unmodified exported meshes, not artistic acceptance.
"""
import bpy,json,sys,math,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'scripts/trooper'))
from anatomy import uvw
ART=ROOT/'unity/Assets/StarTournament/Art';E=ROOT/'docs/evidence/unity-trooper-2026-09-19';expected={'idle','walk','run','aim','fire','hit','death'}
meta=json.loads((ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2/source-audit.json').read_text());scale=meta['scaleFactor'];floor=meta['sourceFloorZ'];report={};samples=[]
for label in ['body','arms']:
 bpy.ops.wm.read_factory_settings(use_empty=True);path=ART/f'trooper-{label}-animated.glb';bpy.ops.import_scene.gltf(filepath=str(path));s=bpy.context.scene
 rig=next(o for o in s.objects if o.type=='ARMATURE');assert len(rig.data.bones)==57
 assert expected<={a.name for a in bpy.data.actions};assert len([o for o in s.objects if o.name=='trooper:rig:weapon-mount'])==1
 gun=bpy.data.objects['weapon:joined'];glove=next(o for o in s.objects if o.type=='MESH' and o.get('sourceMesh')=='Object_3')
 assert glove.vertex_groups, 'Readback must retain skin weights; wrist proxies are not grip evidence'
 glove.data.calc_loop_triangles();groups=[[(glove.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in glove.data.vertices]
 sides={}
 for side in ['Left','Right']:
  ids={i for i,v in enumerate(glove.data.vertices) if uvw(Vector((v.co.x/scale,v.co.y/scale,v.co.z/scale+floor))).x>.29 and sum(w for n,w in groups[i] if n.startswith(side))>.5}
  triangles=[tuple(t.vertices) for t in glove.data.loop_triangles if all(i in ids for i in t.vertices)]
  sides[side]=(ids,triangles)
 report[label]={'status':'PASS_TECHNICAL','sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bones':57,'clips':sorted(expected),'meshCount':len([o for o in s.objects if o.type=='MESH']),'handVertices':{k:len(v[0]) for k,v in sides.items()}}
 s.render.engine='BLENDER_EEVEE';s.render.resolution_x=1100;s.render.resolution_y=800;s.render.resolution_percentage=100
 s.world=bpy.data.worlds.new('Audit');s.world.color=(.12,.12,.12)
 for pos in [(2,-2,3),(-2,-3,2)]:
  bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=450;l.data.size=3;l.rotation_euler=(Vector((0,-.4,1.25))-l.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add(location=(-.6,-1.25,1.7));cam=bpy.context.object;cam.rotation_euler=(Vector((0,-.35,1.27))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.8;s.camera=cam
 for clip,phases in [('idle',[.25]),('walk',[.25,.75]),('run',[.25,.75]),('aim',[.25]),('fire',[0,.22,.5,1]),('hit',[.5])]:
  action=bpy.data.actions[clip];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];a,b=action.frame_range
  for phase in phases:
   frame=a+(b-a)*phase;s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
   bvh=BVHTree.FromPolygons([gun.matrix_world@v.co for v in gun.data.vertices],[tuple(p.vertices) for p in gun.data.polygons])
   ev=glove.evaluated_get(bpy.context.evaluated_depsgraph_get());coords=[glove.matrix_world@v.co for v in ev.data.vertices]
   for side,(ids,triangles) in sides.items():
    def distance(p):
     near,n,idx,d=bvh.find_nearest(p);return d if (p-near).dot(n)>=0 else -d
    distances={i:distance(coords[i]) for i in ids};centroids=[distance(sum((coords[i] for i in tri),Vector())/3) for tri in triangles]
    fingers={d:min((distances[i] for i in ids if any(n.startswith(side+d) and w>.35 for n,w in groups[i])),default=None) for d in ['Thumb','Index','Middle','Ring','Little']}
    overlaps=len(BVHTree.FromPolygons(coords,triangles,all_triangles=True).overlap(bvh))
    samples.append({'triangleOverlapPairs':overlaps,'asset':label,'clip':clip,'phase':phase,'side':side,'minimumSignedVertexDistance':min(distances.values()),'minimumSignedCentroidDistance':min(centroids),'fingerAbsoluteDistances':{d:min((abs(distances[i]) for i in ids if any(n.startswith(side+d) and w>.35 for n,w in groups[i])),default=None) for d in ['Thumb','Index','Middle','Ring','Little']},'nearestAbsoluteContact':min(abs(v) for v in distances.values()),'fingerMinDistances':fingers,'vertices':len(ids),'triangles':len(triangles)})
   if clip in ['aim','fire'] and phase in [.25,.22,1]:
    s.render.filepath=str(E/f'asset-grip-{label}-{clip}-{int(phase*100):03d}.png');bpy.ops.render.render(write_still=True)
(E/'asset-import-readback.json').write_text(json.dumps(report,indent=2)+'\n')
(E/'asset-exported-contact-audit.json').write_text(json.dumps({'status':'PASS_SAMPLED_SURFACE_INTERSECTION_CHECK' if not any(x['triangleOverlapPairs'] for x in samples) else 'FAIL_SURFACE_INTERSECTIONS','sampleCount':len(samples),'fullHandSurfaceIntersections':sum(x['triangleOverlapPairs'] for x in samples),'artisticAcceptance':False,'method':'Exported weighted hand vertices AND triangle centroids vs BVH of the actual embedded shotgun. Triangle BVH overlap included for the full hand surface. Signed nearest-surface values are diagnostic only because joined source gun has overlapping/mixed-winding components. No wrist-anchor or proxy-cylinder substitution.','samples':samples},indent=2)+'\n')
print('READBACK',len(samples),'hand/phase samples',report)

assert not any(x['triangleOverlapPairs'] for x in samples), 'Exported hand/weapon surfaces intersect; inspect audit before shipping'
