"""SEAM 01: reference-matched industrial cutter, metres / Y up / Z forward.
Standalone generator; packed PBR textures, editable components, material-only batching.
Run blender --background --python scripts/trooper/build_cutter.py
"""
import bpy,bmesh,math,json,hashlib,random
import numpy as np
from pathlib import Path
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'unity/Assets/StarTournament/Art';OUT=ROOT/'.local/seam-f5';OUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
# Surface maps are authored in physical planar coordinates, not one stretched patch per object.
def mat(name,color,metal,rough):
 m=bpy.data.materials.new('SEAM '+name);m.use_nodes=True;m.diffuse_color=(*color,1)
 m.node_tree.nodes.clear();p=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');out=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(p.outputs['BSDF'],out.inputs['Surface'])
 p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 size=1024;rng=np.random.default_rng(sum(map(ord,name)));yy,xx=np.indices((size,size));grain=rng.normal(0,.008,(size,size));broad=np.zeros((size,size))
 for period,weight in [(16,.006),(64,.009),(256,.012)]:
  grid=rng.normal(0,weight,(size//period,size//period));broad+=np.repeat(np.repeat(grid,period,0),period,1)
 # Fine stipple, abrasion and sparse scratches are intentionally visible at weapon distance.
 marks=np.zeros((size,size));scratches=np.zeros((size,size))
 for i in range(12 if name=='ochre enamel' else 8):
  x=int(rng.integers(5,size-5));y=int(rng.integers(5,size-5));length=int(rng.integers(3,90));slope=rng.uniform(-.8,.8)
  for j in range(length):
   ix=(x+j)%size;iy=int(y+j*slope)%size;scratches[iy,ix]=rng.uniform(.3,.8)
 for i in range(80):
  x=int(rng.integers(0,size));y=int(rng.integers(0,size));r=int(rng.integers(1,3));marks[max(0,y-r):y+r,max(0,x-r):x+r]=rng.uniform(.12,.38)
 if name=='grip rubber':grain+=((np.sin(xx*1.3)*np.sin(yy*1.3))*.10)
 if metal>.7:grain+=rng.normal(0,.025,(size,1))
 linear=np.clip(np.array(color)[None,None,:]*(1+grain[:,:,None]+broad[:,:,None]-marks[:,:,None]),0,1)
 if name=='ochre enamel':linear=linear*(1-scratches[:,:,None])+np.array([.09,.08,.065])*scratches[:,:,None]
 else:linear*=1-scratches[:,:,None]*.3
 srgb=np.where(linear<=.0031308,12.92*linear,1.055*np.maximum(linear,0)**(1/2.4)-.055)
 def texture(label,rgb,space):
  image=bpy.data.images.new('SEAM '+name+' '+label,width=size,height=size);image.colorspace_settings.name=space
  pixels=np.ones((size,size,4),np.float32);pixels[:,:,:3]=rgb;image.pixels.foreach_set(pixels.ravel());image.pack()
  node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;node.extension='REPEAT';return node
 tex=texture('albedo',srgb,'sRGB');m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
 values=np.clip(rough+grain*.7+marks*.15+scratches*.12,.12,.95)
 roughtex=texture('roughness',np.repeat(values[:,:,None],3,2),'Non-Color');m.node_tree.links.new(roughtex.outputs['Color'],p.inputs['Roughness'])
 # Packed tangent-space surface relief survives export (fine enamel, brushed metal, rubber stipple).
 dy,dx=np.gradient(grain+marks*.4+scratches*.15);normal=np.stack((-dx*.35,-dy*.35,np.ones_like(dx)),axis=2);normal/=np.linalg.norm(normal,axis=2)[:,:,None]
 normaltex=texture('normal',normal*.5+.5,'Non-Color');normalnode=m.node_tree.nodes.new('ShaderNodeNormalMap');m.node_tree.links.new(normaltex.outputs['Color'],normalnode.inputs['Color']);m.node_tree.links.new(normalnode.outputs['Normal'],p.inputs['Normal'])
 return m
Y=mat('ochre enamel',(.58,.30,.026),.12,.57);D=mat('graphite titanium',(.033,.04,.048),.55,.53);R=mat('grip rubber',(.012,.016,.019),.02,.76);C=mat('copper exchanger',(.52,.19,.068),.85,.34);S=mat('machined steel',(.27,.30,.33),.75,.46);B=mat('deep recess',(.004,.006,.008),.2,.58);A=mat('amber emitter',(1,.38,.055),.15,.3)
p=next(n for n in A.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Emission Color'].default_value=(1,.32,.04,1);p.inputs['Emission Strength'].default_value=3
E=A.copy();E.name='SEAM battery charge'
MATS=[Y,D,R,C,S,B,A,E]
def finish(o,material,width=.002,segments=3):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;o.data.materials.append(material)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(o.data);bm.free()
 if width:
  mod=o.modifiers.new('machined edge radius','BEVEL');mod.width=width;mod.segments=segments;bpy.ops.object.modifier_apply(modifier=mod.name)
 for f in o.data.polygons:f.use_smooth=True
 mod=o.modifiers.new('weighted planar normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50;bpy.ops.object.modifier_apply(modifier=mod.name)
 uv=o.data.uv_layers.new(name='physical-planar')
 for face in o.data.polygons:
  normal=face.normal;axis=max(range(3),key=lambda k:abs(normal[k]));axes=[i for i in range(3) if i!=axis]
  for li in face.loop_indices:
   v=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(v[axes[0]]*5,v[axes[1]]*5)
 o.select_set(False);return o

def mesh(name,verts,faces,m,edge=.002):
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);return finish(o,m,edge)
def panel(name,outline,x,width,m,edge=.002):
 n=len(outline);vertices=[(xx,y,z) for xx in (x-width/2,x+width/2) for z,y in outline]
 faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 return mesh(name,vertices,faces,m,edge)
def box(name,pos,size,m,edge=.002):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,m,edge)
def pipe(name,points,r,m):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=10;c.bevel_depth=r;c.bevel_resolution=3;s=c.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
 for b,p in zip(s.bezier_points,points):b.co=p;b.handle_left_type=b.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');return finish(o,m,0)
def ring(name,z,length,outer,inner,m,center=.10):
 # Chamfered rectangular aperture: recognisable concept silhouette, not a stack of round discs.
 shape=[(-.65,-1),(.65,-1),(1,-.65),(1,.65),(.65,1),(-.65,1),(-1,.65),(-1,-.65)]
 v=[(x*radius,center+y*radius,depth) for depth,radius in [(z-length/2,outer),(z+length/2,outer),(z-length/2,inner),(z+length/2,inner)] for x,y in shape]
 faces=[]
 for i in range(8):j=(i+1)%8;faces.extend([(i,j,8+j,8+i),(16+i,24+i,24+j,16+j),(i,16+i,16+j,j),(8+i,8+j,24+j,24+i)])
 return mesh(name,v,faces,m,.002)
def screw(side,z,y,x=.111,r=.004):
 bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=r*1.5,depth=.002,location=(side*x,y,z),rotation=(0,math.pi/2,0));finish(bpy.context.object,B,.0004)
 bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=r,depth=.003,location=(side*(x+.0015),y,z),rotation=(0,math.pi/2,0));finish(bpy.context.object,S,.0003)
 box('fastener slot',(side*(x+.0032),y,z),(.0005,r*.24,r*1.1),B,.0001)
def glyph(text,position,size,m,side):
 c=bpy.data.curves.new(text,'FONT');c.body=text;c.size=size;c.extrude=.00008;o=bpy.data.objects.new(text,c);bpy.context.collection.objects.link(o);o.location=position;o.rotation_euler=Matrix(((0,0,side),(0,1,0),(-side,0,0))).to_euler();bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');finish(o,m,0)
# Authored varying cross-sections. X width and shoulder/belly heights change along
# the weapon; outer skins return across the top/bottom instead of floating plates.
def loft(name,sections,m,edge=.002):
 # Each section: z, half width, bottom, top, corner cut. Eight continuous faces.
 verts=[]
 for z,w,lo,hi,c in sections:
  verts.extend([(x,y,z) for x,y in [(-w+c,lo),(w-c,lo),(w,lo+c),(w,hi-c),(w-c,hi),(-w+c,hi),(-w,hi-c),(-w,lo+c)]])
 faces=[tuple(range(7,-1,-1)),tuple(range((len(sections)-1)*8,len(sections)*8))]
 for j in range(len(sections)-1):
  for i in range(8):faces.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i))
 return mesh(name,verts,faces,m,edge)
def skin(name,outline,side,depth,m,bevel=.014):
 # Broad, angled return at the rim; the central plane stands away from the core.
 zc=sum(p[0] for p in outline)/len(outline);yc=sum(p[1] for p in outline)/len(outline);n=len(outline)
 inner=[(z+(zc-z)*bevel/max(.025,abs(z-zc)),y+(yc-y)*bevel/max(.025,abs(y-yc))) for z,y in outline]
 verts=[(side*(depth-.025),y,z) for z,y in outline]+[(side*depth,y,z) for z,y in inner]
 faces=[tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 return mesh(name,verts,faces,m,.0015)
loft('receiver forged backbone',[(-.245,.066,.015,.192,.019),(-.18,.075,.018,.215,.016),(.08,.078,.008,.213,.014),(.27,.082,.005,.212,.014),(.46,.085,.005,.203,.02)],D,.003)
loft('rear battery beveled housing',[(-.262,.067,.005,.19,.018),(-.242,.083,-.005,.203,.024),(-.20,.083,-.005,.203,.024),(-.18,.070,.019,.207,.016)],D,.003)
# Separate spine has a rising rear shoulder and breaks above the exposed mechanism.
loft('upper enamel spine',[(-.227,.069,.175,.204,.010),(-.18,.083,.178,.225,.014),(.10,.084,.184,.23,.013),(.24,.080,.181,.225,.012),(.30,.071,.178,.219,.012)],Y,.002)
loft('front wrapped shroud',[ (.235,.080,.005,.205,.021),(.272,.116,-.009,.241,.034),(.315,.119,-.008,.238,.028),(.448,.108,.000,.218,.027),(.49,.093,.014,.203,.026)],Y,.003)
# Deep rectangular slots cut into the actual solid shell, with recessed floors.
for side in (-1,1):
 outer=bpy.data.objects['front wrapped shroud']
 cutter=panel('temporary ventilation cutter',[(.291,.203),(.301,.219),(.397,.208),(.392,.194)],side*.115,.06,B,.001)
 bpy.ops.object.select_all(action='DESELECT');outer.select_set(True);bpy.context.view_layer.objects.active=outer
 mod=outer.modifiers.new('recessed cooling slot','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
 panel('vent shadow floor',[(.29,.20),(.303,.219),(.399,.208),(.393,.19)],side*.088,.002,B,.0005)
 for z in (.312,.334,.356,.378):box('vent separator',(side*.092,.202,z),(.008,.012,.004),D,.0005)
 # Stepped under-shell widens over its service reservoir, then tucks under front shroud.
 skin('lower cranked armor',[(-.153,.092),(-.124,.132),(.014,.132),(.060,.086),(.245,.086),(.269,.046),(.214,-.048),(.172,-.069),(.015,-.065),(-.015,-.009),(-.142,.005)],side,.107,Y,.009)
 skin('lower reservoir inset',[(.025,.042),(.222,.045),(.198,-.035),(.165,-.053),(.04,-.05)],side,.111,Y,.005)
 skin('mechanism upper cover',[(-.13,.159),(-.111,.185),(.04,.184),(.066,.165),(.201,.169),(.234,.139),(.206,.121),(-.099,.13)],side,.087,D,.006)
 # A sloping instrument recess follows the front shroud's changing width.
 bezel=panel('instrument dark inset',[(.317,.063),(.33,.079),(.414,.074),(.43,.057),(.417,.043),(.329,.047)],side*.117,.004,B,.002)
 box('amber instrument inset',(side*.120,.060,.374),(.002,.007,.072),A,.001)
 for y,zoff in [(.113,0),(.143,.015)]:
  pipe('braided flexible power cable',[(side*.095,y,.041+zoff),(side*.104,y-.018,.089),(side*.105,y-.018,.191),(side*.103,y+.003,.252)],.0085,R)
  for z in (.055,.235):
   # Cylindrical compression fittings wrap around the actual curved cable axis.
   bpy.ops.mesh.primitive_cylinder_add(vertices=20,radius=.011,depth=.017,location=(side*.103,y-.006,z));finish(bpy.context.object,C,.001)
   for zz in (z-.008,z+.008):
    bpy.ops.mesh.primitive_torus_add(major_radius=.0095,minor_radius=.0014,major_segments=20,minor_segments=6,location=(side*.103,y-.006,zz));finish(bpy.context.object,S,0)
 # Fine ribbed protection is actual geometry, following the exposed cable span.
 for y in (.113,.143):
  for z in np.linspace(.102,.192,13):
   bpy.ops.mesh.primitive_torus_add(major_radius=.0084,minor_radius=.00065,major_segments=16,minor_segments=5,location=(side*.105,y-.018,float(z)));o=bpy.context.object;o.name='cable protective braid';finish(o,D,0)
 for z,y,x in [(-.188,.193,.087),(.102,.203,.088),(-.112,.094,.109),(.032,.036,.112),(.184,-.029,.112),(.289,.179,.118),(.447,.113,.11)]:screw(side,z,y,x,.003)
 glyph('SEAM-01',(side*.086,.198,.09 if side==1 else -.17),.014,D,side)
 for z in (-.157,-.144,-.131):panel('serial paint',[(z,.186),(z+.005,.186),(z+.01,.201),(z+.005,.201)],side*.086,.0003,D,0)
 for z in (.075,.091):panel('service drainage',[(z,-.039),(z+.004,-.039),(z+.011,-.019),(z+.007,-.019)],side*.112,.0005,B,0)
 for a,b in [((.435,.098),(.446,.118)),((.446,.118),(.457,.098)),((.457,.098),(.435,.098))]:
  delta=Vector(b)-Vector(a);n=Vector((-delta.y,delta.x)).normalized()*.0007
  panel('warning stencil',[a,b,tuple(Vector(b)+n),tuple(Vector(a)+n)],side*.11,.0003,D,0)
# Service top rail follows stepped spine rather than one featureless slab.
for z,length,y in [(-.14,.13,.231),(.016,.14,.236),(.186,.095,.232)]:
 box('segmented graphite top hatch',(0,y,z),(.112,.011,length),D,.002)
 for side in (-1,1):
  box('hatch latch',(side*.045,y+.006,z-length*.33),(.013,.004,.019),S,.001)
for z in (-.17,.30):
 panel('stamped carry bridge',[(z-.04,.229),(z-.03,.251),(z+.03,.251),(z+.04,.229),(z+.025,.229),(z+.02,.243),(z-.022,.243),(z-.027,.229)],0,.078,D,.0015)
# F4: a recessed, replaceable energy cartridge; 16 cells are the approved asset topology.
# Keep the original rear silhouette and all three gameplay/grip anchors.
cut=box('temporary battery cavity',(.020,.101,-.254),(.079,.151,.064),B,.004)
for name in ['rear battery beveled housing','receiver forged backbone']:
 body=bpy.data.objects[name];bpy.context.view_layer.objects.active=body
 mod=body.modifiers.new('battery socket recess','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut;bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.data.objects.remove(cut,do_unlink=True)
box('battery socket floor',(.020,.101,-.227),(.076,.148,.004),B,.003)
# Stepped graphite rails and copper cartridge contacts frame a deep, narrow cavity.
for x in (.062,-.022):
 box('battery protective rail',(x,.100,-.269),(.009,.161,.020),D,.002)
 box('battery rail machined return',(x,.100,-.280),(.002,.145,.002),S,.0005)
for y in (.020,.180):box('battery protective crossbar',(.020,y,-.270),(.085,.010,.018),D,.002)
for y in (.028,.172):
 box('battery copper end contact',(.020,y,-.267),(.061,.008,.012),C,.0015)
 box('battery contact lip',(.020,y,-.275),(.042,.004,.004),S,.0006)
box('battery yellow retaining latch',(.020,.185,-.278),(.044,.014,.010),Y,.002)
# A single separate emissive mesh carries a constant UV.x cell index per quad.
# Runtime reads energy; the 16 dark sockets remain physical geometry at zero charge.
verts=[];faces=[]
for i in range(16):
 y=.036+i*.0084
 box('battery cell socket %02d'%i,(.020,y,-.265),(.066,.0078,.013),D,.001)
 box('battery cell dark inset %02d'%i,(.020,y,-.272),(.058,.0064,.002),B,.0005)
 n=len(verts);verts.extend([(-.008,y-.0028,-.2732),(.048,y-.0028,-.2732),(.048,y+.0028,-.2732),(-.008,y+.0028,-.2732)]);faces.append((n,n+1,n+2,n+3))
charge=mesh('SEAM battery charge',verts,faces,E,0)
for i,face in enumerate(charge.data.polygons):
 for li in face.loop_indices:charge.data.uv_layers.active.data[li].uv=(float(i),.5)
# Mechanical quarter-turn release on the narrow side strip, with a yellow pointer.
for name,r,depth,z,m in [('lock socket',.019,.005,-.264,B),('lock steel rim',.0165,.004,-.269,S),('lock dial',.014,.006,-.273,D)]:
 bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=r,depth=depth,location=(-.049,.100,z));o=bpy.context.object;o.name='battery '+name;finish(o,m,.0008)
box('battery lock handle',(-.049,.100,-.278),(.006,.025,.006),D,.001)
box('battery lock amber pointer',(-.049,.106,-.2815),(.002,.010,.001),Y,.0004)
for x in (.056,-.055):
 for y in (.035,.165):
  bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.006,depth=.002,location=(x,y,-.265));finish(bpy.context.object,B,.0005)
  bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.004,depth=.003,location=(x,y,-.267));finish(bpy.context.object,S,.0004)
mesh('battery latch arrow',[(.015,.189,-.284),(.025,.189,-.284),(.020,.182,-.284)],[(0,1,2)],B,0)

# Radiator: axial horizontal copper blades at different heights. Black core fills
# the nose; blade ends disappear into cast collars, no tall free-standing cage.
loft('radiator internal core',[(.466,.078,.018,.19,.02),(.583,.080,.018,.19,.02)],B,.003)
for side in (1,):
 for y in (.035,.060,.085,.110,.135,.160,.183):
  loft('longitudinal copper heat fin',[(.477,.086,y-.004,y+.004,.002),(.49,.109,y-.004,y+.004,.002),(.566,.103,y-.004,y+.004,.002),(.588,.085,y-.004,y+.004,.002)],C,.001)
 # Clamped dark upper/lower rails join shell to nozzle, flush with fin envelope.
 for y in (.014,.194):box('cast radiator spine',(0,y,.532),(.176,.016,.126),D,.002)
# Nozzle contour changes width and depth around its chamfered aperture.
ring('rear muzzle collar',.583,.022,.111,.083,D)
ring('faceted nose housing',.615,.050,.119,.087,D)
ring('recessed machined aperture',.641,.009,.103,.087,S)
ring('copper inner bevel',.647,.009,.093,.079,C)
ring('emitter amber surround',.646,.007,.081,.071,A)
ring('dark emitter throat',.625,.025,.073,.062,D)
ring('emitter face',.622,.003,.064,.001,A)
for side in (-1,1):
 for y in (.033,.168):screw(side,.617,y,.12,.0025)
# Rear pistol grip: palm swell, curved backstrap and a flared heel.
loft('primary pistol grip',[(-.054,.031,-.19,-.055,.012),(-.028,.039,-.209,-.024,.012),(.006,.038,-.202,-.025,.011),(.034,.029,-.183,-.042,.010)],D,.003)
for side in (-1,1):
 skin('primary sculpted rubber palm',[(-.048,-.069),(-.024,-.047),(.015,-.051),(.023,-.09),(.005,-.185),(-.024,-.19),(-.042,-.156)],side,.041,R,.006)
 skin('primary yellow neck',[(-.029,-.029),(.022,-.031),(.023,-.061),(.008,-.077),(-.024,-.064)],side,.041,Y,.004)
 panel('primary flared heel',[(-.063,-.176),(.014,-.186),(.01,-.205),(-.017,-.222),(-.066,-.205)],side*.026,.030,D,.003)
 screw(side,-.035,-.192,.044,.003)
# Support handle is substantially broader, rounded across X and Z, with transverse
# raised pads over its forward surface and a thicker shoulder under the shell.
loft('support armored stem',[(.226,.033,-.199,-.018,.012),(.247,.045,-.215,-.013,.014),(.298,.044,-.205,-.013,.013),(.324,.032,-.182,-.031,.012)],D,.004)
for side in (-1,1):
 skin('support side rubber',[(.237,-.06),(.277,-.047),(.309,-.065),(.293,-.187),(.251,-.195),(.233,-.167)],side,.049,R,.006)
 skin('support yellow shoulder',[(.244,-.018),(.309,-.022),(.309,-.065),(.29,-.085),(.249,-.061)],side,.052,Y,.005)
 panel('support armored heel',[(.224,-.18),(.301,-.187),(.294,-.211),(.25,-.224),(.219,-.206)],side*.028,.035,D,.003)
 screw(side,.246,-.188,.049,.003)
for i,y in enumerate(np.linspace(-.069,-.179,7)):
 z=.326
 loft('support transverse grip pad',[(z-.011,.039,y-.008,y+.008,.005),(z+.003,.046,y-.008,y+.008,.005),(z+.009,.035,y-.007,y+.007,.004)],R,.002)
# Rear elastomer has an inset diamond tread. Small physical ridges stay inside
# the palm patch and follow the handle rake with the rest of the assembly.
for side in (-1,1):
 for row in range(11):
  y=-.083-row*.008
  for col in range(4):
   z=-.027+col*.008+(row%2)*.004
   panel('primary rubber diamond',[(z-.0025,y),(z,y+.003),(z+.0025,y),(z,y-.003)],side*.043,.0015,R,.00025)
 # A narrow exposed steel edge borders the heel, away from the palm.
 panel('primary heel metal inset',[(-.058,-.194),(.003,-.201),(-.011,-.213),(-.056,-.202)],side*.042,.0015,S,.0005)
# Rake complete handle assemblies together, including pads and fasteners.
for o in bpy.context.scene.objects:
 if o.type=='MESH' and o.name.startswith(('primary ','support ')):
  inv=o.matrix_world.inverted()
  for v in o.data.vertices:
   co=o.matrix_world@v.co;co.z+=(co.y+.035)*.24;v.co=inv@co
# Forefinger opening and curved trigger remain clear of palm volume.
panel('trigger guard',[(.012,-.019),(.089,-.022),(.112,-.047),(.104,-.111),(.078,-.135),(.011,-.132),(.015,-.115),(.075,-.116),(.088,-.102),(.095,-.052),(.082,-.039),(.017,-.038)],0,.043,D,.003)
panel('amber curved trigger',[(.049,-.038),(.061,-.04),(.061,-.067),(.05,-.088),(.038,-.09),(.047,-.075),(.052,-.055)],0,.017,Y,.0015)
# Local chips belong only to enamel seams and exposed corners, not uniform noise.
rng=random.Random(9027);chips=[]
for o in list(bpy.context.scene.objects):
 if o.type!='MESH' or o.data.materials[0]!=Y:continue
 o.data.update()
 for f in o.data.polygons:
  normal=(o.matrix_world.to_3x3()@f.normal).normalized()
  if f.area<.001:continue
  vs=[o.matrix_world@o.data.vertices[i].co for i in f.vertices];center=sum(vs,Vector())/len(vs)
  for a,b in zip(vs,vs[1:]+vs[:1]):
   delta=b-a
   for k in range(int(delta.length/.022)):
    v=a+delta*rng.uniform(.05,.95);tangent=delta.normalized();inward=(center-v).normalized();length=rng.uniform(.0007,.003);width=rng.uniform(.00015,.0005);offset=normal*.00012
    chips.append([v+offset,v+tangent*length+offset,v+tangent*length*.8+inward*width+offset,v+inward*width*.4+offset])
mesh('edge enamel wear',[tuple(v) for pts in chips for v in pts],[(i*4,i*4+1,i*4+2,i*4+3) for i in range(len(chips))],D,0)
# F5: authored rear-shell proportions, not a runtime/camera tuning parameter.
# Raise the belly toward its fixed upper shoulder. Taper into the unchanged
# receiver before the primary grip; keep all 16 cell UV indices and anchors.
# The release dial moves as a rigid detail, retaining its circular silhouette.
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 inv=o.matrix_world.inverted()
 for v in o.data.vertices:
  co=o.matrix_world@v.co
  if o.name.startswith('battery lock'):
   co.y+=(.203-.100)*.30
  elif co.z<-.14 and co.y<.203:
   blend=min(1.,(-.14-co.z)/.04)
   co.y+=(.203-co.y)*.30*blend
  v.co=inv@co
for name,pos in [('cutter-grip',(0,-.09,0)),('cutter-muzzle',(0,.10,.665)),('cutter-support-grip',(0,-.1,.29))]:bpy.ops.object.empty_add(location=pos);bpy.context.object.name=name
# Boolean recesses and proportion edits must not leave interpolated normals across broad planes.
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  o.data.update()
  for f in o.data.polygons:
   if f.area>.0003:f.use_smooth=False
axis=Matrix.Rotation(math.pi/2,4,'X')
for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world

def stats():
 obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];n=0
 for o in obs:o.data.calc_loop_triangles();n+=len(o.data.loop_triangles)
 return {'objects':len(obs),'triangles':n,'materials':len(MATS)}
def render(path):
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
 scene.world.color=(.07,.07,.07);scene.view_settings.view_transform='AgX';made=[]
 target=Vector((0,.04,.075))
 for pos,power,size in [((1,-.1,1.2),48,1),((-.6,-.5,.7),25,.7),((.1,.8,.8),60,.6)]:
  d=bpy.data.lights.new('studio','AREA');d.energy=power;d.size=size;o=bpy.data.objects.new('studio',d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler();made.append(o)
 d=bpy.data.cameras.new('review');o=bpy.data.objects.new('review',d);scene.collection.objects.link(o);o.location=(.60,1.35,.47);o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=1.07;scene.camera=o;made.append(o)
 scene.render.image_settings.file_format='PNG';scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
 for o in made:bpy.data.objects.remove(o,do_unlink=True)
# A monochrome geometry render precedes materials, allowing silhouette review.
clay=bpy.data.materials.new('review clay');clay.use_nodes=True;clay.node_tree.nodes.clear();clay_shader=clay.node_tree.nodes.new('ShaderNodeBsdfPrincipled');clay_shader.inputs['Base Color'].default_value=(.28,.28,.28,1);clay_shader.inputs['Roughness'].default_value=.65;clay_out=clay.node_tree.nodes.new('ShaderNodeOutputMaterial');clay.node_tree.links.new(clay_shader.outputs['BSDF'],clay_out.inputs['Surface'])
saved=[]
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  saved.append((o,list(o.data.materials)));o.data.materials.clear();o.data.materials.append(clay)
render(OUT/'model-clay.png')
for o,mats in saved:
 o.data.materials.clear()
 for m in mats:o.data.materials.append(m)
before=stats();bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('seam-cutter.blend')));render(OUT/'model-before-batching.png')
for m in MATS:
 obs=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.data.materials[0]==m];bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();obs[0].name=m.name
render(OUT/'model-after-batching.png');after=stats();reports=[]
assert before['triangles']==after['triangles'] and after['objects']==len(MATS)
images=[bpy.data.images.load(str(OUT/name),check_existing=False) for name in ('model-before-batching.png','model-after-batching.png')]
pixels=[np.array(i.pixels[:],dtype=np.float32).reshape(-1,4)[:,:3] for i in images]
comparison={'mean_absolute_linear_difference':float(np.mean(np.abs(pixels[0]-pixels[1]))),'identical_camera':True,'geometry_triangles_preserved':before['triangles']==after['triangles'],'classification':'batching equivalence only, not concept or human acceptance'}
(OUT/'batching-comparison.json').write_text(json.dumps(comparison,indent=2)+'\n')
for image in images:bpy.data.images.remove(image)
# No premature simplification: both existing keys intentionally retain full detail for this visual review.
for lod in (0,1):
 path=ART/f'cutter-lod{lod}.glb';bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True)
 reports.append({'lod':lod,'before':before,'after':after,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
manifest=ART/'asset-manifest.json';doc=json.loads(manifest.read_text())
for e in doc['entries']:
 if e['key'] in ('cutter-lod0','cutter-lod1'):
  e['sha256']=hashlib.sha256((ART/(e['key']+'.glb')).read_bytes()).hexdigest();e['source']='SEAM F5 compact rear energy cartridge, sixteen cells; standalone build_cutter.py + seam-cutter.blend; packed PBR maps; no collision proxy.'
manifest.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n');(OUT/'asset-cost.json').write_text(json.dumps(reports,indent=2)+'\n');print('SEAM_REPORT',json.dumps(reports))
