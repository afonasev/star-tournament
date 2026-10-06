"""Read back GLB hands, check exact triangle/finite-cylinder clearance, render QA.
The fixed gauge and lights are review geometry, never character/runtime assets.
"""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'scripts/trooper'))
from anatomy import xyz,uvw,GRIP_CENTER,GRIP_RADIUS,GRIP_V_RANGE
from geometry_checks import radial_distance_to_triangle
OUT=ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2'
E=ROOT/'docs/evidence/trooper-animation-pipeline'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(OUT/'trooper-skinned-animated.glb'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
glove=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('sourceMesh')=='Object_3')
for track in rig.animation_data.nla_tracks:track.mute=True
s=bpy.context.scene;s.render.fps=30;s.render.engine='BLENDER_EEVEE';s.render.resolution_x=1000;s.render.resolution_y=1000;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('QA');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[0].default_value=(.07,.09,.13,1)
for loc,energy in [((2,-3,4),600),((-3,-1,2),500),((0,3,3),900)]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=energy;o.data.size=3;o.rotation_euler=(Vector((0,0,1.1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=.27;s.camera=cam
meta=json.loads((OUT/'source-audit.json').read_text());scale=meta['scaleFactor'];floor=meta['sourceFloorZ']
def source_point(p):return Vector((p[0]*scale,p[1]*scale,(p[2]-floor)*scale))
def skin_matrix(side):return rig.matrix_world@rig.pose.bones[side+'Hand'].matrix@rig.data.bones[side+'Hand'].matrix_local.inverted()
# Isolated QA hand surfaces keep the torso/opposite hand from hiding the view.
# All measurements still use the complete original exported glove mesh.
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':obj.hide_render=True
def hand_preview(side):
    evaluated=glove.evaluated_get(bpy.context.evaluated_depsgraph_get())
    retained=set()
    for i,v in enumerate(glove.data.vertices):
        q=uvw(Vector((v.co.x/scale,v.co.y/scale,v.co.z/scale+floor)))
        if q.x>.29 and sum(w for name,w in groups[i] if name.startswith(side))>.5:retained.add(i)
    indices=sorted(retained);mapping={old:new for new,old in enumerate(indices)}
    faces=[p for p in glove.data.polygons if all(i in retained for i in p.vertices)]
    mesh=bpy.data.meshes.new('QA_IsolatedHand');mesh.from_pydata([evaluated.data.vertices[i].co for i in indices],[],[[mapping[i] for i in p.vertices] for p in faces])
    for material in glove.data.materials:mesh.materials.append(material)
    uv=mesh.uv_layers.new()
    for target,original in zip(mesh.polygons,faces):
        target.material_index=original.material_index;target.use_smooth=True
        for new_loop,old_loop in zip(target.loop_indices,original.loop_indices):uv.data[new_loop].uv=glove.data.uv_layers.active.data[old_loop].uv
    obj=bpy.data.objects.new('QA_IsolatedHand',mesh);bpy.context.collection.objects.link(obj);obj.matrix_world=glove.matrix_world.copy();return obj
def render(label):s.render.filepath=str(E/(label+'.png'));bpy.ops.render.render(write_still=True)


glove.data.calc_loop_triangles()
groups=[[ (glove.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in glove.data.vertices]
side_triangles={side:[tuple(t.vertices) for t in glove.data.loop_triangles if sum(w for i in t.vertices for name,w in groups[i] if name.startswith(side))>=1.5] for side in ['Left','Right']}

def measure_hand(side,transform):
    inv=transform.inverted();deps=bpy.context.evaluated_depsgraph_get();ev=glove.evaluated_get(deps)
    coords=[];distances={d:[] for d in (['Thumb','Index','Middle','Ring','Little'] if side=='Left' else ['Thumb','Middle','Ring','Little'])}
    for i,v in enumerate(ev.data.vertices):
        local=inv@(glove.matrix_world@v.co);q=uvw(Vector((local.x/scale,local.y/scale,local.z/scale+floor)));coords.append(q)
        if GRIP_V_RANGE[0]<q.y<GRIP_V_RANGE[1]:
            radial=math.hypot(q.x-GRIP_CENTER[0],q.z-GRIP_CENTER[1])
            for digit in distances:
                if any(name.startswith(side+digit) and weight>.35 for name,weight in groups[i]):distances[digit].append((radial-GRIP_RADIUS)*scale)
    surface_min=float('inf')
    for tri in side_triangles[side]:
        radial=radial_distance_to_triangle([coords[i] for i in tri],GRIP_CENTER,GRIP_V_RANGE)
        if radial is not None:surface_min=min(surface_min,(radial-GRIP_RADIUS)*scale)
    assert math.isfinite(surface_min)
    return {'side':side,'radiusMeters':GRIP_RADIUS*scale,'testedTriangles':len(side_triangles[side]),'surfaceMinClearanceMeters':surface_min,
            'fingerMinClearanceMeters':{d:min(v) if v else None for d,v in distances.items()}}

def gauge(transform,sign):
    objects=[]
    for vv in GRIP_V_RANGE:
        curve=bpy.data.curves.new('QA_GripGauge','CURVE');curve.dimensions='3D';curve.bevel_depth=.0006
        spline=curve.splines.new('POLY');spline.points.add(63)
        for i,p in enumerate(spline.points):
            theta=2*math.pi*i/64;pos=transform@source_point(xyz((GRIP_CENTER[0]+GRIP_RADIUS*math.cos(theta),vv,GRIP_CENTER[1]+GRIP_RADIUS*math.sin(theta)),sign));p.co=(*pos,1)
        spline.use_cyclic_u=True;obj=bpy.data.objects.new('QA_GripGauge',curve);bpy.context.collection.objects.link(obj);objects.append(obj)
    return objects

samples=[]
for clip,phases in [('idle',[.25]),('aim',[.25]),('fire',[0,.125,.22,.25,.5,1])]:
    action=next(a for a in bpy.data.actions if a.name==clip or a.name.startswith(clip+'_'))
    rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
    a,b=action.frame_range
    for phase in phases:
        frame=a+(b-a)*phase;s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
        for side,sign in [('Left',1),('Right',-1)]:
            transform=skin_matrix(side)
            if clip!='idle':samples.append({'clip':clip,'phase':phase,**measure_hand(side,transform)})
            if phase!=.25:continue
            preview=hand_preview(side)
            center=transform@source_point(xyz((.47,.065,.454),sign));objects=gauge(transform,sign) if clip!='idle' else []
            for view,offset in [('dorsal',Vector((sign,0,1)).normalized()),('palm',Vector((-sign,-.25,-1)).normalized())]:
                direction=transform.to_3x3()@offset;cam.location=center+direction*.6;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();render(f'hand-{side.lower()}-{clip}-{view}')
            for obj in objects+[preview]:bpy.data.objects.remove(obj,do_unlink=True)
errors=[]
for sample in samples:
    if sample['surfaceMinClearanceMeters']<-.00005:errors.append(f"{sample['clip']} {sample['phase']} {sample['side']}: gauge penetration")
    if any(v is None or v<-.00005 or v>.003 for v in sample['fingerMinClearanceMeters'].values()):errors.append(f"{sample['clip']} {sample['phase']} {sample['side']}: missing finger contact")
report={'status':'PASS_GRIP_FIXTURE' if not errors else 'FAIL','fixtureOnly':True,'gameWeaponFitAccepted':False,'method':'Exact exported triangle projection after finite V-slab clipping; vertex contact per load-bearing digit. Right index reserved for trigger motion.',
        'samples':samples,'errors':errors}
(E/'grip-audit.json').write_text(json.dumps(report,indent=2));(OUT/'grip-audit.json').write_text(json.dumps(report,indent=2))
print(report['status'],len(samples),'hand poses',errors)
if errors:raise RuntimeError('Grip fixture audit failed')
