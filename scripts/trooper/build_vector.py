"""Strata F4 equipment (stable Vector pipeline keys). Blender background; preserves imported rig/clip contracts.
Authored dimensions are mesh content in meters, not runtime balance parameters.
"""
import bpy, bmesh, math, json, hashlib
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
OUT=ROOT/'.local/strata-f5-source';OUT.mkdir(parents=True,exist_ok=True)

def material(name,color,metal=0,rough=.4):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 return m

def setup():
 global white,navy,rubber,metal,light,dark,parts,thread,leather,oxblood,cloth
 white=material('vector-ceramic',(.79,.77,.71),.14,.49);navy=material('vector-chassis',(.025,.029,.034),.72,.34)
 oxblood=material('strata-oxblood-accent',(.12,.013,.025),.26,.52)
 cloth=material('strata-oxblood-fabric',(.07,.009,.016),0,.82)
 rubber=material('vector-glove',(.018,.025,.032),.05,.66);metal=material('vector-titanium',(.32,.38,.42),.82,.28)
 leather=material('orbital-palm-leather',(.038,.041,.045),0,.83)
 thread=material('orbital-seam-thread',(.055,.065,.073),0,.9)
 light=material('identity-vector-status',(.05,.7,.85),.4,.25);dark=material('vector-bore',(.004,.006,.009),0,.8)
 light.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value=(.01,.5,.8,1)
 light.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=.7
 # Packed ceramic albedo retains a quiet machined surface instead of a flat
 # plastic white. These are authored texture marks, not runtime variation.
 import random
 rng=random.Random(2402)
 ceramic=bpy.data.images.new('strata-ceramic-albedo',width=512,height=512)
 pixels=[]
 for y in range(512):
  for x in range(512):
   micro=rng.uniform(-.023,.023)
   scratch=-.18 if (x*13+y*7)%263<2 and rng.random()<.22 else 0
   grime=-.10 if rng.random()<.003 else 0
   value=max(.48,min(.91,.77+micro+scratch+grime))
   pixels.extend((value,value*.965,value*.91,1))
 ceramic.pixels=pixels;ceramic.pack()
 albedo=white.node_tree.nodes.new('ShaderNodeTexImage');albedo.image=ceramic
 white.node_tree.links.new(albedo.outputs['Color'],white.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
 # Original seamless woven glove map; no external material dependencies.
 image=bpy.data.images.new('vector-woven-glove',width=128,height=128)
 pixels=[]
 for y in range(128):
  for x in range(128):
   value=.025+(.0018 if ((x//2+y//2)%2)==0 else 0)+(.002 if x%4==0 or y%4==0 else 0)
   pixels.extend((value,value*1.15,value*1.3,1))
 image.pixels=pixels;image.pack()
 tex=rubber.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
 rubber.node_tree.links.new(tex.outputs['Color'],rubber.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
 # Baked tangent-space woven normal, portable to glTF and Unity (no procedural shader).
 normal_image=bpy.data.images.new('orbital-woven-normal',width=128,height=128);normal_image.colorspace_settings.name='Non-Color';normal_pixels=[]
 for y in range(128):
  for x in range(128):
   dx=.18*math.sin(x*math.tau/4);dy=.18*math.sin(y*math.tau/4);v=Vector((dx,dy,1)).normalized();normal_pixels.extend((v.x*.5+.5,v.y*.5+.5,v.z*.5+.5,1))
 normal_image.pixels=normal_pixels;normal_image.pack();nt=rubber.node_tree.nodes.new('ShaderNodeTexImage');nt.image=normal_image
 nm=rubber.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.4
 rubber.node_tree.links.new(nt.outputs['Color'],nm.inputs['Color']);rubber.node_tree.links.new(nm.outputs['Normal'],rubber.node_tree.nodes.get('Principled BSDF').inputs['Normal'])
 # Ceramic microfinish: low-amplitude tangent-space grain, visible under grazing light.
 grain=bpy.data.images.new('orbital-ceramic-microfinish',width=256,height=256);grain.colorspace_settings.name='Non-Color'
 rng=random.Random(17);pixels=[]
 for y in range(256):
  for x in range(256):
   dx=rng.uniform(-.018,.018);dy=rng.uniform(-.018,.018)
   pixels.extend((.5+dx,.5+dy,1,1))
 grain.pixels=pixels;grain.pack()
 tex=white.node_tree.nodes.new('ShaderNodeTexImage');tex.image=grain
 normal=white.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.28
 white.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color']);white.node_tree.links.new(normal.outputs['Normal'],white.node_tree.nodes.get('Principled BSDF').inputs['Normal'])
 parts=[]

def finish(o,name,mat,bevel=0):
 o.name=name;o.data.materials.append(mat)
 if bevel:
  mod=o.modifiers.new('manufactured edge radius','BEVEL');mod.width=bevel;mod.segments=3
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 for face in o.data.polygons:face.use_smooth=True
 if bevel:
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

def mesh_object(name,verts,faces,mat):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
 # Object-scale planar UVs give the woven surface a consistent physical density.
 uv=mesh.uv_layers.new(name='UVMap')
 for poly in mesh.polygons:
  for li in poly.loop_indices:
   co=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(co.x*12+co.z*2,co.z*12+co.y*3)
 return finish(o,name,mat)

def smooth_outline(points,steps=5):
 out=[];n=len(points)
 for i in range(n):
  a,b,c,d=[Vector(points[j%n]) for j in [i-1,i,i+1,i+2]]
  for k in range(steps):
   t=k/steps;out.append((2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)*.5)
 return out

def sculpt(name,x,profile,depth,mat):
 """Manufactured spline-contour shell. Concave outlines use planar caps, not intersecting radial fans."""
 outline=smooth_outline(profile);n=len(outline);sign=1 if x>=0 else -1
 verts=[(xx,p.x,p.y) for xx in [x-sign*depth*.10,x+sign*depth*.50] for p in outline]
 faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 o=mesh_object(name,verts,faces,mat)
 # Constrained cap tessellation supports concave contour cutaways without radial folds.
 # Shallow camber preserves the sculpted manufactured surface, with narrow edge roll.
 bm=bmesh.new();bm.from_mesh(o.data)
 front=x+sign*depth*.50
 caps=[f for f in bm.faces if all(abs(v.co.x-front)<1e-6 for v in f.verts)]
 bmesh.ops.triangulate(bm,faces=caps)
 edges=[e for e in bm.edges if all(abs(v.co.x-front)<1e-6 for v in e.verts)]
 bmesh.ops.subdivide_edges(bm,edges=edges,cuts=3,use_grid_fill=True)
 for v in bm.verts:
  if abs(v.co.x-front)>1e-6:continue
  q=Vector((v.co.y,v.co.z));distance=1e9
  for a,b in zip(outline,outline[1:]+outline[:1]):
   ab=b-a;t=max(0,min(1,(q-a).dot(ab)/max(ab.length_squared,1e-12)))
   distance=min(distance,(q-a-ab*t).length)
  v.co.x=x+sign*depth*(.43+.10*min(1,distance/.03))
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
 bpy.context.view_layer.objects.active=o
 bevel=o.modifiers.new('controlled shell edge','BEVEL');bevel.width=min(.004,depth*.10);bevel.segments=3;bpy.ops.object.modifier_apply(modifier=bevel.name)
 normal=o.modifiers.new('manufactured surface normals','WEIGHTED_NORMAL');normal.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=normal.name)
 return o

def tube(name,points,r,mat,closed=False):
 curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.bevel_depth=r;curve.bevel_resolution=2;curve.resolution_u=8
 spline=curve.splines.new('POLY');spline.points.add(len(points)-1)
 for p,co in zip(spline.points,points):p.co=(*co,1)
 spline.use_cyclic_u=closed;o=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(o)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');return finish(bpy.context.object,name,mat)

def ellipsoid(name,loc,size,mat):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=8 if max(size)<.006 else 24,ring_count=6 if max(size)<.006 else 16,radius=1,location=loc);o=bpy.context.object;o.scale=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,name,mat)

def axial_shell(name,sections,mat):
 # Rounded loft: y, center z, halfwidth, halfheight; longitudinal tessellation preserves curvature.
 verts=[];count=48
 for y,z,w,h in sections:
  for i in range(count):
   t=2*math.pi*i/count;verts.append((w*math.cos(t),y,z+h*math.sin(t)))
 faces=[]
 for j in range(len(sections)-1):
  for i in range(count):faces.append((j*count+i,j*count+(i+1)%count,(j+1)*count+(i+1)%count,(j+1)*count+i))
 faces.extend([tuple(reversed(range(count))),tuple(range((len(sections)-1)*count,len(sections)*count))])
 return mesh_object(name,verts,faces,mat)

def screw(name,side,y,z,x=.15,r=.011):
 # Circular countersink, metal cap and real slot on the shell's side.
 o=cyl(name+' socket',(side*x,y,z),r*1.4,.005,dark,20);o.rotation_euler=(0,math.pi/2,0)
 o=cyl(name+' head',(side*(x+.003),y,z),r,.006,metal,20);o.rotation_euler=(0,math.pi/2,0)
 box(name+' recess',(side*(x+.007),y,z),(.002,r*1.05,.0025),dark,.001)

def marking(text,loc,size,side):
 curve=bpy.data.curves.new('orbital marking','FONT');curve.body=text;curve.size=size;curve.extrude=.00012;curve.resolution_u=3
 o=bpy.data.objects.new('orbital marking',curve);bpy.context.collection.objects.link(o);o.location=loc
 o.rotation_euler=Matrix(((0,0,side),(side,0,0),(0,1,0))).to_4x4().to_euler()
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');return finish(bpy.context.object,'orbital marking '+text,navy)

def weapon():
 import sys
 sys.path.insert(0,str(ROOT/'scripts/trooper'))
 from strata_weapon import build
 return build(globals())

def attach_skin(o,rig,bone):
 # Geometry is authored directly in rig rest coordinates.
 group=o.vertex_groups.new(name=bone);group.add(list(range(len(o.data.vertices))),1,'REPLACE')
 mod=o.modifiers.new('TrooperHumanoid','ARMATURE');mod.object=rig;o.parent=rig

def body_armor(rig):
 # F4's pale segmented plates are tied to the existing bones. These are
 # presentation surfaces only; collider, skeleton and clips are unchanged.
 for side in ['Left','Right']:
  for plate_index,(region,stations,width,depth) in enumerate([
   ('LowerLeg',[(.12,.67),(.18,.97),(.34,1),(.39,.61)],.165,.205),
   ('LowerLeg',[(.45,.67),(.50,1),(.75,.92),(.82,.60)],.158,.195),
   ('UpperLeg',[(.22,.48),(.28,.78),(.42,.84),(.48,.54)],.19,.22),
   ('UpperArm',[(.05,.57),(.13,.90),(.53,.88),(.61,.58)],.16,.185)]):
   name=side+region;bone=rig.data.bones[name]
   a,b=bone.head_local,bone.tail_local;axis=(b-a).normalized()
   front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized()
   lateral=axis.cross(front).normalized();verts=[];faces=[];count=14
   for layer in [1,0]:
    for t,span in stations:
     for i in range(count):
      theta=2.35*(i/(count-1)-.5)*span
      v=a.lerp(b,t)+lateral*(math.sin(theta)*width/2)+front*(math.cos(theta)*depth/2+(.007 if layer else 0))
      verts.append(v)
   ringcount=len(stations)*count
   for layer in range(2):
    for j in range(len(stations)-1):
     for i in range(count-1):
      q=layer*ringcount+j*count+i
      f=(q,q+1,q+count+1,q+count)
      faces.append(f if layer==0 else tuple(reversed(f)))
   for j in range(len(stations)-1):
    for edge in [0,count-1]:
     q=j*count+edge;faces.append((q,q+count,q+ringcount+count,q+ringcount))
   for i in range(count-1):
    for edge in [0,(len(stations)-1)*count]:
     q=edge+i;faces.append((q,q+1,q+ringcount+1,q+ringcount))
   plate=mesh_object('Strata ceramic '+name+' '+str(plate_index),verts,faces,white)
   attach_skin(plate,rig,name)
   if region=='UpperArm':
    tailored('Strata oxblood '+name+' sleeve',a,b,.15,.15,cloth,rig,name,
             [(.03,.73),(.11,.96),(.48,1),(.82,.94),(.96,.72)])

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

def tailored(name,a,b,width,depth,mat,rig,bone,profile=None):
 a,b=Vector(a),Vector(b);length=(b-a).length;radial=24
 profile=profile or [(-.09,.72),(-.04,.95),(.12,1),(.35,.99),(.64,.92),(.88,.84),(1.02,.65),(1.07,.22)]
 rotation=Vector((0,0,1)).rotation_difference((b-a).normalized());verts=[]
 for t,r in profile:
  for i in range(radial):
   angle=math.tau*i/radial;v=rotation@Vector((math.cos(angle)*width*r/2,math.sin(angle)*depth*r/2,t*length));verts.append(a+v)
 faces=[]
 for j in range(len(profile)-1):
  for i in range(radial):faces.append((j*radial+i,j*radial+(i+1)%radial,(j+1)*radial+(i+1)%radial,(j+1)*radial+i))
 faces.extend([tuple(reversed(range(radial))),tuple(range((len(profile)-1)*radial,len(profile)*radial))])
 o=mesh_object(name,verts,faces,mat);attach_skin(o,rig,bone);return o

def skin_detail(o,rig,bone):
 bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);attach_skin(o,rig,bone);return o

def hands(rig):
 import sys
 sys.path.insert(0,str(ROOT/'scripts/trooper'))
 from orbital_hands import build
 return build(globals(),rig)

def export(path,objs):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0]
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=True,export_extras=True,export_yup=True,export_skins=True)

for kind in ['body','arms']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 path=ART/('trooper-'+kind+'-animated.glb')
 # The body retains the canonical rig baseline. The user selected the exact
 # accepted F3/F2 view hands for FPS; import that immutable derivative instead.
 import subprocess,tempfile
 revision='bd1e06d79d5c0ae2af9f549b9e3ba6eead611200' if kind=='arms' else '49878a337e98f999787b530b9a071ce6698f55cb'
 baseline=subprocess.check_output(['git','show',revision+':unity/Assets/StarTournament/Art/'+path.name],cwd=ROOT)
 with tempfile.NamedTemporaryFile(suffix='.glb') as source:
  source.write(baseline);source.flush();bpy.ops.import_scene.gltf(filepath=source.name)
 rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 import sys
 sys.path.insert(0,str(ROOT/'scripts/trooper'))
 from strata_palette import apply as apply_strata_palette
 if kind=='body':apply_strata_palette()
 old=bpy.data.objects['weapon:joined'];parent=old.parent;transform=old.matrix_local.copy()
 for index in range(2):
  old_muzzle=bpy.data.objects.get('vector-muzzle-'+str(index))
  if old_muzzle:bpy.data.objects.remove(old_muzzle,do_unlink=True)
 bpy.data.objects.remove(old,do_unlink=True);setup();gun=weapon()
 if kind=='body':
  gun.scale=(.32,.32,.32)
  export(ART/'vector-shotgun-lod0.glb',[gun])
  low=gun.copy();low.data=gun.data.copy();bpy.context.collection.objects.link(low);bpy.context.view_layer.objects.active=low
  mod=low.modifiers.new('LOD1 bounded simplification','DECIMATE');mod.ratio=.55;bpy.ops.object.modifier_apply(modifier=mod.name)
  export(ART/'vector-shotgun-lod1.glb',[low]);bpy.data.objects.remove(low,do_unlink=True)
  gun.scale=(1,1,1)
 gun.parent=parent;gun.matrix_local=transform
 if kind=='body':body_armor(rig)
 for index,z in enumerate([.035,.245]):
  node=bpy.data.objects.new('vector-muzzle-'+str(index),None);bpy.context.collection.objects.link(node);node.parent=gun;node.location=(0,-1.18,z+.105)
 # The F3/F2 arms already contain the approved glove mesh, armor and weights.
 # Do not rebuild or recolor them while replacing only the embedded weapon.
 # Remove importer-only shape helpers, never export diagnostic geometry.
 keep=[o for o in bpy.context.scene.objects if o==rig or o.parent is not None]
 export(path,keep)
 # Save an inspectable authored Blender source for reproducibility.
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/('strata-'+kind+'.blend')))
manifest_path=ART/'asset-manifest.json';manifest=json.loads(manifest_path.read_text())
for entry in manifest['entries']:
 if entry['key'] in ['world-weapon','view-weapon']:
  entry['sha256']=hashlib.sha256((ART/'vector-shotgun-lod0.glb').read_bytes()).hexdigest();entry['source']='Strata F5 with fitted primary palm grip; same authored model in world and view'
 if entry['key']=='player-body':
  entry['sha256']=hashlib.sha256((ROOT/'unity'/entry['path']).read_bytes()).hexdigest();entry['source']='Strata F4 oxblood world body with fitted primary palm grip; build_vector.py and strata_palette.py'
 if entry['key']=='player-hands':
  entry['sha256']=hashlib.sha256((ROOT/'unity'/entry['path']).read_bytes()).hexdigest();entry['source']='Selected Orbital F3/F2 FPS hands and Strata F5 weapon over attributed ART_LOLL glove topology/UV and Trooper rig/clips; build_vector.py'
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
print('VECTOR_EXPORT_COMPLETE')
mount_path=ART/'trooper-mounts.json';mount_data=json.loads(mount_path.read_text())
mount_data['equipment']='strata-oxblood-equipment-v2-f5; Strata weapon with Orbital F3/F2 FPS hands; right grip corrected; attributed gloves and clips preserved'
for key,file in [('body','trooper-body-animated.glb'),('arms','trooper-arms-animated.glb')]:mount_data[key]['sha256']=hashlib.sha256((ART/file).read_bytes()).hexdigest()
mount_path.write_text(json.dumps(mount_data,indent=2)+'\n')
