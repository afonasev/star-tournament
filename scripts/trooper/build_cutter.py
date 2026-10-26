"""SEAM concept 01. Authored meters/+Y up/+Z muzzle, original grip-space.
Run: blender --background --python scripts/trooper/build_cutter.py
Editable source retains individual components; export joins by material only.
No collision proxies. Renderer-only LODs preserve the approved silhouette.
"""
from pathlib import Path
exec(Path(__file__).with_name('build_pulse.py').read_text().split('def make(lod):')[0])
from mathutils import Vector
import random

def mat(name,color,metal=.5,rough=.34):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 m.node_tree.nodes.clear()
 b=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');output=m.node_tree.nodes.new('ShaderNodeOutputMaterial');output.is_active_output=True
 m.node_tree.links.new(b.outputs['BSDF'],output.inputs['Surface'])
 b.inputs['Base Color'].default_value=(*color,1);b.inputs['Metallic'].default_value=metal;b.inputs['Roughness'].default_value=rough
 palette[m.name]={'baseColorFactor':[*color,1],'metallicFactor':metal,'roughnessFactor':rough}
 return m
OUT=ROOT/'.local/seam-cutter';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=Path(__file__).with_name('seam-cutter.blend')

def bevel(o,width=.003,segments=3):
 m=o.modifiers.new('rounded machined edge','BEVEL');m.width=width;m.segments=segments
 bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
 m=o.modifiers.new('weighted panel normals','WEIGHTED_NORMAL');m.keep_sharp=True
 bpy.ops.object.modifier_apply(modifier=m.name)
 return o

def panel(name,outline,x,width,material,edge=.003):
 # Side profile expressed as (forward Z,height Y); deliberate contiguous surfaces.
 vertices=[(xx,y,z) for xx in (x-width/2,x+width/2) for z,y in outline]
 n=len(outline);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
 faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
 o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 return bevel(o,edge)

def pipe(name,points,radius,material,lod):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=12 if lod==0 else 6;c.bevel_depth=radius;c.bevel_resolution=3 if lod==0 else 2
 s=c.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
 for b,p in zip(s.bezier_points,points):b.co=p;b.handle_left_type=b.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o.select_set(False)
 return o

def screw(side,z,y,steel,dark):
 for r,d,x,matl in [(.009,.004,.107,dark),(.006,.005,.110,steel)]:
  bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=d,location=(side*x,y,z),rotation=(0,math.pi/2,0));o=bpy.context.object;o.name='captive hex fastener';o.data.materials.append(matl);bevel(o,.0008,2)

def glyph(text,position,size,material,side):
 c=bpy.data.curves.new('stencilled '+text,'FONT');c.body=text;c.size=size;c.extrude=.0001
 o=bpy.data.objects.new('stencilled '+text,c);bpy.context.collection.objects.link(o);o.location=position
 # text X follows the receiver; local normal points outwards.
 o.rotation_euler=Matrix(((0,0,side),(0,1,0),(-side,0,0))).to_euler();o.data.materials.append(material)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH')

def stats():
 obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];tris=0
 for o in obs:o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles)
 return dict(objects=len(obs),triangles=tris,vertices=sum(len(o.data.vertices) for o in obs),materials=len({m.name for o in obs for m in o.data.materials}))

def render(path):
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
 scene.render.resolution_x=1500;scene.render.resolution_y=900;scene.render.resolution_percentage=100
 scene.world.color=(.10,.10,.10);scene.view_settings.view_transform='AgX'
 made=[]
 for pos,power,size in [((1.0,-.4,1.6),180,1.3),((-1,-.3,.8),130,1.0),((.2,.7,1.1),200,.8)]:
  d=bpy.data.lights.new('studio softbox','AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new('studio softbox',d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,-.18,.03))-o.location).to_track_quat('-Z','Y').to_euler();made.append(o)
 d=bpy.data.cameras.new('review camera');o=bpy.data.objects.new('review camera',d);scene.collection.objects.link(o);o.location=(1.1,-.95,.60);target=Vector((0,-.20,.015));o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=1.12;scene.camera=o;made.append(o)
 scene.render.film_transparent=False;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
 for o in made:bpy.data.objects.remove(o,do_unlink=True)

reports=[]
for lod in (0,1):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 yellow=mat('SEAM ochre enamel',(.68,.29,.012),.32,.43)
 dark=mat('SEAM graphite titanium',(.042,.05,.057),.78,.32)
 rubber=mat('SEAM grip rubber',(.014,.019,.022),.05,.72)
 copper=mat('SEAM copper heat exchanger',(.66,.245,.075),.88,.27)
 steel=mat('SEAM machined steel',(.28,.32,.35),.88,.29)
 black=mat('SEAM deep recess',(.005,.008,.011),.25,.5)
 amber=mat('SEAM amber emitter',(1,.49,.10),.15,.28)
 b=next(n for n in amber.node_tree.nodes if n.type=='BSDF_PRINCIPLED');b.inputs['Emission Color'].default_value=(1,.28,.025,1);b.inputs['Emission Strength'].default_value=1.8
 # Central spine and shaped enamel shell, stepped volumes rather than stacked boxes.
 panel('continuous graphite chassis',[(-.285,.04),(-.26,.18),(.36,.18),(.43,.13),(.40,-.025),(.12,-.05),(-.04,-.025),(-.24,-.01)],0,.155,dark,.009)
 panel('rear battery housing',[(-.285,.02),(-.29,.17),(-.255,.192),(-.16,.188),(-.12,.15),(-.13,.055),(-.19,.015)],0,.175,yellow,.007)
 panel('upper enamel bridge',[(-.19,.185),(-.13,.215),(.36,.215),(.405,.175),(.30,.166),(-.12,.166)],0,.142,yellow,.004)
 panel('top graphite inset',[(-.14,.215),(-.11,.226),(.26,.226),(.28,.211)],0,.08,dark,.002)
 for side in (-1,1):
  x=side*.085
  panel('rear sculpted shell',[(-.19,.08),(-.16,.16),(.045,.16),(.085,.125),(.075,.025),(.015,-.033),(-.105,-.031),(-.135,.025)],x,.035,yellow,.006)
  panel('forward sculpted shroud',[(.23,.045),(.25,.19),(.285,.217),(.445,.192),(.476,.135),(.44,.004),(.35,-.028),(.285,-.012)],x,.039,yellow,.005)
  panel('side graphite relief',[(-.12,.145),(-.10,.165),(.09,.165),(.14,.125),(.12,.10),(.035,.12)],side*.105,.006,dark,.002)
  panel('front luminous recess',[(.30,.055),(.31,.075),(.405,.075),(.416,.055),(.406,.04),(.31,.04)],side*.109,.005,black,.001)
  box('amber status slot',(side*.113,.056,.362),(.003,.008,.074),amber)
  # Power conduits remain sheltered inside the open waist of the side shell.
  for y in (.08,.122):
   pipe('braided power conduit',[(side*.075,y,.05),(side*.08,y,.11),(side*.08,y-.018,.18),(side*.083,y-.008,.27)],.010,rubber,lod)
   for z in (.075,.245):
    box('copper conduit coupling',(side*.081,y-.004,z),(.025,.025,.022),copper)
   if lod==0:
    for z in [ .12+i*.008 for i in range(11)]:
     box('conduit rib',(side*.088,y-.012,z),(.005,.021,.002),dark)
  for z,y in [(-.18,.11),(-.11,.035),(.046,.10),(.293,.178),(.421,.136),(.338,.008)]:screw(side,z,y,steel,black)
  for z in (.017,.032,.047):
   panel('industrial warning slash',[(z,.041),(z+.006,.045),(z+.018,.076),(z+.012,.072)],side*.105,.0015,dark,.0003)
  if lod==0:glyph('SEAM / 01',(side*.107,.127,-.025 if side==1 else -.145),.015,dark,side)
 # Small surface wear and segmented service-panel detail: authored meshes survive GLB export.
 rng=random.Random(104)
 for side in (-1,1):
  for outline in [ [(-.17,.085),(-.14,.145),(.045,.14),(.065,.12),(.055,.027),(.012,-.018),(-.10,-.018)],[(.25,.05),(.268,.18),(.29,.2),(.427,.18),(.455,.13),(.426,.01),(.35,-.015)] ]:
   for j in range(len(outline)-1):
    a=Vector(outline[j]);b=Vector(outline[j+1]);delta=b-a
    for k in range(3):
     t=rng.uniform(.12,.80);point=a+delta*t;length=rng.uniform(.004,.012);v=delta.normalized()*length
     panel('edge enamel scuff',[(point.x,point.y),(point.x+v.x,point.y+v.y),(point.x+v.x+.0006,point.y+v.y-.0006),(point.x+.0005,point.y-.0005)],side*.106,.0003,steel,.00005)
  panel('rear battery seam',[(-.245,.055),(-.24,.153),(-.235,.153),(-.24,.055)],side*.088,.0015,black,.0002)
  for z in (-.20,-.18,-.16):
   panel('battery vent',[(z,.048),(z+.008,.048),(z+.013,.077),(z+.005,.077)],side*.103,.002,black,.0005)
 # Rear emitter-status window.
 box('rear instrument recess',(0,.095,-.292),(.075,.095,.007),black)
 box('rear amber charge strip',(0,.095,-.298),(.008,.065,.004),amber)
 # Hexagonal cooling cage and deep recessed emission aperture.
 tube('radiator root',.445,.025,.113,.061,dark,12)
 for z in [ .458+i*.012 for i in range(10)]:
  o=tube('copper cooling fin',z,.005,.110,.063,copper,48 if lod==0 else 24);bevel(o,.001,2)
 tube('recessed emitter bore',.577,.13,.078,.058,black,48 if lod==0 else 24)
 for z in (.465,.590):
  o=tube('hexagonal nozzle armour',z,.025,.133,.101,dark,8);bevel(o,.003,3)
 for a in [math.pi/8+i*math.pi/2 for i in range(4)]:
  x=math.cos(a)*.115;y=.08+math.sin(a)*.115
  box('longitudinal radiator guard',(x,y,.526),(.021,.019,.128),dark)
 o=tube('machined aperture lip',.642,.026,.111,.078,steel,8);bevel(o,.002,3)
 o=tube('inner copper aperture',.647,.011,.083,.069,copper,32);bevel(o,.001,2)
 tube('amber emitter annulus',.638,.008,.070,.052,amber,32)
 tube('ceramic emitter well',.615,.009,.054,.003,amber,32)
 # Grips retain original contact centres; shape and heel treatment supply concept detail.
 for name,z in [('primary',0),('support',.29)]:
  outline=[(z-.036,-.035),(z+.034,-.035),(z+.025,-.19),(z+.011,-.216),(z-.046,-.206),(z-.05,-.183)]
  panel(name+' structural grip',outline,0,.055,dark,.007)
  panel(name+' rubber palm',[(z-.029,-.055),(z+.027,-.055),(z+.017,-.184),(z-.034,-.184)],0,.060,rubber,.006)
  for side in (-1,1):
   panel(name+' yellow grip inlay',[(z+.008,-.055),(z+.025,-.051),(z+.016,-.10),(z+.005,-.098)],side*.031,.003,yellow,.001)
   if lod==0:
    for row in range(14):
     for col in range(4):
      o=box('moulded grip stipple',(side*.0315,-.105-row*.005,z-.023+col*.008),(.0014,.0015,.003),rubber)
  for y in (-.073,-.092,-.111,-.13,-.149,-.168):box(name+' front traction rib',(0,y,z+.027),(.052,.003,.005),rubber)
  panel(name+' grip heel',[(z-.05,-.184),(z+.026,-.19),(z+.02,-.216),(z-.047,-.216)],0,.065,dark,.003)
 # Open trigger guard around right index finger; never fill the opening.
 pipe('trigger guard',[(.0,-.038,.065),(0,-.062,.105),(0,-.142,.096),(0,-.148,.032)],.009,dark,lod)
 pipe('yellow trigger',[(0,-.045,.047),(0,-.077,.052),(0,-.097,.035)],.006,yellow,lod)
 for name,pos in [('cutter-grip',(0,-.09,0)),('cutter-muzzle',(0,.08,.665)),('cutter-support-grip',(0,-.1,.29))]:
  bpy.ops.object.empty_add(location=pos);bpy.context.object.name=name
 # Author Y-up, store Blender Z-up, exporter restores glTF +Y-up.
 axis=Matrix.Rotation(math.pi/2,4,'X')
 for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world
 before=stats()
 if lod==0:
  bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE));render(OUT/'model-before-batching.png')
 groups={}
 for o in list(bpy.context.scene.objects):
  if o.type=='MESH':groups.setdefault(o.data.materials[0].name,[]).append(o)
 for name,objects in groups.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in objects:o.select_set(True)
  bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=name
 after=stats()
 if lod==0:render(OUT/'model-after-batching.png')
 path=ART/f'cutter-lod{lod}.glb';bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True);preserve_pbr(path)
 reports.append(dict(lod=lod,before=before,after=after,bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
manifest=ART/'asset-manifest.json';doc=json.loads(manifest.read_text())
for e in doc['entries']:
 if e['key'] in ('cutter-lod0','cutter-lod1'):
  path=ART/f"{e['key']}.glb";e['sha256']=hashlib.sha256(path.read_bytes()).hexdigest();e['source']='SEAM approved concept 01; scripts/trooper/seam-cutter.blend + build_cutter.py; renderer-only, no collision proxy.'
manifest.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n')
(OUT/'asset-cost.json').write_text(json.dumps(reports,indent=2)+'\n')
print('SEAM_ASSET_REPORT',json.dumps(reports))
