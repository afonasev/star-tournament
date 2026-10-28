"""Detailed Vector/Petrol; Blender -b -noaudio --python author.py.
Offline mesh dimensions in original equipment coordinates (Z up, -Y forward).
Fits contact surfaces to the unchanged native hand pose. No decimation; source stays editable.
"""
import bpy, bmesh, math, json, sys, subprocess, random
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'; OUT=ROOT/'.local/vector-shotgun';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
baseline=OUT/'original-contact-source.glb'
baseline.write_bytes(subprocess.check_output(['git','show','efc73a7a5ade08f161022f3b3afb2b7dd89c6b1e:unity/Assets/StarTournament/Art/vector-shotgun-lod0.glb'],cwd=ROOT))
bpy.ops.import_scene.gltf(filepath=str(baseline))
old=next(o for o in bpy.context.scene.objects if o.type=='MESH')
old.scale=(1,1,1);old.rotation_euler=(0,0,0);old.location=(0,0,0)
materials={m.name:m for m in old.data.materials}
parts=[]
def mat(name,color,metal=.6,rough=.36,emit=0,source=None):
 m=(source.copy() if source else bpy.data.materials.new(name));m.name=name;m.use_nodes=True;m.diffuse_color=(*color,1)
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 if source:
  # Retain exact existing texture image; color via a baked factor at export.
  for link in list(m.node_tree.links):
   if link.to_socket==bs.inputs['Base Color']:m.node_tree.links.remove(link)
  tex=next((n for n in m.node_tree.nodes if n.type=='TEX_IMAGE'),None)
  if tex:
   mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(*color,1)
   m.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
 if emit:bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=emit
 return m
chassis=materials['vector-chassis'];steel=materials['vector-titanium'];rubber=materials['vector-glove'];dark=materials['vector-bore']
petrol=mat('vector-petrol-enamel',(.048,.125,.139),.48,.4,source=chassis)
edge=mat('vector-brushed-edge',(.32,.37,.4),.86,.3,source=steel)
status=mat('vector-amber-inset',(.95,.29,.015),.15,.24,2.5)
rng=random.Random(261027)
finishmap=bpy.data.images.new('Vector micrograin roughness',width=512,height=512);pixels=[]
for y in range(512):
 for x in range(512):
  value=.38+rng.uniform(-.06,.06)
  if x%89==0 and y%37<11:value=.62
  pixels.extend((value,value,value,1))
finishmap.pixels=pixels;finishmap.pack();finishmap.colorspace_settings.name='Non-Color'
for m in (petrol,edge):
 node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=finishmap;m.node_tree.links.new(node.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'])
# Contact surfaces are fitted to the unchanged native F17 hand pose.
# Gloves, skeleton and animation remain in the character GLB, never copied here.
bpy.data.objects.remove(old,do_unlink=True)
def finish(o,name,m,bevel=.004):
 o.name=name;bpy.context.view_layer.objects.active=o
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.data.materials.append(m)
 # Closed authored solids must render from outside under Unity back-face culling.
 bm=bmesh.new();bm.from_mesh(o.data)
 if all(e.is_manifold for e in bm.edges) and bm.calc_volume(signed=True)<0:
  bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(o.data)
 bm.free()
 if not o.data.uv_layers:
  layer=o.data.uv_layers.new(name='Vector authored planar UV')
  for face in o.data.polygons:
   axis=max(range(3),key=lambda i:abs(face.normal[i]));axes=[i for i in range(3) if i!=axis]
   for loop in face.loop_indices:
    co=o.data.vertices[o.data.loops[loop].vertex_index].co;layer.data[loop].uv=(co[axes[0]]*3,co[axes[1]]*3)
 for face in o.data.polygons:face.use_smooth=True
 if bevel:
  mod=o.modifiers.new('Machined edge radius','BEVEL');mod.width=bevel;mod.segments=4
  mod=o.modifiers.new('Weighted planar normals','WEIGHTED_NORMAL');mod.keep_sharp=True
 parts.append(o);return o
def box(name,loc,size,m,bevel=.004):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size;return finish(o,name,m,bevel)
def panel(name,x,profile,depth,m,bevel=.006):
 n=len(profile);v=[(x+s*depth/2,y,z) for s in (-1,1) for y,z in profile];f=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],f);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);return finish(o,name,m,bevel)
def rear(name,loc,size,m,cut):
 # Eight broad planar faces follow the approved faceted rear rather than a rounded box.
 x,y,z=loc;w,d,h=size;w/=2;h/=2
 outline=[(-w+cut,-h),(w-cut,-h),(w,-h+cut),(w,h-cut),(w-cut,h),(-w+cut,h),(-w,h-cut),(-w,-h+cut)]
 v=[(x+xx,y+dy,z+zz) for dy in (-d/2,d/2) for xx,zz in outline]
 f=[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return finish(o,name,m,.0015)
def cylinder(name,loc,r,depth,m,axis='Y',vertices=48):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc);o=bpy.context.object
 if axis=='Y':o.rotation_euler.x=math.pi/2
 if axis=='X':o.rotation_euler.y=math.pi/2
 return finish(o,name,m,.0015)
def ring(name,y,z,outer,inner,length,m,n=64):
 v=[]
 for yy,r in [(y-length/2,outer),(y+length/2,outer),(y-length/2,inner),(y+length/2,inner)]:
  v += [(math.cos(i*math.tau/n)*r,yy,z+math.sin(i*math.tau/n)*r) for i in range(n)]
 f=[]
 for i in range(n):
  j=(i+1)%n;f.extend([(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(i,2*n+i,2*n+j,j),(n+j,3*n+j,3*n+i,n+i)])
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return finish(o,name,m,.0007)
def bolt(side,y,z):
 cylinder('Recessed screw socket',(side*.126,y,z),.008,.004,dark,'X',24)
 cylinder('Titanium hex fastener',(side*.129,y,z),.0058,.005,steel,'X',6)
 box('Fastener drive',(side*.132,y,z),(.001,.006,.0016),dark,.0003)
# Long continuous closed structure. Main silhouette follows the approved FPS crop.
box('Vector main chassis',(0,-.49,.225),(.196,1.24,.397),chassis,.018)
box('Lower continuous fore-end',(0,-.61,.012),(.163,.76,.12),chassis,.016)
# Twin muzzles retain the exact effect mounts at y=-1.18, z=.14/.35.
for i,z in enumerate((.14,.35)):
 # Real circular opening within an octagonal collar, never a black disk on a cube.
 ring('Twin octagonal nose '+str(i),-1.158,z,.112,.059,.085,steel,8)
 ring('Inner machined muzzle '+str(i),-1.191,z,.074,.052,.023,edge)
 ring('Deep bore liner '+str(i),-1.14,z,.054,.049,.11,dark)
 cylinder('Deep bore terminus '+str(i),(0,-1.082,z),.049,.003,dark)
 # Fine recessed axial grooves; actual geometry, not a painted imitation.
 for a in range(12):
  angle=a*math.tau/12
  cylinder('Bore flute '+str(i),(math.cos(angle)*.050,-1.147,z+math.sin(angle)*.050),.0013,.073,steel,vertices=8)
# Large coherent angled side panels; restrained layering rather than small cubes.
for side in (-1,1):
 panel('Machined upper receiver surround',side*.105,[(-1.085,.438),(-1.00,.473),(-.74,.473),(-.68,.431),(-.25,.429),(-.10,.402),(.122,.401),(.152,.343),(.13,.285),(-.16,.287),(-.36,.342),(-1.085,.343)],.023,edge)
 panel('Petrol upper continuous inlay',side*.12,[(-1.045,.424),(-.993,.451),(-.765,.451),(-.706,.410),(-.26,.407),(-.11,.381),(.111,.380),(.127,.346),(.11,.311),(-.15,.31),(-.36,.365),(-1.045,.365)],.009,petrol,.003)
 panel('Lower angular receiver plate',side*.107,[(-1.075,.284),(-.84,.301),(-.57,.294),(-.45,.252),(-.23,.244),(-.15,.208),(.115,.208),(.14,.155),(.10,.015),(-.07,-.016),(-.29,.047),(-.39,.11),(-.93,.11),(-1.075,.169)],.025,edge)
 panel('Petrol lower enamel inset',side*.123,[(-1.049,.267),(-.84,.280),(-.58,.274),(-.46,.231),(-.24,.225),(-.16,.189),(.09,.189),(.117,.153),(.08,.034),(-.067,.005),(-.274,.067),(-.373,.129),(-.92,.129),(-1.049,.181)],.01,petrol,.003)
 # Uninterrupted dark center seam, a recessed amber status window.
 panel('Recessed status pocket',side*.131,[(-.435,.233),(-.48,.215),(-.48,.171),(-.436,.150),(-.26,.150),(-.23,.176),(-.23,.226)],.008,chassis,.002)
 box('Amber glass sunk in pocket',(side*.137,-.356,.191),(.005,.15,.018),status,.002)
 box('Glass top metal eyebrow',(side*.14,-.356,.212),(.005,.175,.008),steel,.001)
 for n in range(7):
  y=-.97+n*.043
  box('Recessed forward vent',(side*.131,y,.321),(.005,.026,.014),dark,.002)
  box('Vent rib machined edge',(side*.133,y+.011,.321),(.003,.0025,.011),steel,.0006)
 for yz in [(-1.02,.39),(-.79,.425),(-.58,.39),(-.26,.365),(.075,.35),(-.94,.22),(-.58,.235),(-.17,.16),(.064,.08)]:bolt(side,*yz)
 # Short inset service plate, small witness marks.
 box('Service access inset',(side*.131,-.65,.205),(.005,.15,.032),chassis,.003)
 for k in range(3):box('Etched service line',(side*.135,-.70+k*.028,.208),(.001,.018,.0015),steel,.0002)
# Closed rear cap: the dominant plane in first person, angular nested frame.
rear('Faceted closed receiver rear',(0,.158,.225),(.235,.092,.405),steel,.033)
rear('Rear cap recessed gasket',(0,.208,.225),(.201,.014,.365),dark,.028)
rear('Rear cap graphite inset',(0,.217,.245),(.172,.013,.296),chassis,.025)
rear('Rear cap raised central face',(0,.227,.261),(.14,.010,.223),chassis,.022)
# Chevron witness on closed rear face; not a bore.
for angle in (-.50,.50):
 o=box('Rear engraved chevron',(angle*.016,.234,.252),(.004,.001,.021),dark,.0005);o.rotation_euler.y=angle
for z in (.065,.083,.101):box('Rear lower recessed vent',(0,.219,z),(.105,.005,.008),dark,.001)
# Long rail and paired sights, clear centre channel.
box('Continuous rail bed',(0,-.47,.46),(.093,1.16,.028),chassis,.006)
for i in range(27):
 y=-1.015+i*.039
 box('Machined top rail cross rib',(0,y,.482),(.103,.018,.018),steel,.0025)
 box('Rail dark central seam',(0,y,.493),(.048,.019,.003),chassis,.0005)
for y in (-1.015,.045):
 box('Sight mounting saddle',(0,y,.505),(.128,.095,.026),chassis,.006)
 box('Sight front bridge',(0,y,.527),(.09,.054,.025),steel,.004)
 for side in (-1,1):
  box('Open sight protective ear',(side*.045,y,.558),(.023,.045,.08),chassis,.006)
  box('Amber sight insert',(side*.031,y+.024,.556),(.008,.006,.041),status,.0015)
# Rear sight base access screws and inset eye-facing badge.
box('Rear sight recess',(0,.099,.512),(.067,.007,.015),dark,.002)
# Native mount-space hand contacts measured from CaptureGrip.
# Blender -X maps to Unity +X; -Y maps to Unity +Z. The shallow,
# asymmetric fore-end meets the palm while letting the four fingers wrap up.
for side in (-1,1):
 panel('Fore-end machined support flange',side*.104,[(-.77,-.029),(-.22,-.029),(-.24,-.056),(-.32,-.12),(-.43,-.148),(-.61,-.148),(-.77,-.09)],.012,chassis,.005)
panel('Palm fitted rubber fore-end',.012,[(-.79,-.035),(-.21,-.035),(-.25,-.072),(-.33,-.142),(-.43,-.172),(-.60,-.169),(-.79,-.092)],.196,rubber,.012)
for y in [-.70+i*.035 for i in range(9)]:
 bottom=(-.169+(abs(y)-.60)/.19*.077) if y<-.60 else -.169
 box('Fore-end shallow moulded rib',(.012,y,bottom+.003),(.172,.009,.008),rubber,.002)
# One ergonomic rear grip replaces the old overlapping rectangular primitives.
# Cross sections follow the palm/backstrap and fingertip envelope; the ring
# and little fingers curl around its forward face, thumb rests above the heel.
sections=[(.045,-.022,.092,.12),(-.13,-.018,.11,.151),(-.22,-.012,.113,.158),(-.30,.003,.105,.145),(-.36,.027,.089,.117)]
verts=[];N=16
for z,cy,w,d in sections:
 for i in range(N):
  t=math.tau*i/N
  # Rounded rectangular section, broad textured flats instead of a round rod.
  c=math.cos(t);sn=math.sin(t)
  verts.append((-.025+math.copysign(abs(c)**.6,c)*w/2,cy+math.copysign(abs(sn)**.6,sn)*d/2,z))
faces=[tuple(range(N-1,-1,-1)),tuple(range((len(sections)-1)*N,len(sections)*N))]
for j in range(len(sections)-1):
 for i in range(N):faces.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
me=bpy.data.meshes.new('Fitted rear grip');me.from_pydata(verts,[],faces);me.update();ob=bpy.data.objects.new('Vector single contoured pistol grip',me);bpy.context.collection.objects.link(ob);finish(ob,ob.name,rubber,.005)
box('Pistol grip upper ferrule',(-.025,-.022,.006),(.11,.14,.086),chassis,.01)
# A continuous bowed trigger guard and separate curved trigger meet the index pad.
curve=bpy.data.curves.new('Vector trigger guard path','CURVE');curve.dimensions='3D';curve.bevel_depth=.006;curve.bevel_resolution=4
spline=curve.splines.new('BEZIER');path=[(-.025,.035,.018),(-.025,-.18,.018),(-.025,-.19,-.125),(-.025,-.125,-.17),(-.025,-.075,-.15)]
spline.bezier_points.add(len(path)-1)
for p,co in zip(spline.bezier_points,path):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
o=bpy.data.objects.new('Vector bowed trigger guard',curve);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');finish(bpy.context.object,'Vector bowed trigger guard',steel,0);bpy.ops.object.select_all(action='DESELECT')
panel('Curved trigger blade',-.025,[(-.085,.024),(-.108,.014),(-.105,-.065),(-.080,-.128),(-.066,-.143),(-.057,-.137),(-.075,-.069)],.018,steel,.003)
# Rear strap details stay outside the finger-contact face.
for z in (-.14,-.17,-.20,-.23,-.26,-.29):
 box('Grip backstrap traction',(-.025,.064+(abs(z)-.2)*.06,z),(.076,.006,.006),rubber,.002)
# Save editable individual authored parts before export join.
for o in list(bpy.context.scene.objects):
 if o not in parts:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'scripts/vector_shotgun/vector-petrol.blend'))
# Apply normal/bevel modifiers, batch identical materials only; no shape reduction.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:
 bpy.context.view_layer.objects.active=o;o.select_set(True)
 for mod in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 o.select_set(False)
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();gun=bpy.context.object;gun.name='weapon:joined';gun.data.name='VectorPetrolDetailed'
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');gun.scale=(.32,.32,.32)
bpy.ops.export_scene.gltf(filepath=str(OUT/'vector-petrol.glb'),export_format='GLB',use_selection=True,export_animations=False,export_extras=True,export_yup=True)
# Studio output of the actual mesh, no image-generation involved.
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=32;s.render.resolution_x=1600;s.render.resolution_y=1000;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('Studio');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.18,.23,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.4;s.view_settings.view_transform='AgX'
center=Vector((0,-.15,.055))
for loc,energy,size in [((1,-1,2),160,1.5),((-1,-.3,.5),80,1),((0,1,1),120,1)]:
 bpy.ops.object.light_add(type='AREA',location=center+Vector(loc));o=bpy.context.object;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.68
for name,offset in [('model',( .7,-1,.65)),('rear',(.55,1,.55)),('side',(1,0,.12))]:
 cam.location=center+Vector(offset);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
print('VECTOR_AUTHOR_COMPLETE')
