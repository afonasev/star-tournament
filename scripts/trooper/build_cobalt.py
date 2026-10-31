"""Detailed COBALT, approved 2026-10-26. Run in Blender; -- --preview-only is non-shipping.
Metres, Y up, Z forward before glTF conversion. No decimation or detail reduction.
"""
import argparse, json, math, shutil, sys
from pathlib import Path
import bpy, bmesh
import numpy as np
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]; ART=ROOT/'unity/Assets/StarTournament/Art'; OUT=ROOT/'.local/cobalt'
p=argparse.ArgumentParser();p.add_argument('--preview-only',action='store_true')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
sys.path.insert(0,str(Path(__file__).resolve().parent))
from cobalt_surfaces import material as mat, bake_atlas
blue=mat('blue anodized armor',(.045,.13,.42),.25,.48)
blue2=mat('blue bevel inserts',(.055,.16,.48),.3,.43)
dark=mat('graphite chassis',(.035,.045,.055),.35,.55)
rubber=mat('grip rubber',(.009,.012,.016),.04,.72,.07)
steel=mat('machined steel',(.34,.40,.46),.85,.38)
black=mat('recessed black',(.004,.006,.009),.35,.5)
lime=mat('lime inlays',(.49,.78,.018),.25,.36)
materials=[blue,blue2,dark,rubber,steel,black,lime]
def finish(o,m,bevel=.002,segments=3):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;o.data.materials.append(m)
 if bevel:
  mod=o.modifiers.new('precision rounded edges','BEVEL');mod.width=bevel;mod.segments=segments;bpy.ops.object.modifier_apply(modifier=mod.name)
 for f in o.data.polygons:f.use_smooth=True
 mod=o.modifiers.new('weighted machined normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50;bpy.ops.object.modifier_apply(modifier=mod.name)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.01);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False)
 return o
def mesh(name,verts,faces,m,bevel=.002):
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
 bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(data);bm.free()
 o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);return finish(o,m,bevel)
def profile(name,points,width,m,x=0,bevel=.002):
 n=len(points);verts=[(x+s*width/2,y,z) for s in (-1,1) for z,y in points]
 faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 return mesh(name,verts,faces,m,bevel)
def box(name,pos,size,m,bevel=.002):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,m,bevel)
def tube(name,z,y,r,inner,length,m,x=0,n=64,axis='Z'):
 verts=[]
 for along,rad in [(-length/2,r),(length/2,r),(-length/2,inner),(length/2,inner)]:
  for i in range(n):
   a=i*2*math.pi/n;v=(rad*math.cos(a),rad*math.sin(a),along)
   if axis=='X':v=(along,v[1],v[0])
   verts.append((v[0]+x,v[1]+y,v[2]+z))
 faces=[]
 for i in range(n):
  j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
 return mesh(name,verts,faces,m,.00065)
def screw(s,z,y,x=.067,r=.005):
 tube('recessed fastener socket',z,y,r*1.28,r*.84,.0015,black,x=s*x,axis='X',n=32)
 tube('hex socket steel fastener',z,y,r,r*.46,.002,steel,x=s*(x+.001),axis='X',n=6)
def stripe(name,points,x,m=lime):return profile(name,points,.0018,m,x=x,bevel=.0003)
# Deliberate primary silhouettes and sculpted hand-contact surfaces.
profile('monocoque receiver',[(-.115,.083),(-.088,.108),(.157,.108),(.202,.077),(.195,.001),(.117,-.033),(.039,-.032),(.014,-.009),(-.084,-.009),(-.115,.014)],.108,dark,bevel=.004)
profile('upper cobalt receiver shell',[(-.103,.083),(-.080,.112),(.152,.112),(.19,.083),(.182,.050),(.075,.048),(.051,.032),(-.08,.032),(-.102,.045)],.119,blue,bevel=.003)
profile('graphite handguard chassis',[(.162,.074),(.383,.074),(.415,.051),(.408,-.014),(.371,-.037),(.183,-.037),(.153,-.009)],.100,dark,bevel=.004)
profile('sculpted fore-end contact pad',[(.183,-.018),(.37,-.018),(.388,-.035),(.357,-.055),(.205,-.055),(.18,-.04)],.093,rubber,bevel=.006)
for s in (-1,1):
 profile('forward cobalt upper shell',[(.181,.098),(.358,.098),(.414,.07),(.404,.044),(.335,.048),(.306,.062),(.194,.060)],.014,blue,x=s*.055,bevel=.0025)
 profile('forward lower cobalt cheek',[(.344,.029),(.410,.033),(.401,-.008),(.368,-.032),(.339,-.024)],.012,blue2,x=s*.058)
 profile('receiver stepped lower armor',[(.038,.018),(.076,.035),(.143,.034),(.184,.009),(.164,-.045),(.102,-.069),(.055,-.043)],.015,blue,x=s*.058,bevel=.0025)
 profile('diagonal steel buttress',[(.105,.039),(.122,.044),(.174,-.017),(.164,-.034),(.146,-.021)],.007,steel,x=s*.068,bevel=.0013)
 stripe('receiver lime seam',[(.047,.047),(.129,.047),(.136,.052),(.054,.052)],s*.061)
 stripe('forward lime slash',[(.268,.083),(.292,.083),(.28,.094),(.256,.094)],s*.063)
 profile('ventilation pocket',[(.197,.063),(.298,.063),(.312,.052),(.304,.041),(.19,.041)],.0025,black,x=s*.063)
 for z in (.211,.232,.253,.274):profile('vent internal vane',[(z,.044),(z+.008,.044),(z+.013,.059),(z+.005,.059)],.002,steel,x=s*.064,bevel=.0005)
 for z,y in [(.072,.012),(.155,.006),(.355,.064),(.390,.011),(-.075,.069)]:screw(s,z,y)
 for z in np.linspace(.215,.342,14):profile('grip diagonal tread',[(z,-.039),(z+.003,-.039),(z+.009,-.022),(z+.006,-.022)],.0016,dark,x=s*.047,bevel=.00035)
# Skeleton stock: true open window between spine and swept strut.
profile('rear stock upper spine',[(-.338,.107),(-.111,.107),(-.09,.080),(-.116,.052),(-.335,.069)],.088,blue,bevel=.004)
profile('stock lower strut',[(-.34,.071),(-.305,.049),(-.241,-.031),(-.209,-.019),(-.15,.046),(-.124,.049),(-.18,-.008),(-.228,-.061),(-.257,-.061),(-.33,.018)],.067,dark,bevel=.004)
profile('stock heel blue trim',[(-.344,.103),(-.321,.097),(-.312,-.032),(-.29,-.074),(-.31,-.086),(-.346,-.053)],.078,blue,bevel=.003)
profile('rubber shoulder butt',[(-.359,.109),(-.344,.11),(-.341,-.048),(-.309,-.085),(-.325,-.094),(-.359,-.055)],.088,rubber,bevel=.006)
for s in (-1,1):
 stripe('stock lime diagonal',[(-.177,.065),(-.193,.065),(-.22,.092),(-.203,.092)],s*.045)
 stripe('stock lime heel',[(-.324,-.055),(-.314,-.063),(-.31,-.047),(-.32,-.040)],s*.041)
 profile('stock recessed inset',[(-.292,.078),(-.239,.078),(-.221,.065),(-.248,.060),(-.29,.067)],.002,black,x=s*.045,bevel=.001)
 screw(s,-.278,.071,x=.046,r=.0045)
for y in np.linspace(-.036,.09,10):box('buttpad molding',(0,y,-.360),(.076,.003,.0025),dark,.0006)
# Closed right palm fits preserved mount; guard remains open.
profile('primary grip structural core',[(-.047,.014),(.011,.003),(.015,-.027),(.024,-.058),(.006,-.154),(-.015,-.172),(-.062,-.156),(-.055,-.139),(-.049,-.037)],.054,dark,bevel=.005)
for s in (-1,1):
 profile('inset rubber palm pad',[(-.040,-.031),(.020,-.048),(.006,-.143),(-.014,-.155),(-.043,-.145)],.004,rubber,x=s*.027)
 stripe('cobalt grip heel',[(-.049,-.142),(-.017,-.161),(.004,-.150),(.007,-.159),(-.016,-.174),(-.058,-.151)],s*.028,blue)
 for z,y in [(-.017,-.149),(-.025,-.042)]:screw(s,z,y,x=.03,r=.003)
profile('open trigger guard bow',[(.020,-.074),(.083,-.074),(.096,-.058),(.106,-.006),(.094,-.008),(.084,-.059),(.077,-.063),(.022,-.063)],.037,dark)
profile('lime curved trigger',[(.051,-.003),(.059,-.006),(.054,-.036),(.042,-.046),(.034,-.046),(.044,-.035)],.013,lime,bevel=.0013)
profile('guard front brace',[(.08,.008),(.105,-.001),(.1,-.023),(.086,-.048),(.077,-.044)],.044,blue)
# Open bridge, rails and protected sights.
box('receiver top rail',(0,.119,.026),(.041,.010,.24),steel,.001)
profile('open bridge beam',[(.017,.145),(.27,.145),(.286,.132),(.28,.125),(.032,.13)],.027,dark,bevel=.0015)
for z in (.025,.272):profile('blue bridge foot',[(z-.017,.103),(z+.014,.103),(z+.018,.133),(z+.002,.146),(z-.012,.144)],.046,blue)
for z in np.linspace(-.072,.095,15):box('rail cross notch',(0,.125,z),(.043,.003,.003),dark,.0005)
for z in (-.063,.343):
 box('sight shoe',(0,.121,z),(.056,.013,.035),dark)
 for s in (-1,1):
  profile('protected sight ear',[(z-.011,.127),(z+.009,.127),(z+.006,.178),(z-.006,.178),(z-.012,.157)],.009,dark,x=s*.021,bevel=.0015)
  box('lime sight marker',(s*.020,.158,z-.012),(.003,.019,.0015),lime,.0004)
 box('sight post',(0,.146,z),(.004,.030,.005),steel,.0006)
# Layered open muzzle; one physical bore, no black disk at the tip.
tube('exposed steel barrel',.433,.062,.022,.012,.085,steel)
tube('barrel collar',.411,.062,.029,.022,.025,dark)
tube('muzzle shoulder ring',.467,.062,.028,.020,.012,steel)
tube('muzzle brake',.502,.062,.030,.015,.062,dark)
tube('muzzle bright rim',.535,.062,.031,.015,.006,steel)
tube('deep inner bore',.505,.062,.015,.012,.058,black)
for a in np.linspace(0,2*math.pi,8,endpoint=False):
 o=box('longitudinal brake flute',(.030*math.cos(a),.062+.030*math.sin(a),.502),(.0035,.0025,.038),black,.001);o.rotation_euler[2]=a-math.pi/2
for z in (.443,.45,.457):tube('barrel turning line',z,.062,.0225,.0218,.0015,dark)
for s in (-1,1):
 profile('service port recess',[(-.068,.066),(-.012,.066),(.003,.079),(-.005,.087),(-.067,.087)],.002,black,x=s*.061,bevel=.001)
 profile('service port steel interior',[(-.057,.069),(-.018,.069),(-.009,.078),(-.057,.078)],.0016,steel,x=s*.062,bevel=.0005)
 tube('selector pivot',-.041,.018,.009,.003,.004,steel,x=s*.061,axis='X',n=32)
 profile('selector lever',[(-.048,.019),(-.035,.021),(-.015,.013),(-.018,.008),(-.039,.013)],.004,dark,x=s*.065,bevel=.001)
 stripe('triangular insignia',[(.052,.075),(.065,.092),(.081,.075),(.073,.075),(.065,.085),(.058,.075)],s*.0615,steel)
 for z in (.101,.110,.119):stripe('service index',[(z,.072),(z+.003,.072),(z+.003,.081),(z,.081)],s*.0615,steel)
# Secondary machining and paint wear: geometry, not a normal-map approximation.
def pocket(target,pos,size):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);cut=bpy.context.object;cut.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 edge=cut.modifiers.new('rounded recess corners','BEVEL');edge.width=.002;edge.segments=3;bpy.ops.object.modifier_apply(modifier=edge.name)
 bpy.context.view_layer.objects.active=target
 mod=target.modifiers.new('machined pocket','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut
 bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
 for f in target.data.polygons:f.material_index=0
 while len(target.data.materials)>1:target.data.materials.pop(index=len(target.data.materials)-1)
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('longitudinal brake flute'):bpy.data.objects.remove(obj,do_unlink=True)
for pos,size in [((0,.088,.502),(.010,.025,.035)),((0,.036,.502),(.010,.025,.035)),((.026,.062,.502),(.025,.010,.035)),((-.026,.062,.502),(.025,.010,.035))]:
 pocket(bpy.data.objects['muzzle brake'],pos,size)
for s in (-1,1):
 target=bpy.data.objects.get('upper cobalt receiver shell')
 pocket(target,(s*.062,.078,-.033),(.018,.023,.067))
 # A dark recessed receiver panel with inner metal travel rail.
 box('recessed port floor',(s*.052,.078,-.033),(.001,.019,.062),black,.001)
 box('port internal rail',(s*.054,.074,-.035),(.002,.004,.047),steel,.0007)
 # Conformal inset side panel on the broad fore-end.
 profile('fore-end recessed rubber panel',[(.19,.014),(.31,.014),(.334,-.002),(.322,-.024),(.207,-.028),(.189,-.015)],.002,rubber,x=s*.051,bevel=.001)
 for z in np.linspace(.214,.308,20):
  profile('fore-end diagonal rubber knurl',[(z,-.019),(z+.0015,-.019),(z+.013,.006),(z+.0115,.006)],.0008,dark,x=s*.0525,bevel=.00015)
 profile('rear stock service panel',[(-.299,.097),(-.249,.097),(-.227,.079),(-.237,.071),(-.3,.078)],.003,dark,x=s*.045,bevel=.001)
 screw(s,-.286,.087,x=.048,r=.003)
 # Discrete edge scuffs sit on coating boundaries, never random floating greebles.
 for z,y,length in [(-.083,.109,.019),(-.04,.110,.012),(.02,.109,.023),(.102,.109,.012),(.193,.095,.017),(.255,.095,.009),(.312,.095,.018),(-.306,.104,.022),(-.235,.105,.013)]:
  stripe('chipped coating edge',[(z,y),(z+length,y),(z+length*.82,y-.0007),(z+.001,y-.0012)],s*(.0598 if z>-.11 and z<.16 else .062 if z>.16 else .0445),steel)
 # Fine molded pistol-grip chevrons, fitted to the sloping palm pad.
 for i in range(13):
  y=-.065-i*.0055;z=-.012+(y+.065)*.14
  profile('pistol grip molded chevron',[(z-.021,y),(z-.001,y-.003),(z+.02,y),(z+.02,y-.001),(z,y-.0045),(z-.021,y-.001)],.0009,dark,x=s*.030,bevel=.0002)
# Top cooling channels and ribbed breech sleeve remain visible in first person.
for z in np.linspace(.187,.30,8):
 box('upper shroud cooling recess',(0,.099,z),(.030,.0015,.006),black,.001)
for z in np.linspace(-.105,-.079,6):box('rear breech rib',(0,.111,z),(.095,.003,.0017),dark,.0003)
for name,point in [('rifle-grip',(0,-.09,0)),('rifle-muzzle',(0,.062,.54)),('cobalt-support',(0,-.05,.29))]:
 bpy.ops.object.empty_add(type='PLAIN_AXES',location=point);bpy.context.object.name=name
axis=Matrix.Rotation(math.pi/2,4,'X')
for o in [o for o in bpy.context.scene.objects if o.type=='MESH']:
 stock=o.data.attributes.new('cobalt_stock_texture','FLOAT','CORNER')
 for item in stock.data:item.value=1.0 if o.name in ('rear stock upper spine','rubber shoulder butt') else 0.0
 wear=o.data.attributes.new('cobalt_edge_wear','FLOAT','CORNER')
 for face in o.data.polygons:
  value=1.0 if max(abs(c) for c in face.normal)<.999 and face.area<.0003 else 0.0
  for loop in face.loop_indices:wear.data[loop].value=value
for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
def counts(objects):
 for o in objects:o.data.calc_loop_triangles()
 return {'objects':len(objects),'triangles':sum(len(o.data.loop_triangles) for o in objects),'vertices':sum(len(o.data.vertices) for o in objects),'materials':len({m.name for o in objects for m in o.data.materials})}
before=counts(meshes)
source=OUT/'cobalt-rifle.blend' if args.preview_only else ROOT/'scripts/trooper/cobalt-rifle.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(source))
for m in materials:
 group=[o for o in list(bpy.context.scene.objects) if o.type=='MESH' and o.data.materials[0]==m]
 if not group:continue
 bpy.ops.object.select_all(action='DESELECT')
 for o in group:o.select_set(True)
 bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();group[0].name='cobalt-'+m.name.split(' / ')[1].replace(' ','-')
after=counts([o for o in bpy.context.scene.objects if o.type=='MESH']);assert before['triangles']==after['triangles']
texture_dir=(OUT/'textures') if args.preview_only else ROOT/'assets/source/cobalt/textures'
texture_dir.mkdir(parents=True,exist_ok=True)
atlases=[]
for o in [o for o in bpy.context.scene.objects if o.type=='MESH']:
 atlases.append(bake_atlas(o,o.data.materials[0],texture_dir))

dest=OUT if args.preview_only else ART
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(dest/'automatic-rifle-lod0.glb'),export_format='GLB',use_selection=True,export_apply=True)
shutil.copy2(dest/'automatic-rifle-lod0.glb',dest/'automatic-rifle-lod1.glb')
if not args.preview_only:
 import hashlib
 path=ART/'asset-manifest.json';manifest=json.loads(path.read_text())
 for entry in manifest['entries']:
  if entry['key'] in ('rifle-lod0','rifle-lod1'):
   entry['sha256']=hashlib.sha256((ROOT/'unity'/entry['path']).read_bytes()).hexdigest();entry['source']='COBALT approved concept 01, 2026-10-26; scripts/trooper/build_cobalt.py and cobalt-rifle.blend; full detail, lossless material batching, no collision'
 path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
report={'before_batching':before,'after_batching':after,'lossless':True,'lod1':'same detailed geometry; simplification deferred by user','units':'meters','up':'+Y','forward':'+Z','collision':'none','source':str(source),'textures':{'count':len(materials)*3,'atlases':atlases,'channels':'basecolor + packed roughness/metallic + tangent normal'},'muzzle':[0,.062,.54]}
(OUT/'asset-pass.json').write_text(json.dumps(report,indent=2))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1800;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.color=(.18,.18,.18);scene.view_settings.view_transform='AgX';center=Vector((0,-.08,.015))
for pos,power,size in [((1,-.4,1.4),160,1.2),((-1,-.1,.6),110,1),((0,.8,1),200,1)]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1.2,-.55,.48));camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=1.1
for name,pos in [('beauty',(1.2,-.55,.48)),('side',(1.3,-.08,.08)),('fps',(.32,.60,.32))]:
 camera.location=pos;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
print(json.dumps(report))
