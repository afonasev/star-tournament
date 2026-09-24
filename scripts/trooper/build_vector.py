"""Orbital refined equipment geometry (stable Vector pipeline keys). Blender background; preserves imported rig/clip contracts.
Authored dimensions are mesh content in meters, not runtime balance parameters.
"""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
OUT=ROOT/'.local/orbital-source';OUT.mkdir(parents=True,exist_ok=True)

def material(name,color,metal=0,rough=.4):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 return m

def setup():
 global white,navy,rubber,metal,light,dark,parts
 white=material('vector-ceramic',(.72,.79,.83),.22,.39);navy=material('vector-chassis',(.025,.042,.061),.7,.33)
 rubber=material('vector-glove',(.018,.025,.032),.05,.66);metal=material('vector-titanium',(.32,.38,.42),.82,.28)
 light=material('identity-vector-status',(.05,.7,.85),.4,.25);dark=material('vector-bore',(.004,.006,.009),0,.8)
 light.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value=(.01,.5,.8,1)
 light.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=.7
 # Original seamless woven glove map; no external material dependencies.
 image=bpy.data.images.new('vector-woven-glove',width=128,height=128)
 pixels=[]
 for y in range(128):
  for x in range(128):
   value=.009+(.008 if ((x//3+y//3)%2)==0 else 0)+(.013 if x%6==0 or y%6==0 else 0)
   pixels.extend((value,value*1.15,value*1.3,1))
 image.pixels=pixels;image.pack()
 tex=rubber.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
 rubber.node_tree.links.new(tex.outputs['Color'],rubber.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
 # Baked tangent-space woven normal, portable to glTF and Unity (no procedural shader).
 normal_image=bpy.data.images.new('orbital-woven-normal',width=128,height=128);normal_image.colorspace_settings.name='Non-Color';normal_pixels=[]
 for y in range(128):
  for x in range(128):
   dx=.27*math.sin(x*math.tau/6);dy=.27*math.sin(y*math.tau/6);v=Vector((dx,dy,1)).normalized();normal_pixels.extend((v.x*.5+.5,v.y*.5+.5,v.z*.5+.5,1))
 normal_image.pixels=normal_pixels;normal_image.pack();nt=rubber.node_tree.nodes.new('ShaderNodeTexImage');nt.image=normal_image
 nm=rubber.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.65
 rubber.node_tree.links.new(nt.outputs['Color'],nm.inputs['Color']);rubber.node_tree.links.new(nm.outputs['Normal'],rubber.node_tree.nodes.get('Principled BSDF').inputs['Normal'])
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
 """Closed convex shell with spline perimeter and domed face, never a flat extruded slab."""
 outline=smooth_outline(profile);center=sum(outline,Vector((0,0)))/len(outline);n=len(outline)
 verts=[];sign=1 if x>=0 else -1
 for radius,height in [(.02,1),(.35,1),(.72,.97),(.94,.65),(1,0),(.96,-.3),(.02,-.3)]:
  for p in outline:
   v=center+(p-center)*radius;verts.append((x+sign*depth*height,v.x,v.y))
 faces=[]
 for j in range(6):
  for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 faces.extend([tuple(reversed(range(n))),tuple(range(6*n,7*n))])
 return mesh_object(name,verts,faces,mat)

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
 parts.clear()
 axial_shell('sculpted receiver core',[(-.5,.14,.08,.13),(-.43,.14,.12,.16),(-.30,.14,.14,.18),(-.08,.14,.139,.18),(.06,.14,.125,.16),(.115,.14,.09,.11)],navy)
 for z in [.035,.245]:
  ring('barrel liner',-.64,z,.081,.068,.76,navy,48)
  ring('round ceramic muzzle socket',-1.001,z,.106,.075,.12,white,48)
  ring('machined titanium crown',-1.073,z,.095,.071,.026,metal,64)
  ring('concentric inner lip',-1.085,z,.078,.067,.012,navy,48)
  ring('recessed bore',-1.00,z,.072,.062,.16,dark,48)
  cyl('deep bore',(0,-.80,z),.064,.006,dark)
  for y in [-1.06,-1.025,-.97]:ring('machined collar groove',y,z,.107,.103,.005,navy,48)
  for i in range(12):
   angle=i*math.tau/12;ellipsoid('crown fastener',(.10*math.cos(angle),-1.042,z+.10*math.sin(angle)),(.003,.003,.003),metal)
 for side in [-1,1]:
  # Two sweeping enclosed ceramic fairings, separated by a recessed breathing channel.
  sculpt('upper flowing barrel shell',side*.076,[(-.98,.285),(-.93,.345),(-.61,.37),(-.34,.355),(-.25,.306),(-.38,.23),(-.68,.225),(-.94,.215)],.052,white)
  sculpt('lower flowing fore-end',side*.079,[(-.98,.082),(-.85,.131),(-.54,.149),(-.34,.115),(-.29,.002),(-.40,-.07),(-.65,-.06),(-.91,-.065)],.056,white)
  sculpt('rounded receiver cheek',side*.116,[(-.34,.307),(-.20,.336),(.028,.312),(.085,.255),(.08,.091),(.023,.046),(-.16,.066),(-.29,.152)],.039,white)
  sculpt('rear swept heel',side*.074,[(.007,.31),(.095,.31),(.155,.231),(.15,.10),(.108,.08),(.06,.174)],.035,white)
  # Dark elongated recesses use inset rim + smaller face, visibly nested in fairings.
  for y,z,length in [(-.73,.282,.14),(-.50,.285,.16),(-.62,.044,.12)]:
   profile=[(y-length/2,z-.013),(y+length/2,z-.013),(y+length/2+.009,z),(y+length/2,z+.013),(y-length/2,z+.013),(y-length/2-.009,z)]
   sculpt('vent machined rim',side*(.138 if z<.1 else .126),profile,.004,metal)
   sculpt('vent dark well',side*(.144 if z<.1 else .132),[(y+(a-y)*.9,z+(b-z)*.64) for a,b in profile],.0015,dark)
   for j in range(4):box('recessed vent louvre',(side*(.146 if z<.1 else .134),y-length*.3+j*length*.2,z),(.003,.004,.013),navy,.001)
  for j in range(3):
   y=-.87+j*.047;sculpt('swept forward gill',side*.140,[(y-.013,.014),(y+.012,.023),(y+.017,.035),(y-.011,.029)],.003,dark)
  sculpt('status pocket',side*.15,[(-.22,.13),(-.12,.13),(-.10,.15),(-.21,.157)],.005,navy)
  sculpt('status lens',side*.157,[(-.207,.139),(-.13,.14),(-.125,.148),(-.205,.149)],.003,light)
  for y,z,x in [(-.33,.278,.139),(.045,.24,.156),(-.30,.084,.147),(-.84,.102,.111)]:screw('flush shell fastener',side,y,z,x)
  marking('ORBITAL', (side*.158,-.26,.18),.020,side)
  marking('02', (side*.158,-.245,.23),.027,side)
  marking('DUAL / KINETIC', (side*.133,-.72,.325),.012,side)
  # Recessed concentric service ports in the receiver.
  screw('receiver service socket',side,-.044,.213,.158,.022)
  # Subtle continuous orbit mark in actual geometry, not an external decal dependency.
  pts=[(side*.158,-.164+.035*math.cos(t),.235+.023*math.sin(t)) for t in [i*math.tau/48 for i in range(48)]]
  tube('orbital insignia',pts,.0018,navy,True)
 # Rounded crown/spine with nested sight rails and rear face visible in FPS.
 axial_shell('ceramic crown',[(-.94,.332,.04,.024),(-.85,.357,.045,.023),(-.53,.376,.052,.017),(-.32,.365,.054,.02),(-.12,.346,.058,.026),(.045,.32,.055,.023)],white)
 box('sight bed',(0,-.35,.389),(.062,.81,.018),navy,.008)
 for i in range(8):box('sight micro rail',(0,-.65+i*.081,.405),(.057,.009,.008),metal,.003)
 sculpt('rear upper shroud',.0,[(-.07,.3),(.09,.27),(.09,.17),(.02,.16),(-.04,.2)],.07,navy)
 box('rear recessed face',(0,.109,.19),(.15,.022,.16),navy,.03)
 box('rear ceramic brow',(0,.128,.256),(.16,.035,.058),white,.026)
 for x in [-.057,.057]:box('rear cheek lip',(x,.127,.175),(.025,.026,.11),white,.012)
 for i in range(4):box('rear heatsink',(0,.129,.15+i*.018),(.082,.011,.005),metal,.002)
 box('rear sight',(0,.045,.354),(.085,.081,.022),navy,.009)
 box('rear sight signal',(0,.077,.365),(.032,.006,.007),light,.002)
 # Ergonomic palm swell and open rounded trigger guard.
 axial_shell('curved palm grip',[(.035,-.27,.048,.047),(.025,-.245,.060,.074),(.00,-.185,.066,.101),(-.025,-.12,.064,.11),(-.055,-.064,.051,.075)],rubber)
 for side in [-1,1]:
  sculpt('grip sculpted heel',side*.050,[(-.06,-.042),(.035,-.055),(.103,-.23),(.092,-.31),(.015,-.317),(-.044,-.23)],.023,white)
  sculpt('grip rubber insert',side*.073,[(-.048,-.065),(.012,-.083),(.066,-.231),(.056,-.281),(.009,-.284),(-.026,-.22)],.004,rubber)
  for i in range(12):
   for j in range(4):ellipsoid('grip grip stipple',(side*.079,-.025+j*.018+i*.003,-.098-i*.014),(.0015,.0028,.0028),navy)
 guard=[(0,-.07,-.053),(0,-.28,-.056),(0,-.301,-.093),(0,-.292,-.157),(0,-.25,-.185),(0,-.13,-.19),(0,-.06,-.16)]
 curve=smooth_outline([(y,z) for x,y,z in guard],6);tube('rounded trigger guard',[(0,p.x,p.y) for p in curve],.014,metal,True)
 tube('curved trigger',[(0,-.145,-.034),(0,-.15,-.075),(0,-.13,-.119),(0,-.15,-.139)],.009,navy)
 axial_shell('underbarrel palm rest',[(-.87,-.065,.06,.016),(-.78,-.089,.089,.035),(-.62,-.102,.091,.038),(-.47,-.093,.078,.03),(-.38,-.07,.055,.018)],rubber)
 for i in range(13):
  y=-.79+i*.025;tube('foregrip traction',[(x,y,-.133+abs(x)*.32) for x in [-.07,-.035,0,.035,.07]],.0028,navy)
 # Fit the authored shell above the retained support-hand trajectory. Grip and palm
 # rests remain anchored to the accepted animation; front shells clear curled fingertips.
 for part in parts:
  if any(label in part.name for label in ['grip','trigger','palm rest','foregrip']):continue
  for vertex in part.data.vertices:
   point=part.matrix_world@vertex.co
   point.z+=.105
   if point.y<-.4:point.y-=.08*min(1,(-point.y-.4)/.5)
   vertex.co=part.matrix_world.inverted()@point
 axial_shell('support throat',[(-.82,-.022,.055,.044),(-.66,.012,.074,.072),(-.48,.019,.073,.078),(-.35,.045,.052,.065)],navy)
 return join(parts[:],'weapon:joined')

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
 pieces=[];dorsal=Vector((0,-1,0))
 for side in ['Left','Right']:
  bones=rig.data.bones;w=bones[side+'Hand'].head_local;el=bones[side+'LowerArm'].head_local;shoulder=bones[side+'UpperArm'].head_local
  for i in range(3):
   bn=side+('UpperArm' if i==0 else 'UpperArmTwist'+str(i));bn=bn if bn in bones else side+'UpperArm'
   a=shoulder.lerp(el,i/3);b=shoulder.lerp(el,(i+1)/3+.035)
   pieces.append(tailored('tailored continuous upper sleeve',a,b,.10-i*.005,.09-i*.005,rubber,rig,bn,[(0,.93),(.1,1),(.5,.98),(.9,.96),(1,.94)]))
  for i in range(3):
   a=el.lerp(w,i/3);b=el.lerp(w,(i+1)/3+.026);bn=side+('LowerArm' if i==0 else 'LowerArmTwist'+str(i))
   width=.079-i*.009;depth=.069-i*.008
   pieces.append(tailored('woven forearm sleeve',a,b,width,depth,rubber,rig,bn,[(0,1),(.12,1.03),(.5,.98),(.9,.92),(1,.91)]))
   for j in range(2):
    aa=a.lerp(b,.08+j*.46);bb=a.lerp(b,.47+j*.46);off=dorsal*(depth*.46)
    pieces.append(tailored('overlapping curved ceramic gauntlet',aa+off,bb+off,width*.85,.019,white,rig,bn,[(0,.61),(.1,.91),(.3,1),(.7,.95),(.9,.8),(1,.55)]))
    # Flush fasteners, dark recessed inlays and edging travel with each articulated section.
    p=aa.lerp(bb,.7)+off+dorsal*.009
    pieces.append(skin_detail(ellipsoid('gauntlet socket',p,(.004,.002,.004),navy),rig,bn))
    pieces.append(skin_detail(ellipsoid('gauntlet fastener',p+dorsal*.0015,(.0023,.001,.0023),metal),rig,bn))
   direction=(b-a).normalized();cross=direction.cross(dorsal).normalized()
   for sign in [-1,1]:
    line=[a.lerp(b,t)+cross*sign*width*.40+dorsal*depth*.29 for t in [.06,.3,.6,.91]]
    pieces.append(skin_detail(tube('forearm bound seam',line,.0011,metal),rig,bn))
    for k in range(8):
     p=a.lerp(b,.12+k*.095)+cross*sign*width*.40+dorsal*depth*.29
     pieces.append(skin_detail(tube('seam stitch',[p-cross*.0015,p+cross*.0015],.00045,white),rig,bn))
  # Five slim wrist bellows, elliptical in the forearm's local frame.
  axis=(w-el).normalized();cross=axis.cross(dorsal).normalized()
  for j in range(5):
   center=w-axis*(.006+j*.007)
   ringpts=[center+cross*math.cos(t)*.030+dorsal*math.sin(t)*.026 for t in [k*math.tau/32 for k in range(32)]]
   pieces.append(skin_detail(tube('wrist flexible bellow',ringpts,.0022,navy,True),rig,side+'Hand'))
  proximal=[bones[side+d+'Proximal'].head_local for d in ['Index','Middle','Ring','Little']];end=sum(proximal,Vector())/4
  pieces.append(tailored('anatomical glove palm',w,end,.075,.035,rubber,rig,side+'Hand',[(0,.76),(.2,.91),(.45,1),(.72,1.04),(1,.94)]))
  for j in [-1,1]:
   off=Vector((j*.016,-.021,0))
   pieces.append(tailored('split metacarpal armor',w.lerp(end,.2)+off,w.lerp(end,.8)+off,.031,.010,white,rig,side+'Hand',[(0,.65),(.18,1),(.7,.96),(1,.55)]))
  for digit in ['Thumb','Index','Middle','Ring','Little']:
   for ix,suffix in enumerate(['Proximal','Intermediate','Distal']):
    bn=side+digit+suffix;bone=bones[bn];a=bone.head_local;b=bone.tail_local
    width=(.0215 if digit!='Little' else .018)*(1-ix*.08);depth=width*1.05
    pieces.append(tailored('shaped glove '+bn,a,b,width,depth,rubber,rig,bn))
    # Rounded joint volumes close the gaps as fingers bend without the old square plates.
    pieces.append(skin_detail(ellipsoid('flexible knuckle '+bn,a,(width*.49,depth*.48,width*.49),rubber),rig,bn))
    off=dorsal*(depth*.46)
    pieces.append(tailored('curved phalanx shield '+bn,a.lerp(b,.16)+off,a.lerp(b,.73)+off,width*.81,.0055,white,rig,bn,[(0,.48),(.17,.9),(.4,1),(.72,.92),(1,.45)]))
    tangent=(b-a).normalized();cross=tangent.cross(dorsal).normalized()
    for sign in [-1,1]:
     points=[a.lerp(b,t)+cross*sign*width*.39+dorsal*depth*.27 for t in [.12,.32,.55,.81]]
     pieces.append(skin_detail(tube('finger stitched seam',points,.00055,metal),rig,bn))
    for k in range(3):
     center=a.lerp(b,.04+k*.035)
     pieces.append(skin_detail(tube('knuckle fabric fold',[center+cross*x+dorsal*(depth*.46-abs(x)*.10) for x in [-width*.31,0,width*.31]],.00085,navy),rig,bn))
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
  node=bpy.data.objects.new('vector-muzzle-'+str(index),None);bpy.context.collection.objects.link(node);node.parent=gun;node.location=(0,-1.18,z+.105)
 if kind=='arms':
  for o in list(bpy.context.scene.objects):
   if o.type=='MESH' and o.find_armature()==rig:bpy.data.objects.remove(o,do_unlink=True)
  hands(rig)
 # Remove importer-only shape helpers, never export diagnostic geometry.
 keep=[o for o in bpy.context.scene.objects if o==rig or o.parent is not None]
 export(path,keep)
 # Save an inspectable authored Blender source for reproducibility.
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/('orbital-'+kind+'.blend')))
manifest_path=ART/'asset-manifest.json';manifest=json.loads(manifest_path.read_text())
for entry in manifest['entries']:
 if entry['key'] in ['world-weapon','view-weapon']:
  entry['path']='Assets/StarTournament/Art/vector-shotgun-lod0.glb';entry['sha256']=hashlib.sha256((ART/'vector-shotgun-lod0.glb').read_bytes()).hexdigest();entry['source']='Original authored Orbital refined; embedded equivalent in trooper derivatives'
 if entry['key'] in ['player-body','player-hands']:
  entry['sha256']=hashlib.sha256((ROOT/'unity'/entry['path']).read_bytes()).hexdigest();entry['source']='Original Orbital equipment + existing attributed Trooper rig/clips; build_vector.py'
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
print('VECTOR_EXPORT_COMPLETE')
mount_path=ART/'trooper-mounts.json';mount_data=json.loads(mount_path.read_text())
mount_data['equipment']='orbital-equipment-v2; original rig/grip motion with replacement first-person gloves and gauntlets'
for key,file in [('body','trooper-body-animated.glb'),('arms','trooper-arms-animated.glb')]:mount_data[key]['sha256']=hashlib.sha256((ART/file).read_bytes()).hexdigest()
mount_path.write_text(json.dumps(mount_data,indent=2)+'\n')
