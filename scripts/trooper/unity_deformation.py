"""Same 2x diagnostic edge-stretch threshold as v2, after actual shipping GLB import."""
import bpy,json,math,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(ROOT/'unity/Assets/StarTournament/Art/trooper-body-animated.glb'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.find_armature()==rig]
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for pb in rig.pose.bones:pb.matrix_basis.identity()
bpy.context.view_layer.update()
def coords(o):
 return np.array([tuple(o.matrix_world@v.co) for v in o.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices])
rest=[coords(o) for o in meshes];edges=[np.array([tuple(e.vertices) for e in o.data.edges]) for o in meshes]
lengths=[np.linalg.norm(v[e[:,0]]-v[e[:,1]],axis=1) for v,e in zip(rest,edges)]
report=[]
for name in ['idle','walk','run','aim','fire','hit','death']:
 action=bpy.data.actions[name];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];a,b=action.frame_range;maximum=0;worst=None
 for k in range(17):
  frame=a+(b-a)*k/16;bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
  for o,e,l in zip(meshes,edges,lengths):
   v=coords(o);assert np.isfinite(v).all();ratios=np.linalg.norm(v[e[:,0]]-v[e[:,1]],axis=1)/np.maximum(l,1e-6);ratios[l<1e-6]=0
   i=int(np.argmax(ratios))
   if ratios[i]>maximum:maximum=float(ratios[i]);worst={'mesh':o.name,'edge':e[i].tolist(),'phase':k/16}
 report.append({'clip':name,'samples':17,'maxEdgeStretch':maximum,'worst':worst})
output={'status':'PASS_DIAGNOSTIC' if all(x['maxEdgeStretch']<=2 for x in report) else 'WARN_DIAGNOSTIC','thresholdUnchanged':2,'poses':119,'artisticAcceptance':False,'clips':report}
(ROOT/'docs/evidence/unity-trooper-2026-09-19/asset-deformation.json').write_text(json.dumps(output,indent=2)+'\n');print(json.dumps(output))

assert output['status']=='PASS_DIAGNOSTIC', 'Shipping deformation exceeds unchanged candidate threshold'
