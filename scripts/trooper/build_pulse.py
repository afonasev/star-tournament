"""Original Pulse concept adaptation, renderer-only GLB; meters, +Y up/+Z muzzle, grip origin.
Run blender --background --python scripts/trooper/build_pulse.py. No collision proxies or rig changes.
"""
import bpy, math, json, struct, hashlib
from pathlib import Path
from mathutils import Matrix
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
palette={}

def mat(name,color,metal=.5,rough=.34):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
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

def tube(name,z,length,outer,inner,material,n):
 # Hollow cylinder with authored Y-up axis convention; keep the dark bore visibly open.
 v=[]
 for depth,r in ((z-length/2,outer),(z+length/2,outer),(z-length/2,inner),(z+length/2,inner)):
  for i in range(n):a=i*math.tau/n;v.append((math.cos(a)*r,.08+math.sin(a)*r,depth))
 faces=[]
 for i in range(n):j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(j,i,2*n+i,2*n+j),(n+i,n+j,3*n+j,3*n+i)])
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 return o

def make(lod):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 dark=mat('pulse graphite',(.075,.083,.088),.78);white=mat('pulse white ceramic',(.82,.85,.86),.32);orange=mat('pulse orange rings',(1,.26,.025),.45);black=mat('pulse recessed black',(.008,.013,.018),.35);steel=mat('pulse bolts',(.34,.42,.47),.85);cyan=mat('pulse cyan strips',(.02,.72,.85),.15)
 n=48 if lod==0 else 24
 tube('graphite launch tube',.13,.86,.105,.079,dark,n)
 tube('front muzzle rim',.57,.055,.109,.079,dark,n)
 for z in (.49,.17,-.20):tube('orange pulse ring',z,.045,.121,.102,orange,n)
 for side in (-1,1):
  box('white rear side shell',(side*.113,.079,-.205),(.032,.14,.20),white)
  box('white middle protective rib',(side*.111,.08,.12),(.028,.11,.27),white)
  box('white front protective rib',(side*.086,.165,.395),(.055,.028,.19),white)
  box('black strip recess',(side*.128,.09,.13),(.006,.024,.15),black)
  box('cyan side indicator',(side*.132,.09,.13),(.006,.011,.13),cyan)
  for z in (-.26,-.15):box('rear collar fastener',(side*.135,.03,z),(.01,.013,.013),steel)
 box('upper rear sight housing',(0,.212,-.24),(.14,.075,.17),white)
 box('open sight recessed front',(0,.22,-.146),(.07,.03,.008),black)
 box('grip receiver',(0,-.028,.015),(.075,.09,.15),dark)
 grip=box('primary grip',(0,-.126,-.015),(.056,.15,.07),black);grip.rotation_euler[0]=-.18
 box('primary grip heel',(0,-.205,-.022),(.07,.024,.08),white)
 box('support vertical grip',(0,-.11,.35),(.048,.155,.063),black)
 box('support collar',(0,-.022,.35),(.075,.025,.075),white)
 box('support heel',(0,-.192,.35),(.062,.019,.073),dark)
 box('trigger guard lower',(0,-.125,.063),(.06,.017,.09),dark)
 box('trigger guard front',(0,-.092,.105),(.057,.07,.015),dark)
 box('orange trigger',(0,-.077,.052),(.014,.045,.016),orange)
 if lod==0:
  for z in (-.09,.015,.28,.38):
   tube('machined tube seam',z,.007,.108,.103,black,n)
  for side in (-1,1):
   for i in range(4):box('collar vent',(side*.124,.082,.48+(i-1.5)*.009),(.006,.031,.004),black)
   box('rear cyan inset',(side*.132,.094,-.225),(.008,.056,.012),cyan)
 # Rigid weapon parts share one mount. Join by authored material to keep six renderers per LOD
 # while retaining the detailed silhouette and explicit semantic attachment transforms.
 groups={}
 for obj in list(bpy.context.scene.objects):
  if obj.type=='MESH':groups.setdefault(obj.data.materials[0].name,[]).append(obj)
 for name,objects in groups.items():
  bpy.ops.object.select_all(action='DESELECT')
  for obj in objects:obj.select_set(True)
  bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=name+' geometry'
 bpy.ops.object.empty_add(location=(0,-.09,0));bpy.context.object.name='pulse-grip'
 bpy.ops.object.empty_add(location=(0,.08,.601));bpy.context.object.name='pulse-muzzle'
 bpy.ops.object.empty_add(location=(0,-.1,.35));bpy.context.object.name='pulse-support-grip'
 axis=Matrix.Rotation(math.pi/2,4,'X')
 for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world
 path=ART/f'pulse-launcher-lod{lod}.glb'
 bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True)
 preserve_pbr(path)

ART.mkdir(parents=True,exist_ok=True)
for lod in (0,1):make(lod)
manifest=ART/'asset-manifest.json';doc=json.loads(manifest.read_text())
for entry in doc['entries']:
 if entry['key'] in ('pulse-lod0','pulse-lod1'):entry['sha256']=hashlib.sha256((ROOT/'unity'/entry['path']).read_bytes()).hexdigest()
manifest.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n')
