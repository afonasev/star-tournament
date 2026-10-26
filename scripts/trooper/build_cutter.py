"""SEAM 01: reference-matched industrial cutter, metres / Y up / Z forward.
Standalone generator; packed PBR textures, editable components, material-only batching.
Run blender --background --python scripts/trooper/build_cutter.py
"""
import bpy,bmesh,math,json,hashlib,random
import numpy as np
from pathlib import Path
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'unity/Assets/StarTournament/Art';OUT=ROOT/'.local/seam-rework';OUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
# Surface maps are authored in physical planar coordinates, not one stretched patch per object.
def mat(name,color,metal,rough):
 m=bpy.data.materials.new('SEAM '+name);m.use_nodes=True;m.diffuse_color=(*color,1)
 m.node_tree.nodes.clear();p=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');out=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(p.outputs['BSDF'],out.inputs['Surface'])
 p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 size=1024;rng=np.random.default_rng(sum(map(ord,name)));yy,xx=np.indices((size,size));grain=rng.normal(0,.024,(size,size));broad=np.zeros((size,size))
 for period,weight in [(16,.015),(64,.025),(256,.035)]:
  grid=rng.normal(0,weight,(size//period,size//period));broad+=np.repeat(np.repeat(grid,period,0),period,1)
 # Fine stipple, abrasion and sparse scratches are intentionally visible at weapon distance.
 marks=np.zeros((size,size));scratches=np.zeros((size,size))
 for i in range(240 if name=='ochre enamel' else 100):
  x=int(rng.integers(5,size-5));y=int(rng.integers(5,size-5));length=int(rng.integers(3,90));slope=rng.uniform(-.8,.8)
  for j in range(length):
   ix=(x+j)%size;iy=int(y+j*slope)%size;scratches[iy,ix]=rng.uniform(.3,.8)
 for i in range(480):
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
 dy,dx=np.gradient(grain+marks*.4+scratches*.15);normal=np.stack((-dx*2.2,-dy*2.2,np.ones_like(dx)),axis=2);normal/=np.linalg.norm(normal,axis=2)[:,:,None]
 normaltex=texture('normal',normal*.5+.5,'Non-Color');normalnode=m.node_tree.nodes.new('ShaderNodeNormalMap');m.node_tree.links.new(normaltex.outputs['Color'],normalnode.inputs['Color']);m.node_tree.links.new(normalnode.outputs['Normal'],p.inputs['Normal'])
 return m
Y=mat('ochre enamel',(.58,.30,.026),.22,.44);D=mat('graphite titanium',(.033,.04,.048),.72,.37);R=mat('grip rubber',(.012,.016,.019),.02,.76);C=mat('copper exchanger',(.52,.19,.068),.85,.34);S=mat('machined steel',(.27,.30,.33),.85,.33);B=mat('deep recess',(.004,.006,.008),.2,.58);A=mat('amber emitter',(1,.38,.055),.15,.3)
p=next(n for n in A.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Emission Color'].default_value=(1,.32,.04,1);p.inputs['Emission Strength'].default_value=3
MATS=[Y,D,R,C,S,B,A]
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
# Dense receiver body, broad recessed waist and separate overlapping armor skins.
panel('continuous inner receiver',[(-.245,.025),(-.245,.245),(.42,.245),(.49,.19),(.46,-.035),(.22,-.055),(.13,-.105),(-.045,-.095),(-.06,-.022)],0,.17,D,.006)
panel('upper long enamel spine',[(-.225,.237),(-.19,.271),(.395,.271),(.442,.234),(.41,.213),(-.2,.213)],0,.176,Y,.003)
panel('rear squared battery',[(-.257,-.012),(-.257,.216),(-.221,.238),(-.18,.225),(-.17,.03),(-.203,-.018)],0,.194,D,.005)
for side in (-1,1):
 panel('upper rear armor',[(-.22,.216),(-.209,.252),(.145,.252),(.17,.222),(.137,.188),(-.19,.185)],side*.091,.022,Y,.003)
 panel('central recessed channel',[(-.15,.15),(-.12,.19),(.24,.19),(.29,.15),(.235,.08),(-.08,.08)],side*.091,.008,B,.002)
 panel('machined waist insert',[(-.12,.17),(-.08,.193),(.05,.193),(.075,.172),(.19,.172),(.22,.148),(.19,.13),(-.1,.13)],side*.1,.012,D,.002)
 panel('lower cranked service panel',[(-.16,.075),(-.12,.125),(.015,.125),(.075,.059),(.255,.059),(.28,.025),(.222,-.067),(.17,-.105),(-.005,-.095),(-.028,-.025),(-.145,-.018)],side*.092,.031,Y,.004)
 panel('lower service inset',[(.065,.031),(.244,.031),(.218,-.05),(.167,-.083),(.035,-.078),(.019,-.018)],side*.112,.008,Y,.002)
 panel('front shroud underlying frame',[(.24,.04),(.252,.231),(.3,.269),(.48,.246),(.523,.17),(.493,-.035),(.31,-.048)],side*.09,.044,D,.004)
 outer=panel('large tapered muzzle shroud',[(.252,.027),(.267,.226),(.301,.258),(.461,.239),(.499,.166),(.475,-.014),(.317,-.027)],side*.106,.025,Y,.004)
 panel('bevel division upper',[(.269,.226),(.302,.256),(.461,.238),(.474,.213),(.316,.226)],side*.119,.002,Y,.001)
 panel('shroud lower armor lip',[(.25,.021),(.315,-.027),(.474,-.014),(.479,.017),(.327,.008),(.28,.056)],side*.114,.012,Y,.002)
 # Deep vent slot, dark walls and backing inset, no painted black sticker.
 cutter=panel('temporary ventilation cutter',[(.305,.206),(.321,.225),(.405,.216),(.402,.196)],side*.131,.048,B,.001)
 bpy.ops.object.select_all(action='DESELECT');outer.select_set(True);bpy.context.view_layer.objects.active=outer
 boolean=outer.modifiers.new('recessed ventilation slot','BOOLEAN');boolean.operation='DIFFERENCE';boolean.object=cutter;bpy.ops.object.modifier_apply(modifier=boolean.name);bpy.data.objects.remove(cutter,do_unlink=True)
 panel('ventilation shadow floor',[(.305,.206),(.321,.225),(.405,.216),(.402,.196)],side*.108,.001,B,.0005)
 for z in (.33,.35,.37,.39):box('vent inner vane',(side*.111,.21,z),(.004,.010,.003),S,.0003)
 panel('amber instrument bezel',[(.313,.061),(.328,.085),(.427,.081),(.442,.061),(.426,.042),(.326,.042)],side*.124,.007,D,.002)
 box('amber instrument inset',(side*.129,.063,.378),(.002,.009,.077),A,.001)
 for y in (.101,.14):
  pipe('flexible power cable',[(side*.106,y,.045),(side*.108,y-.018,.11),(side*.11,y-.018,.20),(side*.11,y,.267)],.0105,R)
  for z in (.052,.237):box('copper cable collar',(side*.11,y-.003,z),(.025,.024,.021),C,.002)
  for z in np.linspace(.09,.21,17):box('cable braid rib',(side*.119,y-.018,z),(.002,.019,.0014),D,.0003)
 for z,y,x in [(-.185,.224,.106),(.12,.222,.106),(-.105,.075,.109),(.035,.033,.113),(.193,-.044,.119),(.285,.198,.122),(.464,.151,.122),(.339,.002,.125)]:screw(side,z,y,x)
 glyph('SEAM-01',(side*.105,.215,.095 if side==1 else -.18),.018,D,side)
 for a,b in [((.438,.111),(.449,.132)),((.449,.132),(.46,.111)),((.46,.111),(.438,.111))]:
  delta=Vector(b)-Vector(a);n=Vector((-delta.y,delta.x)).normalized()*.0008
  panel('laser warning triangle',[a,b,tuple(Vector(b)+n),tuple(Vector(a)+n)],side*.121,.0008,D,.0001)
 glyph('!',(side*.122,.115,.451 if side==1 else .444),.011,D,side)
 glyph('CAUTION' ,(side*.121,.106,.47 if side==1 else .402),.006,D,side)
 # Flush polygonal panel seam and top hatch bolts.
 for z in (-.10,.10,.35):screw(side,z,.25,.09,.0028)
 for z in (-.17,-.155,-.14):panel('battery serial hash',[(z,.213),(z+.006,.213),(z+.012,.229),(z+.006,.229)],side*.106,.0008,D,.0001)
 for z in (.075,.095):panel('service drainage slit',[(z,-.065),(z+.005,-.064),(z+.016,-.033),(z+.011,-.034)],side*.118,.002,B,.0005)
# Upper interrupted black rail and stamped carry loops.
box('top graphite cap',(0,.276,.04),(.115,.012,.32),D,.002)
for z in (-.14,.30):
 panel('raised carry bridge',[(z-.043,.275),(z-.03,.302),(z+.029,.302),(z+.043,.275),(z+.028,.275),(z+.02,.29),(z-.021,.29),(z-.03,.275)],0,.077,D,.002)
box('rear status recess',(0,.065,-.263),(.105,.043,.008),B,.002);box('rear amber status',(0,.065,-.269),(.068,.012,.003),A,.001)
# Tighten the tall receiver around the original contact centres; keep full-sized exposed grips.
for o in list(bpy.context.scene.objects):
 if o.type=='MESH':
  inv=o.matrix_world.inverted()
  for v in o.data.vertices:
   co=o.matrix_world@v.co;co.y=.10+(co.y-.10)*.72;v.co=inv@co
# Chipped enamel follows actual outer face boundaries, never free-floating marks.
rng=random.Random(9027);chips=[]
for o in list(bpy.context.scene.objects):
 if o.type!='MESH' or o.data.materials[0]!=Y:continue
 o.data.update()
 for f in o.data.polygons:
  normal=(o.matrix_world.to_3x3()@f.normal).normalized()
  if abs(normal.x)<.99 or f.area<.001:continue
  vertices=[o.matrix_world@o.data.vertices[i].co for i in f.vertices];center=sum(vertices,Vector())/len(vertices)
  for a,b in zip(vertices,vertices[1:]+vertices[:1]):
   delta=b-a
   for k in range(int(delta.length/.012)):
    t=rng.uniform(.04,.94);v=a+delta*t;tangent=delta.normalized();inward=(center-v).normalized()
    length=rng.uniform(.001,.005);width=rng.uniform(.0002,.0008);offset=normal*.00015
    pts=[v+offset,v+tangent*length+offset,v+tangent*(length*.8)+inward*width+offset,v+inward*(width*.4)+offset]
    chips.append(pts)
mesh('conforming chipped enamel',[tuple(v) for pts in chips for v in pts],[(i*4,i*4+1,i*4+2,i*4+3) for i in range(len(chips))],D,0)
# Short radiator recessed between broad shroud and deeply faceted nozzle.
ring('inner heat core',.535,.10,.096,.067,B)
for z in np.linspace(.485,.572,8):ring('copper cooling lamella',float(z),.004,.111,.079,C)
# Front armor occupies most of the nose, cooling stack remains visible only through windows.
ring('muzzle rear armor band',.578,.035,.142,.112,D)
ring('broad faceted nozzle',.621,.055,.143,.093,D)
ring('outer machined bevel rim',.649,.006,.132,.115,S)
ring('warm inset aperture lip',.654,.007,.106,.096,C)
ring('recessed emitter collar',.638,.012,.093,.082,A)
ring('deep inner emitter well',.611,.004,.085,.001,A)
for side in (-1,1):
 for y in (-.002,.205):box('radiator longitudinal bridge',(side*.101,y,.534),(.029,.022,.115),D,.002)
 for y in (.002,.195):screw(side,.61,y,.145,.0035)
# Hand contact centres remain; grip heels rake rearward as in the reference.
for name,z in [('primary',0),('support',.29)]:
 def rake(points):return [(zz+z+(yy+.1)*.27,yy) for zz,yy in points]
 outline=rake([(-.035,-.027),(.039,-.031),(.033,-.07),(.024,-.184),(.007,-.215),(-.045,-.205),(-.05,-.174)])
 panel(name+' angled grip frame',outline,0,.058,D,.005)
 for side in (-1,1):
  panel(name+' inset rubber palm',rake([(-.028,-.059),(.026,-.058),(.016,-.18),(-.032,-.181),(-.038,-.155)]),side*.03,.006,R,.003)
  panel(name+' armored heel',rake([(-.046,-.174),(.025,-.181),(.024,-.198),(.006,-.219),(-.05,-.208),(-.055,-.193)]),side*.03,.008,D,.002)
  panel(name+' yellow neck inlay',rake([(-.026,-.036),(.03,-.039),(.024,-.077),(.009,-.089),(-.026,-.065)]),side*.032,.004,Y,.002)
  screw(side,z-.035,-.183,.036,.003)
 for y in np.linspace(-.08,-.172,8):box(name+' front molded ridges',(0,float(y),z+.026+(y+.1)*.27),(.057,.006,.010),D,.0015)
# Thick polygonal trigger guard with readable opening.
panel('trigger guard',[(-.006,-.038),(.036,-.032),(.10,-.031),(.116,-.055),(.106,-.127),(.082,-.145),(.011,-.142),(.014,-.126),(.081,-.125),(.091,-.112),(.1,-.06),(.086,-.049),(.03,-.05)],0,.042,D,.002)
panel('curved amber trigger',[(.049,-.047),(.062,-.048),(.062,-.072),(.051,-.093),(.036,-.095),(.045,-.081),(.05,-.063)],0,.016,Y,.0015)
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
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
 scene.world.color=(.07,.07,.07);scene.view_settings.view_transform='AgX';made=[]
 target=Vector((0,-.19,.045))
 for pos,power,size in [((1,-.1,1.2),48,1),((-.6,-.5,.7),25,.7),((.1,.8,.8),60,.6)]:
  d=bpy.data.lights.new('studio','AREA');d.energy=power;d.size=size;o=bpy.data.objects.new('studio',d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler();made.append(o)
 d=bpy.data.cameras.new('review');o=bpy.data.objects.new('review',d);scene.collection.objects.link(o);o.location=(1.15,-.78,.41);o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=1.07;scene.camera=o;made.append(o)
 scene.render.image_settings.file_format='PNG';scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
 for o in made:bpy.data.objects.remove(o,do_unlink=True)
before=stats();bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).with_name('seam-cutter.blend')));render(OUT/'model-before-batching.png')
for m in MATS:
 obs=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.data.materials[0]==m];bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();obs[0].name=m.name
render(OUT/'model-after-batching.png');after=stats();reports=[]
# No premature simplification: both existing keys intentionally retain full detail for this visual review.
for lod in (0,1):
 path=ART/f'cutter-lod{lod}.glb';bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True)
 reports.append({'lod':lod,'before':before,'after':after,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
manifest=ART/'asset-manifest.json';doc=json.loads(manifest.read_text())
for e in doc['entries']:
 if e['key'] in ('cutter-lod0','cutter-lod1'):
  e['sha256']=hashlib.sha256((ART/(e['key']+'.glb')).read_bytes()).hexdigest();e['source']='SEAM concept-fidelity rework; standalone build_cutter.py + seam-cutter.blend; packed PBR maps; no collision proxy.'
manifest.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n');(OUT/'asset-cost.json').write_text(json.dumps(reports,indent=2)+'\n');print('SEAM_REPORT',json.dumps(reports))
