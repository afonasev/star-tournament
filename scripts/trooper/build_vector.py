"""Original Vector equipment geometry. Blender background; preserves imported rig/clip contracts.
Authored dimensions are mesh content in meters, not runtime balance parameters.
"""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
OUT=ROOT/'.local/vector-source';OUT.mkdir(parents=True,exist_ok=True)

def material(name,color,metal=0,rough=.4):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 return m

def setup():
 global white,navy,rubber,metal,light,dark,parts
 white=material('vector-ceramic',(.72,.79,.83),.3,.29);navy=material('vector-chassis',(.025,.042,.061),.7,.33)
 rubber=material('vector-glove',(.018,.025,.032),.05,.66);metal=material('vector-titanium',(.32,.38,.42),.82,.28)
 light=material('identity-vector-status',(.05,.7,.85),.4,.25);dark=material('vector-bore',(.004,.006,.009),0,.8)
 light.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value=(.01,.5,.8,1)
 light.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=.7
 # Original seamless woven glove map; no external material dependencies.
 image=bpy.data.images.new('vector-woven-glove',width=128,height=128)
 pixels=[]
 for y in range(128):
  for x in range(128):
   value=.028+(.012 if ((x//3+y//3)%2)==0 else 0)+(.012 if x%6==0 or y%6==0 else 0)
   pixels.extend((value,value*1.15,value*1.3,1))
 image.pixels=pixels;image.pack()
 tex=rubber.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
 rubber.node_tree.links.new(tex.outputs['Color'],rubber.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
 parts=[]

def finish(o,name,mat,bevel=0):
 o.name=name;o.data.materials.append(mat)
 if bevel:
  mod=o.modifiers.new('manufactured edge radius','BEVEL');mod.width=bevel;mod.segments=3
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 for face in o.data.polygons:face.use_smooth=True
 mod=o.modifiers.new('weighted panel normals','WEIGHTED_NORMAL');mod.keep_sharp=True
 bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 parts.append(o);return o

def box(name,loc,size,mat,bevel=.01):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return finish(o,name,mat,bevel)

def cyl(name,loc,r,depth,mat,vertices=32):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc,rotation=(math.pi/2,0,0));return finish(bpy.context.object,name,mat,.006)

def ring(name,y,z,outer,inner,length,mat,vertices=32):
 verts=[]
 for yy,r in [(y-length/2,outer),(y+length/2,outer),(y-length/2,inner),(y+length/2,inner)]:
  for i in range(vertices):
   a=2*math.pi*(i+.5)/vertices;verts.append((r*math.cos(a),yy,z+r*math.sin(a)))
 faces=[]
 for a,b in [(0,1),(2,0),(1,3),(3,2)]:
  for i in range(vertices):
   j=(i+1)%vertices;faces.append((a*vertices+i,a*vertices+j,b*vertices+j,b*vertices+i))
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);return finish(o,name,mat,.003)

def panel(name,x,profile,thickness,mat):
 verts=[(xx,y,z) for xx in [x-thickness/2,x+thickness/2] for y,z in profile];n=len(profile)
 faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);return finish(o,name,mat,.009)

def join(objs,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');return o

def weapon():
 parts.clear()
 box('receiver',(0,-.16,.13),(.24,.42,.32),navy,.035)
 for z in [.035,.245]:
  ring('barrel',-.64,z,.093,.069,.76,navy)
  ring('muzzle octagonal shell',-1.005,z,.112,.077,.12,white,8)
  ring('muzzle titanium lip',-1.073,z,.094,.07,.026,metal)
  ring('inner bore rifling collar',-1.01,z,.075,.064,.14,dark)
  cyl('deep bore shadow',(0,-.79,z),.065,.005,dark)
  for y in [-.83,-.60,-.40]:ring('barrel collar',y,z,.1,.088,.022,metal)
 for side in [-1,1]:
  panel('receiver ceramic',side*.137,[(-.43,-.015),(-.39,.23),(-.17,.32),(.08,.30),(.1,.06),(-.1,-.065)],.035,white)
  panel('front vent panel',side*.109,[(-.96,-.055),(-.94,.13),(-.79,.16),(-.48,.12),(-.48,-.035)],.026,white)
  panel('bridge bracket',side*.122,[(-.69,-.095),(-.60,-.095),(-.49,.27),(-.57,.36),(-.65,.36),(-.58,.25)],.024,metal)
  for y in [-.88,-.80,-.72]:
   box('cooling inset',(side*.128,y,.058),(.008,.045,.024),dark,.004)
  panel('raised receiver shoulder',side*.161,[(-.38,.205),(-.24,.30),(.055,.285),(.065,.23),(-.20,.24)],.012,metal)
  panel('lower receiver inset',side*.158,[(-.29,.055),(-.19,.065),(-.06,.04),(-.095,-.025),(-.23,-.01)],.008,navy)
  for y,z in [(-.35,.20),(.015,.20),(-.58,.27),(-.64,-.06)]:
   bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,radius=1,location=(side*.16,y,z));o=bpy.context.object;o.scale=(.005,.012,.012);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);finish(o,'recess fastener',metal)
  box('cyan status inset',(side*.158,-.23,.1),(.007,.12,.019),light,.004)
 for i in range(7):box('top cooling fin',(0,-.57+i*.043,.359),(.18,.018,.035),metal,.005)
 box('rear receiver inset',(0,.061,.155),(.17,.015,.21),metal,.014)
 box('rear recessed field',(0,.073,.155),(.135,.012,.17),navy,.012)
 for i in range(5):box('rear vent',(0,.083,.11+i*.025),(.095,.007,.01),dark,.003)
 box('top spine',(0,-.09,.326),(.10,.29,.026),navy,.007)
 for i in range(5):box('sight rib',(0,-.19+i*.04,.348),(.08,.014,.012),metal,.003)
 grip=box('pistol grip',(0,.025,-.155),(.135,.15,.32),rubber,.035);grip.rotation_euler.x=-.20
 for side in [-1,1]:
  panel('grip rim',side*.07,[(-.05,-.03),(.09,-.04),(.13,-.29),(.04,-.32),(-.035,-.25)],.015,white)
  for i in range(8):box('grip stipple',(side*.08,.033,-.08-i*.025),(.008,.075,.008),navy,.003)
 box('support grip',(0,-.59,-.101),(.18,.36,.095),rubber,.03)
 for i in range(9):box('support grip ribs',(0,-.745+i*.037,-.15),(.175,.012,.015),navy,.005)
 for side in [-1,1]:panel('trigger guard',side*.066,[(-.29,-.02),(-.30,-.16),(-.05,-.20),(-.01,-.16),(-.06,-.135),(-.24,-.12),(-.23,-.025)],.015,metal)
 trig=box('trigger',(0,-.145,-.073),(.025,.025,.1),navy,.006);trig.rotation_euler.x=-.2
 gun=join(parts[:],'weapon:joined');return gun

def attach_skin(o,rig,bone):
 # Geometry is authored directly in rig rest coordinates.
 group=o.vertex_groups.new(name=bone);group.add(list(range(len(o.data.vertices))),1,'REPLACE')
 mod=o.modifiers.new('TrooperHumanoid','ARMATURE');mod.object=rig;o.parent=rig

def segment(name,a,b,width,depth,mat,rig,bone):
 a,b=Vector(a),Vector(b)
 if mat==rubber:
  # Tailored connected sections keep full circumference at joints (no floating capsule ends).
  length=(b-a).length;verts=[];faces=[];radial=20
  rings=[(-.53,.85),(-.48,1),(.40,.91),(.53,.78)]
  for t,r in rings:
   for i in range(radial):
    angle=2*math.pi*i/radial;verts.append((math.cos(angle)*width*r/2,math.sin(angle)*depth*r/2,t*length))
  for j in range(len(rings)-1):
   for i in range(radial):
    k=(i+1)%radial;faces.append((j*radial+i,j*radial+k,(j+1)*radial+k,(j+1)*radial+i))
  faces.extend([tuple(reversed(range(radial))),tuple(range((len(rings)-1)*radial,len(rings)*radial))])
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.location=(a+b)/2;finish(o,name,mat)

 else:o=box(name,(a+b)/2,(width,depth,(b-a).length),mat,min(width,depth)*.3)
 o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference((b-a).normalized())
 bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);attach_skin(o,rig,bone);return o

def hands(rig):
 pieces=[]
 for side in ['Left','Right']:
  bones=rig.data.bones;w=bones[side+'Hand'].head_local;el=bones[side+'LowerArm'].head_local
  shoulder=bones[side+'UpperArm'].head_local
  for i in range(3):
   bn=side+('UpperArm' if i==0 else 'UpperArmTwist'+str(i))
   if bn not in bones:bn=side+'UpperArm'
   pieces.append(segment('continuous upper sleeve',shoulder.lerp(el,i/3),shoulder.lerp(el,(i+1)/3+.025),.10-i*.005,.09-i*.005,rubber,rig,bn))
  for i in range(3):
   a=el.lerp(w,i/3);b=el.lerp(w,(i+1)/3+.02)
   bone=side+('LowerArm' if i==0 else 'LowerArmTwist'+str(i))
   pieces.append(segment('navy forearm sleeve',a,b,.079-i*.01,.07-i*.009,rubber,rig,bone))
   off=Vector((0,-.034,0));pieces.append(segment('ceramic segmented gauntlet',a+off,b+off,.067-i*.009,.017,white,rig,bone))
   pieces.append(segment('titanium gauntlet rail',a+Vector((.025,-.045,0)),b+Vector((.025,-.045,0)),.01,.009,metal,rig,bone))
  proximal=[bones[side+d+'Proximal'].head_local for d in ['Index','Middle','Ring','Little']];end=sum(proximal,Vector())/4
  pieces.append(segment('tailored glove palm',w,end,.082,.038,rubber,rig,side+'Hand'))
  pieces.append(segment('floating backhand armor',w.lerp(end,.23)+Vector((0,-.025,0)),w.lerp(end,.85)+Vector((0,-.025,0)),.07,.014,white,rig,side+'Hand'))
  for digit in ['Thumb','Index','Middle','Ring','Little']:
   for ix,suffix in enumerate(['Proximal','Intermediate','Distal']):
    bn=side+digit+suffix;bone=bones[bn];a=bone.head_local;b=bone.tail_local
    pieces.append(segment('gloved '+bn,a,b,.022 if digit!='Little' else .018,.024,rubber,rig,bn))
    if ix<2:
     off=Vector((0,-.013,0));pieces.append(segment('knuckle armor '+bn,a.lerp(b,.15)+off,a.lerp(b,.7)+off,.018,.008,metal,rig,bn))
 return join(pieces,'vector-armored-hands')

def export(path,objs):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0]
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=True,export_extras=True,export_yup=True,export_skins=True)

for kind in ['body','arms']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 path=ART/('trooper-'+kind+'-animated.glb')
 # Always rebuild from the committed baseline, not a previously generated derivative.
 import subprocess,tempfile
 baseline=subprocess.check_output(['git','show','49878a337e98f999787b530b9a071ce6698f55cb:unity/Assets/StarTournament/Art/'+path.name],cwd=ROOT)
 with tempfile.NamedTemporaryFile(suffix='.glb') as source:
  source.write(baseline);source.flush();bpy.ops.import_scene.gltf(filepath=source.name)
 rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 old=bpy.data.objects['weapon:joined'];parent=old.parent;transform=old.matrix_local.copy()
 bpy.data.objects.remove(old,do_unlink=True);setup();gun=weapon()
 if kind=='body':
  gun.scale=(.32,.32,.32)
  export(ART/'vector-shotgun-lod0.glb',[gun])
  low=gun.copy();low.data=gun.data.copy();bpy.context.collection.objects.link(low);bpy.context.view_layer.objects.active=low
  mod=low.modifiers.new('LOD1 bounded simplification','DECIMATE');mod.ratio=.55;bpy.ops.object.modifier_apply(modifier=mod.name)
  export(ART/'vector-shotgun-lod1.glb',[low]);bpy.data.objects.remove(low,do_unlink=True)
  gun.scale=(1,1,1)
 gun.parent=parent;gun.matrix_local=transform
 for index,z in enumerate([.035,.245]):
  node=bpy.data.objects.new('vector-muzzle-'+str(index),None);bpy.context.collection.objects.link(node);node.parent=gun;node.location=(0,-1.10,z)
 if kind=='arms':
  for o in list(bpy.context.scene.objects):
   if o.type=='MESH' and o.find_armature()==rig:bpy.data.objects.remove(o,do_unlink=True)
  hands(rig)
 # Remove importer-only shape helpers, never export diagnostic geometry.
 keep=[o for o in bpy.context.scene.objects if o==rig or o.parent is not None]
 export(path,keep)
 # Save an inspectable authored Blender source for reproducibility.
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/('vector-'+kind+'.blend')))
manifest_path=ART/'asset-manifest.json';manifest=json.loads(manifest_path.read_text())
for entry in manifest['entries']:
 if entry['key'] in ['world-weapon','view-weapon']:
  entry['path']='Assets/StarTournament/Art/vector-shotgun-lod0.glb';entry['sha256']=hashlib.sha256((ART/'vector-shotgun-lod0.glb').read_bytes()).hexdigest();entry['source']='Original authored Vector; embedded equivalent in trooper derivatives'
 if entry['key'] in ['player-body','player-hands']:
  entry['sha256']=hashlib.sha256((ROOT/'unity'/entry['path']).read_bytes()).hexdigest();entry['source']='Original Vector equipment + existing attributed Trooper rig/clips; build_vector.py'
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
print('VECTOR_EXPORT_COMPLETE')
mount_path=ART/'trooper-mounts.json';mount_data=json.loads(mount_path.read_text())
mount_data['equipment']='vector-equipment-v1; original rig/grip motion with replacement first-person gloves and gauntlets'
for key,file in [('body','trooper-body-animated.glb'),('arms','trooper-arms-animated.glb')]:mount_data[key]['sha256']=hashlib.sha256((ART/file).read_bytes()).hexdigest()
mount_path.write_text(json.dumps(mount_data,indent=2)+'\n')
