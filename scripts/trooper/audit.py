"""Independent exported-GLB audit. Blender --background --python scripts/trooper/audit.py.
Parses serialized weights/clips, then reimports in a fresh scene and samples deformation.
"""
import bpy, json, struct, math, hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2'
EVIDENCE=ROOT/'docs/evidence/trooper-animation-pipeline'
p=OUT/'trooper-skinned-animated.glb';data=p.read_bytes();size=struct.unpack_from('<I',data,12)[0]
gltf=json.loads(data[20:20+size]);binary=data[28+size:]
def accessor(index):
    a=gltf['accessors'][index];v=gltf['bufferViews'][a['bufferView']]
    code,bytes_={5126:('f',4),5125:('I',4),5123:('H',2),5121:('B',1)}[a['componentType']]
    n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
    stride=v.get('byteStride',n*bytes_);start=v.get('byteOffset',0)+a.get('byteOffset',0)
    return [struct.unpack_from('<'+code*n,binary,start+i*stride) for i in range(a['count'])]
errors=[];warnings=[];weight_sums=[];zero=0;bad_joints=0;triangles=0;max_influences=0
assert len(gltf['skins'])==1
skin=gltf['skins'][0];joint_count=len(skin['joints']);joint_coverage=[0]*joint_count
expected_bones=json.loads((OUT/'humanoid-map.json').read_text())['bones']
actual_bones={gltf['nodes'][i]['name']:i for i in skin['joints']}
assert set(actual_bones)==set(expected_bones), 'Humanoid bone set mismatch'
assert len(accessor(skin['inverseBindMatrices']))==joint_count
for name,bone in expected_bones.items():
    if bone['parent']:
        assert actual_bones[name] in gltf['nodes'][actual_bones[bone['parent']]].get('children',[]), 'Hierarchy mismatch: '+name
for m in gltf['meshes']:
    for p0 in m['primitives']:
        attrs=p0['attributes'];triangles+=gltf['accessors'][p0['indices']]['count']//3
        if 'WEIGHTS_0' not in attrs or 'JOINTS_0' not in attrs:errors.append('Unskinned primitive');continue
        for w,j in zip(accessor(attrs['WEIGHTS_0']),accessor(attrs['JOINTS_0'])):
            weight_sums.append(sum(w));zero+=sum(w)<1e-8;bad_joints+=sum(idx>=joint_count for idx in j)
            for idx,weight in zip(j,w):
                if weight>0 and idx<joint_count:joint_coverage[idx]+=1
            max_influences=max(max_influences,sum(x>0 for x in w))
            if any(not math.isfinite(x) or x<0 for x in w):errors.append('Invalid weight')
if zero or bad_joints or max(abs(s-1) for s in weight_sums)>1e-5:errors.append('Skin normalization/coverage failure')
coverage={gltf['nodes'][idx]['name']:count for idx,count in zip(skin['joints'],joint_coverage)}
required_fingers=[side+digit+segment for side in ['Left','Right'] for digit in ['Thumb','Index','Middle','Ring','Little'] for segment in ['Proximal','Intermediate','Distal']]
required_twists=[side+'LowerArm'+suffix for side in ['Left','Right'] for suffix in ['Twist1','Twist2']]
if max_influences>4:errors.append('Four-influence export contract violated')
if any(coverage.get(name,0)==0 for name in required_fingers+required_twists):errors.append('Missing or unweighted digit/twist joint')
expected=json.loads((OUT/'clips.json').read_text());expected_names={a['name'] for a in expected['clips']}
if {a['name'] for a in gltf.get('animations',[])}!=expected_names:errors.append('Clip set mismatch')
serialized=[]
for a in gltf.get('animations',[]):
    times=[x[0] for s in a['samplers'] for x in accessor(s['input'])]
    name=a['name'];duration=max(times)-min(times);wanted=next(x for x in expected['clips'] if x['name']==name)
    if abs(duration-wanted['durationSeconds'])>1e-5:errors.append(name+' duration mismatch')
    loop_error=0;quaternions=[]
    for ch in a['channels']:
        sampler=a['samplers'][ch['sampler']];values=accessor(sampler['output'])
        if ch['target']['path']=='rotation':
            quaternions.extend(abs(sum(q*q for q in v)-1) for v in values)
            if wanted['loop']:loop_error=max(loop_error,min(max(abs(x-y) for x,y in zip(values[0],values[-1])),max(abs(x+y) for x,y in zip(values[0],values[-1]))))
        elif wanted['loop']:loop_error=max(loop_error,max(abs(x-y) for x,y in zip(values[0],values[-1])))
        if any(not math.isfinite(v) for row in values for v in row):errors.append(name+' nonfinite key')
    if wanted['loop'] and loop_error>1e-5:errors.append(name+' loop mismatch')
    if max(quaternions)>1e-5:errors.append(name+' quaternion norm mismatch')
    serialized.append({'name':name,'durationSeconds':duration,'channels':len(a['channels']),'loopEndpointMaxError':loop_error if wanted['loop'] else None,'quaternionNormMaxError':max(quaternions)})

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p))
scene=bpy.context.scene;scene.render.fps=30
rig=next(o for o in scene.objects if o.type=='ARMATURE');meshes=[o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
assert len(meshes)==len(gltf['meshes'])
for track in rig.animation_data.nla_tracks:track.mute=True
# glTF readback actions have importer naming prefixes. Match exact suffix as needed.
actions={}
for name in expected_names:
    matches=[a for a in bpy.data.actions if a.name==name or a.name.startswith(name+'_') or a.name.startswith(name+'|')]
    if len(matches)!=1:raise RuntimeError((name,[a.name for a in bpy.data.actions]))
    actions[name]=matches[0]
rig.animation_data.action=None
for pb in rig.pose.bones:pb.matrix_basis.identity()
bpy.context.view_layer.update()
def vertices():
    deps=bpy.context.evaluated_depsgraph_get()
    return [[o.matrix_world@v.co for v in o.evaluated_get(deps).data.vertices] for o in meshes]
rest=vertices();rest_points=[p for vs in rest for p in vs]
rest_height=max(p.z for p in rest_points)-min(p.z for p in rest_points)
if abs(rest_height-1.8)>.0001:errors.append('Rest height mismatch')
base_edges=[]
for o,vs in zip(meshes,rest):
    base_edges.append([(e.vertices[0],e.vertices[1],(vs[e.vertices[0]]-vs[e.vertices[1]]).length) for e in o.data.edges])
deformations=[]
for desc in expected['clips']:
    name=desc['name'];rig.animation_data.action=actions[name]
    if hasattr(rig.animation_data,'action_slot') and actions[name].slots:rig.animation_data.action_slot=actions[name].slots[0]
    start,end=actions[name].frame_range
    mesh_stretch={o.name:0 for o in meshes};mesh_worst={}
    frames=[];max_stretch=0;max_edge_growth=0;worst_edge=None;min_floor=100;max_floor=-100
    for k in range(17):
        frame=float(start+(end-start)*k/16);scene.frame_set(int(frame),subframe=frame-int(frame));vs_all=vertices();points=[p for vs in vs_all for p in vs]
        floor=min(p.z for p in points);min_floor=min(floor,min_floor);max_floor=max(floor,max_floor)
        for obj,rest_vs,vs,edges in zip(meshes,rest,vs_all,base_edges):
            for a,b,length in edges:
                if length<1e-6:continue
                current=(vs[a]-vs[b]).length
                if current/length>mesh_stretch[obj.name]:mesh_worst[obj.name]={'restA':list(rest_vs[a]),'restB':list(rest_vs[b]),'fraction':k/16}
                mesh_stretch[obj.name]=max(mesh_stretch[obj.name],current/length)
                if current/length>max_stretch:worst_edge={'mesh':obj.name,'restA':list(rest_vs[a]),'restB':list(rest_vs[b]),'fraction':k/16}
                max_stretch=max(max_stretch,current/length);max_edge_growth=max(max_edge_growth,current-length)
        if any(not math.isfinite(x) for p in points for x in p):errors.append(name+' nonfinite deformation')
        frames.append({'fraction':k/16,'floorZ':floor})
    if min_floor<-.015:warnings.append(name+' floor penetration over 15mm')
    if max_floor>.025:warnings.append(name+' floating lowest vertex over 25mm')
    if max_stretch>2:warnings.append(name+' edge stretch over 2x; inspect joints')
    deformations.append({'name':name,'sampleCount':17,'minFloorZ':min_floor,'maxFloorZ':max_floor,'maxEdgeStretch':max_stretch,'maxEdgeGrowthMeters':max_edge_growth,'perMeshMaxStretch':mesh_stretch,'perMeshWorstEdge':mesh_worst,'worstEdge':worst_edge,'samples':frames})

report={'status':'PASS_TECHNICAL' if not errors else 'FAIL','qualityAccepted':False,'shippingAccepted':False,'deformationGate':'PASS' if not warnings else 'REVIEW_REQUIRED',
        'glbSha256':hashlib.sha256(data).hexdigest(),'glbBytes':len(data),'meshes':len(gltf['meshes']),
        'materials':len(gltf['materials']),'skins':len(gltf['skins']),'joints':joint_count,'triangles':triangles,
        'weightedVertices':len(weight_sums),'unweightedVertices':zero,'outOfRangeJoints':bad_joints,
        'maxInfluences':max_influences,'jointWeightedVertexCounts':coverage,'weightSumMaxError':max(abs(s-1) for s in weight_sums),
        'restHeightMeters':rest_height,'clips':serialized,'deformations':deformations,'errors':errors,'warnings':warnings}
(EVIDENCE/'export-audit.json').write_text(json.dumps(report,indent=2))
(OUT/'export-audit.json').write_text(json.dumps(report,indent=2))
# A render from the exported artifact proves evidence isn't only the .blend source.
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=760;scene.render.resolution_y=840;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('QA');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.15,.20,1)
for loc,energy in [((2,-3,4),900),((-3,-1,2),600),((0,3,3),1200)]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=energy;o.data.size=3;o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(3,-5,2));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.9))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.2;scene.camera=cam
rig.animation_data.action=actions['run'];rig.animation_data.action_slot=actions['run'].slots[0]
start,end=actions['run'].frame_range;scene.frame_set(round(start+(end-start)*.25))
scene.render.filepath=str(EVIDENCE/'export-readback-run.png');bpy.ops.render.render(write_still=True)
print(json.dumps({k:v for k,v in report.items() if k not in ('deformations','clips')},indent=2))
if errors:raise RuntimeError('Export audit failed: '+str(errors))
