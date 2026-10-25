"""Original FORGE concept 01 adaptation, renderer-only GLB; meters, +Y up/+Z muzzle, grip origin.
Run blender --background --python scripts/trooper/build_forge.py. No collision proxies or rig changes.
"""
import bpy, math, json, struct, hashlib
from pathlib import Path
from mathutils import Matrix
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
palette={}

def mat(name,color,metal=.5,rough=.34):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;m.node_tree.nodes.clear()
 b=m.node_tree.nodes.get('Principled BSDF') or m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
 output=m.node_tree.nodes.get('Material Output') or m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(b.outputs['BSDF'],output.inputs['Surface'])
 b.inputs['Base Color'].default_value=(*color,1);b.inputs['Metallic'].default_value=metal;b.inputs['Roughness'].default_value=rough
 palette[m.name]={'baseColorFactor':[*color,1],'metallicFactor':metal,'roughnessFactor':rough}
 return m

def preserve_pbr(path):
 # Blender 5.2's exporter can drop custom Principled output factors. Read back the actual GLB,
 # write the authored palette into its standard glTF PBR contract, and retain all geometry.
 data=path.read_bytes();length=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+length]);tail=data[20+length:]
 for material in doc['materials']:material['pbrMetallicRoughness']=palette[material['name']]
 payload=json.dumps(doc,separators=(',',':')).encode();payload+=b' '*((-len(payload))%4)
 path.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)

def box(name,pos,size,material):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material)
 mod=o.modifiers.new('machined edges','BEVEL');mod.width=.004;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
 return o

def plate(name,pos,size,material,axis='x',cut=.035):
 # Eight-sided armor panel: broad chamfers retain the industrial FORGE silhouette.
 a,b=(1,2) if axis=='x' else (0,1);depth=0 if axis=='x' else 2
 w,h=size[a],size[b];c=min(cut,w*.3,h*.3)
 outline=[(-w/2+c,-h/2),(w/2-c,-h/2),(w/2,-h/2+c),(w/2,h/2-c),(w/2-c,h/2),(-w/2+c,h/2),(-w/2,h/2-c),(-w/2,-h/2+c)]
 vertices=[]
 for d in (-size[depth]/2,size[depth]/2):
  for u,v in outline:
   point=list(pos);point[depth]+=d;point[a]+=u;point[b]+=v;vertices.append(point)
 faces=[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 return o

def tube(name,z,length,outer,inner,material,n,y=.08,x=0):
 # Hollow cylinder with authored Y-up axis convention; keep the dark bore visibly open.
 v=[]
 for depth,r in ((z-length/2,outer),(z+length/2,outer),(z-length/2,inner),(z+length/2,inner)):
  for i in range(n):a=i*math.tau/n;v.append((x+math.cos(a)*r,y+math.sin(a)*r,depth))
 faces=[]
 for i in range(n):j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(j,i,2*n+i,2*n+j),(n+i,n+j,3*n+j,3*n+i)])
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 return o


def preview(name):
 from mathutils import Vector
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.seed=7
 scene.render.resolution_x=1200;scene.render.resolution_y=800;scene.render.resolution_percentage=100
 scene.world=bpy.data.worlds.new('FORGE studio');scene.world.use_nodes=True
 bg=scene.world.node_tree.nodes.new('ShaderNodeBackground');wo=scene.world.node_tree.nodes.new('ShaderNodeOutputWorld');scene.world.node_tree.links.new(bg.outputs[0],wo.inputs[0])
 bg.inputs[0].default_value=(.07,.09,.12,1);bg.inputs[1].default_value=.5
 # Temporary 90 degree basis rotation matches the exported upright model.
 meshes=list(scene.objects);axis=Matrix.Rotation(math.pi/2,4,'X')
 for o in meshes:o.matrix_world=axis@o.matrix_world
 center=Vector((0,-.20,.12));bpy.ops.object.camera_add(location=center+Vector((1.4,-1.5,.85)))
 cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.35;scene.camera=cam
 temps=[cam]
 for off,power,size in [((1,-1,2),170,1.5),((-1,-.5,.7),100,1),((.3,1,1),150,1)]:
  bpy.ops.object.light_add(type='AREA',location=center+Vector(off));light=bpy.context.object;temps.append(light);light.data.energy=power;light.data.size=size;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 out=ROOT/'.local/forge';out.mkdir(parents=True,exist_ok=True);scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
 for o in temps:bpy.data.objects.remove(o,do_unlink=True)
 for o in meshes:o.matrix_world=axis.inverted()@o.matrix_world
metrics=[]
def make(lod):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 dark=mat('forge graphite',(.045,.057,.063),.75,.4);white=mat('forge ceramic',(.74,.77,.73),.35,.34)
 black=mat('forge bore rubber',(.006,.011,.009),.15,.58);steel=mat('forge steel',(.26,.31,.29),.85,.28)
 green=mat('forge plasma green',(.075,.9,.008),.2,.22)
 shader=next(n for n in green.node_tree.nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Emission Color'].default_value=(.06,1,.005,1);shader.inputs['Emission Strength'].default_value=2
 n=40 if lod==0 else 20
 plate('closed rear receiver',(0,.10,-.24),(.23,.27,.20),dark,axis='z')
 plate('rear ceramic cap',(0,.10,-.35),(.235,.26,.035),white,axis='z')
 plate('rear dark inset',(0,.10,-.372),(.13,.21,.012),dark,axis='z',cut=.025)
 box('rear power slit',(0,.10,-.382),(.022,.14,.008),green)
 box('upper armored spine',(0,.29,.06),(.17,.055,.70),white)
 box('lower chassis',(0,-.09,.08),(.30,.06,.68),dark)
 for y in (.035,.19):
  tube('energy capsule',.05,.36,.064,0,green,n,y)
  for z in (-.15,.25):tube('capsule steel clamp',z,.045,.077,.061,steel,n,y)
  for x in (-.078,.078):box('capsule guard',(x,y,.05),(.012,.018,.32),steel)
 for side in (-1,1):
  plate('rear ceramic flank',(side*.13,.1,-.20),(.045,.29,.17),white)
  plate('front ceramic flank',(side*.168,.1,.30),(.055,.34,.16),white)
  box('capsule dividing rib',(side*.122,.112,.05),(.026,.032,.38),dark)
  box('upper green inset',(side*.155,.215,-.2),(.006,.014,.10),green)
  for z in (.35,.38,.41):box('front cooling rib',(side*.153,.1,z),(.045,.26,.012),steel)
 tube('broad armored emitter',.47,.12,.198,.139,white,n,.1)
 tube('outer muzzle steel',.54,.035,.192,.14,dark,n,.1)
 tube('recessed green emitter ring',.49,.018,.145,.115,green,n,.1)
 tube('deep muzzle bore',.44,.08,.117,0,black,n,.1)
 tube('energy focus',.485,.012,.065,0,green,n,.1)
 box('primary grip collar',(0,-.125,-.15),(.085,.085,.12),dark)
 o=box('primary rubber grip',(0,-.235,-.19),(.065,.19,.09),black);o.rotation_euler[0]=-.22
 box('grip heel',(0,-.335,-.215),(.09,.025,.11),white)
 box('trigger guard base',(0,-.225,-.065),(.065,.02,.14),steel)
 box('trigger guard front',(0,-.17,.0),(.065,.11,.02),dark)
 box('trigger',(0,-.165,-.09),(.018,.056,.02),steel)
 box('support grip',(0,-.17,.27),(.085,.12,.14),black)
 box('support heel',(0,-.235,.27),(.12,.025,.16),steel)
 if lod==0:
  for side in (-1,1):
   for y in (-.01,.21):
    for z in (-.25,.3):box('shell bolt',(side*.2,y,z),(.012,.018,.018),steel)
   for z in (-.27,-.235,-.2):box('rear cooling slot',(side*.154,.1,z),(.006,.11,.012),black)
  for z in (-.18,-.06,.06,.18):box('top armored seam',(0,.319,z),(.14,.004,.008),dark)
 # Align the primary grip with the existing shared hand mount.
 for obj in bpy.context.scene.objects:obj.location.y+=.105;obj.location.z+=.19
 bpy.context.view_layer.update()
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 triangles=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
 before=len(meshes)
 if lod==0:preview('asset-before-material-join')
 # Rigid groups are merged only by material, preserving vertices, normals and silhouette.
 groups={}
 for o in meshes:groups.setdefault(o.data.materials[0].name,[]).append(o)
 for name,objects in groups.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in objects:o.select_set(True)
  bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=name+' geometry'
 if lod==0:preview('asset-after-material-join')
 bpy.ops.object.empty_add(location=(0,-.09,0));bpy.context.object.name='forge-grip'
 bpy.ops.object.empty_add(location=(0,.205,.75));bpy.context.object.name='forge-muzzle'
 bpy.ops.object.empty_add(location=(0,-.065,.46));bpy.context.object.name='forge-support-grip'
 axis=Matrix.Rotation(math.pi/2,4,'X')
 for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world
 path=ART/f'forge-plasma-lod{lod}.glb'
 bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True)
 preserve_pbr(path)
 metrics.append({'lod':lod,'triangles':triangles,'renderers_before':before,'renderers_after':len(groups),'materials':len(groups),'textures':0,'geometry_preserved_by_material_join':True})
 if lod==0:
  out=ROOT/'assets/source/forge';out.mkdir(parents=True,exist_ok=True)
  bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge.blend'))
for lod in (0,1):make(lod)
manifest=ART/'asset-manifest.json';doc=json.loads(manifest.read_text())
doc['entries']=[e for e in doc['entries'] if not e['key'].startswith('forge-lod')]
for lod in (0,1):
 path=ART/f'forge-plasma-lod{lod}.glb';doc['entries'].append({'key':f'forge-lod{lod}','path':str(path.relative_to(ROOT/'unity')),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'units':'meters','up':'+Y','forward':'+Z','pivot':'forge-grip','source':'Original FORGE concept 01; scripts/trooper/build_forge.py; rigid material-merged mesh; no collision proxies.','lod':lod})
manifest.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n')
report=ROOT/'.local/forge/asset-metrics.json';report.parent.mkdir(parents=True,exist_ok=True);report.write_text(json.dumps(metrics,indent=2)+'\n')
