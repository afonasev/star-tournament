"""Pulse Mk II authored high-detail source. No gameplay or character-rig changes.
Blender: blender -b -noaudio --python scripts/pulse_mk2/build.py
Art coordinates: metres, X lateral/Y up/Z forward, original Pulse attachment frame.
Detail reduction is explicitly deferred; both compatible LOD keys retain the master.
"""
import bpy, math, json, struct, hashlib, random
from pathlib import Path
from mathutils import Matrix, Vector
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'unity/Assets/StarTournament/Art'
SOURCE=Path(__file__).resolve().parent
TMP=ROOT/'.local/pulse-mk2';TMP.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
PALETTE={}
TEXTURES={}
def material(name,color,metal,rough,texture=True):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 n=m.node_tree.nodes;p=n.get('Principled BSDF') or n.new('ShaderNodeBsdfPrincipled');out=n.get('Material Output') or n.new('ShaderNodeOutputMaterial');m.node_tree.links.new(p.outputs['BSDF'],out.inputs['Surface']);p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 PALETTE[name]={'baseColorFactor':[*color,1],'metallicFactor':metal,'roughnessFactor':rough}
 if texture:
  size=1024;rng=np.random.default_rng(41+len(PALETTE));noise=rng.normal(0,.028,(size,size,1));a=np.ones((size,size,4),dtype=np.float32);a[:,:,:3]=np.clip(np.array(color)[None,None,:]*(1+noise),0,1)
  # Fine authored coating/scratch tile. Subtle: large form remains primary.
  for i in range(1200):
   x,y=rng.integers(0,size,2);length=int(rng.integers(2,24));a[y:y+1,x:min(size,x+length),:3]*=.50 if i%3 else 1.25
  # Sparse coating chips and broad mottling remain visible in native closeups.
  yy,xx=np.mgrid[0:size,0:size]
  mottling=(np.sin(xx*.021+np.cos(yy*.017))*np.sin(yy*.031+xx*.004))[:,:,None]
  a[:,:,:3]*=(1+mottling*.045)
  for i in range(180):
   x,y=rng.integers(0,size,2);r=int(rng.integers(1,4));a[max(0,y-r):y+1,max(0,x-r):x+r,:3]*=.45
  im=bpy.data.images.new(name+' surface',width=size,height=size,alpha=True);im.pixels.foreach_set(a.ravel());im.update();im.filepath_raw=str(TMP/(name.replace(' ','-')+'.png'));im.file_format='PNG';im.save();im.pack();TEXTURES[name]=Path(im.filepath_raw)
  t=n.new('ShaderNodeTexImage');t.image=im;m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color']);PALETTE[name]['baseColorFactor']=[1,1,1,1]
 return m
DARK=material('pulse graphite',(.065,.073,.079),.52,.48)
WHITE=material('pulse white ceramic',(.74,.70,.62),.06,.54)
ORANGE=material('pulse orange rings',(.88,.205,.026),.25,.51)
BLACK=material('pulse recessed black',(.014,.018,.022),.12,.62)
STEEL=material('pulse bolts',(.34,.39,.42),.86,.27)
CYAN=material('pulse cyan strips',(.015,.65,.77),.2,.27,False)
BRASS=material('pulse warm titanium',(.40,.27,.12),.8,.3)

def finish(o,mat,bevel=0):
 o.data.materials.append(mat)
 if bevel:
  b=o.modifiers.new('Machined radii','BEVEL');b.width=bevel;b.segments=3;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=b.name)
 for p in o.data.polygons:p.use_smooth=True
 n=o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');n.keep_sharp=True;n.weight=30
 bpy.context.view_layer.objects.active=o
 try:bpy.ops.object.modifier_apply(modifier=n.name)
 except RuntimeError:o.modifiers.remove(n)
 return o

def box(name,pos,size,mat,bevel=.002):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return finish(o,mat,bevel)

def lathe(name,rings,mat,n=128,center=(0,.08),closed=False):
 v=[(center[0]+r*math.cos(i*math.tau/n),center[1]+r*math.sin(i*math.tau/n),z) for z,r in rings for i in range(n)]
 f=[]
 for j in range(len(rings)-1):
  for i in range(n):k=(i+1)%n;f.append((j*n+i,j*n+k,(j+1)*n+k,(j+1)*n+i))
 if closed:
  j=len(rings)-1
  for i in range(n):k=(i+1)%n;f.append((j*n+i,j*n+k,k,i))
 else:f.extend([tuple(reversed(range(n))),tuple((len(rings)-1)*n+i for i in range(n))])
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return finish(o,mat)

def ring(name,z,width,r,mat):
 return lathe(name,[(z-width/2,r-.004),(z-width/2+.003,r),(z+width/2-.003,r),(z+width/2,r-.004),(z+width/2,r-.014),(z-width/2,r-.014)],mat,closed=True)

def profile(name,poly,half,mat,x=0,bevel=.003):
 v=[(x+s*half,y,z) for s in (-1,1) for y,z in poly];n=len(poly)
 f=[tuple(reversed(range(n))),tuple(n+i for i in range(n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return finish(o,mat,bevel)

def cut(obj,cutter):
 bpy.context.view_layer.objects.active=obj;m=obj.modifiers.new('Authored recess','BOOLEAN');m.operation='DIFFERENCE';m.object=cutter;bpy.ops.object.modifier_apply(modifier=m.name);bpy.data.objects.remove(cutter,do_unlink=True)

def bolt(name,pos,axis='X',r=.005):
 bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=r,depth=.003,location=pos);o=bpy.context.object;o.name=name
 if axis=='X':o.rotation_euler[1]=math.pi/2
 elif axis=='Y':o.rotation_euler[0]=math.pi/2
 finish(o,STEEL,.0006)
 size=(.0035,.0012,r*1.3) if axis=='X' else (r*1.3,.0035,.0012)
 box(name+' hex recess',pos,size,BLACK,.0004)

def label(text,pos,size,mat,side=1):
 curve=bpy.data.curves.new('engraved identity','FONT');curve.body=text;curve.size=size;curve.align_x='CENTER';curve.extrude=.00005
 o=bpy.data.objects.new('Pulse engraved '+text,curve);bpy.context.collection.objects.link(o)
 o.matrix_world=Matrix(((0,0,side,pos[0]),(0,1,0,pos[1]),(-side,0,0,pos[2]),(0,0,0,1)))
 curve.materials.append(mat);bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH')
 # Conform the engraving to the ceramic cylinder, avoiding a floating flat plaque.
 inverse=o.matrix_world.inverted()
 for v in o.data.vertices:
  q=o.matrix_world@v.co;q.x=side*(math.sqrt(max(0,.122**2-(q.y-.08)**2))+.0002);v.co=inverse@q

def uv_all():
 for o in list(bpy.context.scene.objects):
  if o.type!='MESH':continue
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')

def mark(name,pos):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.location=pos

def export(name,keys):
 uv_all()
 axis=Matrix.Rotation(math.pi/2,4,'X')
 for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
 # Join only by material, lossless topology. Editable source retains named objects.
 # Boolean cut walls may introduce another material on the same mesh. Split those
 # first so identical black cavity faces do not survive as extra draw primitives.
 for o in list(bpy.context.scene.objects):
  if o.type!='MESH' or len(o.data.materials)<2:continue
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='MATERIAL');bpy.ops.object.mode_set(mode='OBJECT')
 for o in list(bpy.context.scene.objects):
  if o.type!='MESH' or not o.data.polygons:continue
  slots={p.material_index for p in o.data.polygons}
  if len(slots)==1:
   mat=o.data.materials[next(iter(slots))];o.data.materials.clear();o.data.materials.append(mat)
   for p in o.data.polygons:p.material_index=0
 for mat in list(bpy.data.materials):
  obs=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==mat]
  if not obs:continue
  bpy.ops.object.select_all(action='DESELECT')
  for o in obs:o.select_set(True)
  bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();obs[0].name=mat.name+' geometry'
 stats={'triangles':sum(len(p.vertices)-2 for o in bpy.context.scene.objects if o.type=='MESH' for p in o.data.polygons),'vertices':sum(len(o.data.vertices) for o in bpy.context.scene.objects if o.type=='MESH'),'renderers':sum(o.type=='MESH' for o in bpy.context.scene.objects),'detail_reduction':'deferred by explicit user request','source':str(SOURCE/(name+'.blend'))}
 for key,file in keys:
  path=OUT/file;bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True)
  data=path.read_bytes();length=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+length]);tail=data[20+length:]
  binary=bytearray(tail[8:])
  doc.setdefault('images',[]);doc.setdefault('textures',[]);doc.setdefault('samplers',[{'magFilter':9729,'minFilter':9987,'wrapS':10497,'wrapT':10497}])
  for m in doc['materials']:
   pbr=m.setdefault('pbrMetallicRoughness',{});pbr.update(PALETTE[m['name']])
   if m['name'] in TEXTURES:
    binary.extend(b'\0'*((-len(binary))%4));offset=len(binary);png=TEXTURES[m['name']].read_bytes();binary.extend(png)
    view=len(doc['bufferViews']);doc['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(png)})
    source=len(doc['images']);doc['images'].append({'bufferView':view,'mimeType':'image/png','name':m['name']+' authored surface'})
    tex=len(doc['textures']);doc['textures'].append({'source':source,'sampler':0});pbr['baseColorTexture']={'index':tex}
  binary.extend(b'\0'*((-len(binary))%4));doc['buffers'][0]['byteLength']=len(binary);tail=struct.pack('<II',len(binary),0x004e4942)+binary
  payload=json.dumps(doc,separators=(',',':')).encode();payload+=b' '*((-len(payload))%4)
  path.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)
  manifest=OUT/'asset-manifest.json';d=json.loads(manifest.read_text());entry=next(e for e in d['entries'] if e['key']==key);entry['sha256']=hashlib.sha256(path.read_bytes()).hexdigest();entry['source']='Original Pulse Mk II; scripts/pulse_mk2/build.py; high detail source preserved; reduction deferred';manifest.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
 (TMP/(name+'-audit.json')).write_text(json.dumps(stats,indent=2)+'\n')
 print('PULSE_ASSET',name,stats,flush=True)

# Deep real bore and layered muzzle, aligned with original pulse-muzzle transform.
lathe('continuous graphite barrel',[(-.30,.097),(-.285,.105),(.55,.105),(.588,.102),(.601,.094),(.601,.079),(.24,.079),(.24,.086),(-.30,.086)],DARK,closed=True)
ring('front recessed steel bore',.591,.018,.100,STEEL)
ring('muzzle shadow groove',.565,.012,.108,BLACK)
for z,w in [(.505,.055),(.17,.047),(-.215,.044)]:
 collar=ring('stepped orange collar',z,w,.123,ORANGE)
 ring('collar inner gasket',z-w/2-.004,.008,.115,BLACK)
 ring('collar edge lip',z+w/2-.003,.005,.124,ORANGE)
 for i in range(8):
  a=i*math.tau/8;x,y=.124*math.cos(a),.08+.124*math.sin(a)
  # Real recessed cooling slot, with dark wall behind the rounded opening.
  o=box('collar slot cutter',(x,y,z),(.018,.012,w*.65),BLACK,.003);o.rotation_euler[2]=a;cut(collar,o)
  o=box('collar pocket floor',(x*.94,.08+(y-.08)*.94,z),(.003,.010,w*.66),BLACK,.002);o.rotation_euler[2]=a
  for dz in (-w*.32,w*.32):
   o=box('collar segment seam',(x,y,z+dz),(.001,.032,.0012),DARK,.0004);o.rotation_euler[2]=a
# Complete continuous shells with real recessed windows cut through ceramic.
for za,zb in [(.212,.462),(-.178,.118)]:
 sh=lathe('sculpted ceramic shell',[(za,.112),(za+.012,.121),(zb-.015,.121),(zb,.113),(zb,.107),(za,.107)],WHITE,closed=True)
 for side in (-1,1):
  c=box('window cutter',(side*.115,.105,(za+zb)/2),(.09,.081,(zb-za)*.76),BLACK,.013);cut(sh,c)
  # Lower interlocking plate tapers and recessed service panel.
  poly=[(.027,za+.015),(.002,za+.043),(.002,zb-.045),(.028,zb-.012),(.052,zb-.015),(.052,za+.015)]
  profile('lower ceramic bridge',poly,.008,WHITE,x=side*.097,bevel=.003)
  for z in (za+.025,zb-.025):bolt('shell flush bolt',(side*.117,.044,z),r=.0035)
# Top openings expose the continuous dark launch tube, as on approved A.
for o in [o for o in bpy.context.scene.objects if o.name.startswith('sculpted ceramic shell')]:
 z=sum(v.co.z for v in o.data.vertices)/len(o.data.vertices)
 cut(o,box('upper shell opening',(0,.198,z),(.075,.075,.155),BLACK,.012))
# Panel seam emboss and restrained orange registration marks.
for side in (-1,1):
 for z in [-.09,.035,.29,.39]:
  box('shell shallow seam',(side*.117,.018,z),(.0018,.018,.0015),DARK,.0004)
 for i in range(5):
  box('front collar cooling inset',(side*.122,.075,.487+i*.008),(.0028,.016,.003),BLACK,.0008)
# Rear breech sculpt with cooling recesses; consistent closed rear face.
lathe('rear breech', [(-.385,.083),(-.377,.102),(-.35,.107),(-.255,.108),(-.246,.103)],DARK)
ring('rear steel end rim',-.373,.018,.109,STEEL)
ring('rear ceramic retention band',-.347,.025,.115,WHITE)
ring('rear end gasket',-.328,.011,.112,BLACK)
lathe('rear inset end cap',[(-.389,.071),(-.381,.084)],BLACK)
ring('rear cap inner ring',-.390,.008,.078,DARK)
for s in (-1,1):
 # Stepped, forked receiver plate leaves the rear graphite cylinder exposed.
 profile('breech ceramic fork',[(.015,-.337),(.041,-.337),(.05,-.296),(.125,-.284),(.151,-.255),(.177,-.226),(.171,-.166),(.15,-.145),(.127,-.155),(.134,-.213),(.105,-.25),(.025,-.262)],.007,WHITE,x=s*.104,bevel=.003)
 profile('breech lower structural gusset',[(.014,-.264),(.025,-.193),(-.02,-.149),(-.058,-.076),(-.047,-.023),(-.01,-.05),(.014,-.117),(.056,-.167),(.06,-.25)],.008,DARK,x=s*.080,bevel=.004)
 box('cyan indicator recess',(s*.110,.064,-.30),(.004,.025,.026),BLACK,.003)
 for sign in (-1,1):
  o=box('cyan V indicator',(s*.113,.065+sign*.003,-.300+sign*.004),(.0015,.003,.012),CYAN,.001);o.rotation_euler[0]=sign*.65
 for y,z in [(.03,-.26),(.135,-.19),(-.018,-.126)]:bolt('receiver captive pin',(s*.114,y,z),r=.004)
 # Real socket and louvered side housing, in the rear ceramic shell.
 poly=[(.005,-.163),(.082,-.163),(.096,-.149),(.096,-.061),(.082,-.042),(.033,-.042),(.008,-.065)]
 profile('receiver service socket rim',poly,.004,DARK,x=s*.117,bevel=.004)
 profile('receiver service socket inset',[(.025,-.144),(.073,-.144),(.081,-.134),(.081,-.069),(.069,-.059),(.034,-.059),(.025,-.073)],.002,STEEL,x=s*.120,bevel=.003)
 box('receiver service dark cavity',(s*.123,.055,-.101),(.002,.032,.068),BLACK,.004)
 box('socket inner lower ledge',(s*.125,.040,-.101),(.002,.006,.052),DARK,.0015)
 for z in (-.132,-.07):bolt('socket screw',(s*.125,.083,z),r=.0028)
 profile('lower removable receiver plate',[(-.022,-.15),(-.035,-.124),(-.026,-.042),(-.001,-.022),(.012,-.044),(.004,-.139)],.005,DARK,x=s*.087,bevel=.003)
 for z in (-.123,-.09,-.057):box('lower plate cooling slot',(s*.094,-.01,z),(.003,.012,.020),BLACK,.002)
# Low sight with a sloping base follows the body; protected rear aperture.
profile('top mechanism saddle',[(.177,-.23),(.202,-.23),(.212,-.19),(.209,-.035),(.189,.003),(.175,-.015)],.040,WHITE,bevel=.004)
lathe('top mechanism cylinder',[(-.227,.024),(-.218,.030),(-.045,.030),(-.028,.023)],DARK,n=64,center=(0,.210))
lathe('top mechanism ceramic sleeve',[(-.207,.031),(-.198,.035),(-.126,.035),(-.120,.031)],WHITE,n=64,center=(0,.210))
for z in (-.221,-.115,-.043):lathe('upper assembly steel ring',[(z-.004,.031),(z+.004,.031)],STEEL,n=64,center=(0,.210))
profile('upper sight rail',[(.229,-.20),(.252,-.18),(.252,-.073),(.238,-.044),(.229,-.044)],.027,DARK,bevel=.002)
for z in (-.17,-.145,-.12,-.095):box('sight rail machining slot',(0,.252,z),(.058,.004,.008),BLACK,.0008)
for s in (-1,1):
 for z in (-.182,-.145):bolt('upper sleeve screw',(s*.034,.212,z),r=.004)

# Sculpted shell overlaps, recessed keyways and tube retaining sleeves.
for za,zb in [(.212,.462),(-.178,.118)]:
 for side in (-1,1):
  for z in (za+.03,zb-.035):
   profile('shell recessed elongated keyway',[(.166,z-.009),(.176,z-.009),(.179,z+.014),(.174,z+.020),(.166,z+.018)],.0015,BLACK,x=side*.074,bevel=.002)
   profile('keyway inner metal',[(.169,z-.004),(.173,z-.004),(.174,z+.011),(.170,z+.013)],.0018,STEEL,x=side*.076,bevel=.001)
  profile('sculpted lower shell overlay',[(.023,za+.02),(.001,za+.045),(-.014,za+.082),(-.011,zb-.072),(.013,zb-.041),(.035,zb-.026),(.042,zb-.043),(.024,zb-.070),(.013,za+.085),(.030,za+.045)],.003,WHITE,x=side*.105,bevel=.002)
  for z in (za+.07,zb-.07):bolt('overlay flush pin',(side*.110,.012,z),r=.0028)
 # Thin sleeve details inside the exposed long window.
 for z in (za+.045,zb-.044):ring('tube sleeve rib',z,.006,.106,DARK)
# Front muzzle housing with real radial vents and retaining hardware.
front=ring('muzzle outer graphite housing',.573,.036,.113,DARK)
for i in range(8):
 a=i*math.tau/8;x,y=.113*math.cos(a),.08+.113*math.sin(a)
 o=box('muzzle vent cutter',(x,y,.573),(.019,.014,.019),BLACK,.003);o.rotation_euler[2]=a;cut(front,o)
 o=box('muzzle vent floor',(x*.94,.08+(y-.08)*.94,.573),(.003,.010,.020),BLACK,.002);o.rotation_euler[2]=a
ring('muzzle lip machined edge',.597,.007,.104,STEEL)
for side in (-1,1):
 # Small physical engravings on a recessed ceramic identification pad.
 label('A / PULSE MK II',(side*.101,.153,-.085),.007,DARK,side)
 label('CAUTION  //  HOT',(side*.113,.035,.335),.004,DARK,side)
 profile('orange collar identity delta',[(.067,.16),(.093,.16),(.080,.183)],.001,WHITE,x=side*.124,bevel=.0002)
# Ergonomic grip profile fits the original right-hand contact frame.
profile('rear grip structural spine',[(-.024,-.058),(-.038,.025),(-.18,.002),(-.213,-.018),(-.205,-.063),(-.17,-.073)],.029,DARK,bevel=.006)
for s in (-1,1):
 profile('rear grip ceramic frame',[(-.05,-.058),(-.057,.017),(-.18,-.006),(-.207,-.019),(-.200,-.059),(-.178,-.068)],.0035,WHITE,x=s*.030,bevel=.002)
 profile('rear grip rubber inlay',[(-.064,-.049),(-.068,.004),(-.177,-.016),(-.189,-.028),(-.184,-.051),(-.163,-.058)],.0025,BLACK,x=s*.034,bevel=.002)
 for i in range(11):
  y=-.075-i*.009;z=-.022+(y+.075)*.15
  o=box('rear grip diagonal tread',(s*.037,y,z),(.0015,.0014,.035),DARK,.0004);o.rotation_euler[0]=.3
 bolt('rear heel pin',(s*.034,-.197,-.035),r=.0035)
# Fine cross-hatching is true geometry on rubber inserts, not painted stripes.
for side in (-1,1):
 for i in range(10):
  o=box('rear grip cross tread',(side*.038,-.08-i*.009,-.027),(.001,.001,.027),DARK,.0003);o.rotation_euler[0]=-.45
# Front support grip: original contact dimensions, slight ergonomic swell.
profile('support grip core',[(-.018,.318),(-.018,.38),(-.16,.38),(-.194,.366),(-.194,.322),(-.163,.314)],.025,BLACK,bevel=.006)
for s in (-1,1):
 profile('support ceramic spine',[(-.027,.319),(-.027,.333),(-.162,.331),(-.18,.35),(-.18,.37),(-.195,.37),(-.195,.32),(-.162,.312)],.003,WHITE,x=s*.026,bevel=.002)
 for i in range(13):
  o=box('support inset diagonal tread',(s*.029,-.047-i*.009,.355),(.0013,.0014,.032),DARK,.0004);o.rotation_euler[0]=-.3
 bolt('support heel pin',(s*.03,-.18,.332),r=.0035)
box('support mount',(0,-.015,.35),(.078,.027,.092),DARK,.005)
for s in (-1,1):bolt('support hinge bolt',(s*.041,-.016,.35),r=.006)
for side in (-1,1):
 for i in range(12):
  o=box('support cross tread',(side*.030,-.052-i*.009,.354),(.001,.001,.025),DARK,.0003);o.rotation_euler[0]=.45
# Open trigger guard, separate trigger; no solid proxy inside hand opening.
profile('open guard',[(-.032,.025),(-.027,.100),(-.044,.112),(-.121,.098),(-.137,.075),(-.127,.023),(-.113,.024),(-.119,.073),(-.109,.084),(-.045,.096),(-.043,.026)],.023,DARK,bevel=.002)
profile('orange trigger',[(-.037,.049),(-.064,.043),(-.089,.059),(-.084,.067),(-.059,.058),(-.038,.06)],.006,ORANGE,bevel=.0015)
for s in (-1,1):
 for z in (.28,.36,.42):box('vent pocket',(s*.096,.004,z),(.007,.013,.033),BLACK,.002)
# Fit the authored grip volume to the native glove cavity, preserving wrist/arm pose.
# Dimensions come from the captured RocketLauncher skin, not gameplay tuning.
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 rear=o.name.startswith(('rear grip','rear heel'))
 support=o.name.startswith(('support grip','support ceramic','support inset','support cross','support heel'))
 if not (rear or support):continue
 matrix=o.matrix_world.copy();inverse=matrix.inverted();center=-.018 if rear else .35
 for v in o.data.vertices:
  q=matrix@v.co;q.z=center+(q.z-center)*(1.65 if rear else 1.6)
  if support:q.x*=1.12
  v.co=inverse@q
mark('pulse-grip',(0,-.09,0));mark('pulse-muzzle',(0,.08,.601));mark('pulse-support-grip',(0,-.1,.35))
export('pulse-mk2-master',[('pulse-lod0','pulse-launcher-lod0.glb'),('pulse-lod1','pulse-launcher-lod1.glb')])
# Projectile normalized geometry retains length/radius contract of RocketPresentation.
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
lathe('projectile graphite',[(-.46,.17),(-.41,.22),(.12,.22),(.17,.20)],DARK,center=(0,0))
lathe('ceramic ogive',[(.11,.218),(.19,.214),(.28,.192),(.36,.152),(.43,.092),(.485,.025),(.5,.004)],WHITE,center=(0,0))
for z in (-.18,-.11):
 lathe('projectile collar',[(z-.02,.219),(z-.015,.232),(z+.015,.232),(z+.02,.219)],ORANGE,center=(0,0))
lathe('recessed exhaust nozzle',[(-.50,.16),(-.47,.19),(-.42,.18),(-.42,.115),(-.49,.113),(-.50,.125)],STEEL,center=(0,0),closed=True)
lathe('nozzle inner shadow',[(-.485,.112),(-.34,.105)],BLACK,center=(0,0))
for z in (-.34,-.25,.06):
 lathe('body panel seam',[(z-.004,.221),(z+.004,.221)],BLACK,center=(0,0))
for i in range(4):
 a=i*math.pi/2
 fin=profile('swept ceramic stabilizer',[(.17,-.39),(.34,-.46),(.35,-.43),(.29,-.16),(.20,-.08)],.014,DARK,bevel=.004);fin.rotation_euler[2]=a
 p=profile('stabilizer graphite leading edge',[(.31,-.43),(.341,-.446),(.291,-.17),(.272,-.15)],.016,STEEL,bevel=.002);p.rotation_euler[2]=a
 x,y=.222*math.cos(a),.222*math.sin(a)
 o=box('projectile service slit',(x,y,-.285),(.005,.027,.072),BLACK,.004);o.rotation_euler[2]=a
# Ogive panel joins trace the curved ceramic surface; fine flush attachment details.
for angle in range(0,360,60):
 a=math.radians(angle);curve=bpy.data.curves.new('ogive panel join','CURVE');curve.dimensions='3D';curve.bevel_depth=.0012;curve.bevel_resolution=2
 spline=curve.splines.new('POLY');rings=[(.14,.218),(.19,.215),(.28,.193),(.36,.153),(.43,.093)];spline.points.add(len(rings)-1)
 for point,(z,r) in zip(spline.points,rings):point.co=(r*math.cos(a),r*math.sin(a),z,1)
 obj=bpy.data.objects.new('ceramic longitudinal seam',curve);bpy.context.collection.objects.link(obj);obj.data.materials.append(BLACK);bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj;bpy.ops.object.convert(target='MESH')
 for z,r in [(.16,.219),(-.13,.233)]:
  bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,radius=.005,location=(r*math.cos(a),r*math.sin(a),z));finish(bpy.context.object,STEEL)
mark('pulse-projectile-nozzle',(0,0,-.49))
export('pulse-mk2-projectile-master',[('pulse-projectile','pulse-projectile.glb')])
