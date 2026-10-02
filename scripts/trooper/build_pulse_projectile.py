"""Original hot-impulse projectile, meters +Y up/+Z forward, center pivot; no colliders.
Normalized authored geometry is scaled only by Balance Lab rocketBodyRadius/Length.
Run blender -b -P scripts/trooper/build_pulse_projectile.py.
"""
import bpy, math, json, struct, hashlib
from pathlib import Path
from mathutils import Matrix
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
palette={}
def mat(name,color,metal,rough):
 m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1)
 b=m.node_tree.nodes.get('Principled BSDF') or m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
 output=m.node_tree.nodes.get('Material Output') or m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(b.outputs['BSDF'],output.inputs['Surface'])
 b.inputs['Base Color'].default_value=(*color,1);b.inputs['Metallic'].default_value=metal;b.inputs['Roughness'].default_value=rough
 palette[name]={'baseColorFactor':[*color,1],'metallicFactor':metal,'roughnessFactor':rough};return m
graphite=mat('projectile graphite',(.065,.076,.088),.65,.3)
ceramic=mat('projectile ceramic',(.86,.89,.92),.18,.25)
orange=mat('projectile orange collar',(1,.27,.025),.28,.3)
black=mat('projectile nozzle',(.014,.02,.029),.7,.33)
def lathe(name,rings,material):
 n=32;vertices=[(r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n),z) for z,r in rings for i in range(n)]
 faces=[]
 for k in range(len(rings)-1):
  for i in range(n):j=(i+1)%n;faces.append((k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i))
 faces.extend([tuple(reversed(range(n))),tuple((len(rings)-1)*n+i for i in range(n))])
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
 obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(material)
 for polygon in mesh.polygons:polygon.use_smooth=len(polygon.vertices)==4
lathe('graphite housing',[(-.47,.185),(-.43,.22),(.08,.22),(.12,.205)],graphite)
lathe('ceramic nose',[(.08,.222),(.27,.215),(.43,.14),(.49,.065),(.5,.014)],ceramic)
lathe('orange collar',[(-.30,.223),(-.29,.23),(-.20,.23),(-.19,.223)],orange)
lathe('recessed nozzle',[(-.50,.17),(-.465,.17),(-.465,.135),(-.49,.135)],black)
for angle in range(0,360,90):
 a=math.radians(angle);verts=[(.17,-.027,-.39),(.34,-.027,-.46),(.32,-.027,-.20),(.20,-.027,-.06),(.17,.027,-.39),(.34,.027,-.46),(.32,.027,-.20),(.20,.027,-.06)]
 verts=[(x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a),z) for x,y,z in verts]
 mesh=bpy.data.meshes.new('ceramic fin');mesh.from_pydata(verts,[],[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]);mesh.update()
 o=bpy.data.objects.new('ceramic stabilizer',mesh);bpy.context.collection.objects.link(o);o.data.materials.append(ceramic)
# Join by material so every projectile has four draw parts, shared across instances.
for material in (graphite,ceramic,orange,black):
 objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.data.materials[0]==material]
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=material.name
bpy.ops.object.empty_add(location=(0,0,-.49));bpy.context.object.name='pulse-projectile-nozzle'
axis=Matrix.Rotation(math.pi/2,4,'X')
for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world
path=ART/'pulse-projectile.glb';bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True)
data=path.read_bytes();length=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+length]);tail=data[20+length:]
for m in doc['materials']:m['pbrMetallicRoughness']=palette[m['name']]
payload=json.dumps(doc,separators=(',',':')).encode();payload+=b' '*((-len(payload))%4)
path.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)
manifest=ART/'asset-manifest.json';doc=json.loads(manifest.read_text());doc['entries']=[e for e in doc['entries'] if e['key']!='pulse-projectile']
doc['entries'].append({'key':'pulse-projectile','path':'Assets/StarTournament/Art/pulse-projectile.glb','sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'units':'meters','up':'+Y','front':'+Z','pivot':'center','source':'Original build_pulse_projectile.py; hot impulse concept 1 selected 2026-10-02; no collision proxy, no gameplay LOD. Shared four-material mesh.'})
manifest.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n')
