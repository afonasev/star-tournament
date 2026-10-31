"""FORGE concept 01 with F4 open rear reactor, detailed authored source. Blender --background --python this_file.
All coordinates authored Y-up / Z-forward, grip origin; source retains separate parts.
Export batches rigid geometry by material WITHOUT reducing geometry or detail.
"""
import bpy, math, json, struct, hashlib, random
from pathlib import Path
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
OUT=ROOT/'.local/forge';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'assets/source/forge';SOURCE.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
random.seed(31)
palette={}

def texture(name,kind):
 size=512;image=bpy.data.images.new(name,width=size,height=size);pixels=[]
 rng=random.Random(716)
 for y in range(size):
  for x in range(size):
   u=x/size;v=y/size;n=rng.random()
   if kind=='reactor':
    px=(u-.5)*2;py=(v-.5)*2;radius=math.hypot(px,py);theta=math.atan2(py,px)
    # Polar flow with branching filaments, authored for the actual circular lens UV.
    turbulence=.55*math.sin(theta*5-radius*19)+.23*math.sin(theta*11+radius*31)
    flow=theta*4+radius*12+turbulence
    filament=max(0,1-abs(math.sin(flow))*6)**3
    secondary=max(0,1-abs(math.sin(theta*7-radius*17+turbulence))*12)**4*.32
    cloud=(.5+.5*math.sin(theta*3+radius*13))*(.5+.5*math.cos(theta*5-radius*9))
    envelope=max(0,1-radius**3);hot=min(1,(filament+secondary)*envelope+math.exp(-radius*radius*180))
    c=(.006+.5*hot,.025+.16*cloud*envelope+.78*hot,.002+.16*hot)
   elif kind=='plasma':
    # Seamless cylindrical plasma: branching hot filaments over darker turbulent depth.
    warp=math.sin(v*21+u*math.tau*2)+.35*math.sin(v*49-u*math.tau*3)
    wave=math.sin(u*math.tau*5+warp*1.5)
    secondary=math.sin(u*math.tau*11+v*37+warp*2)
    vein=max(0,1-abs(wave)*7)**3
    branch=max(0,1-abs(secondary)*12)**4*.35
    cloud=(.5+.5*math.sin(u*math.tau*3+v*14+warp))*(.5+.5*math.cos(u*math.tau*2-v*9))
    energy=min(1,vein+branch)
    c=(.008+.68*energy,.06+.27*cloud+.66*energy,.003+.28*energy)
   else:
    base=(.66,.65,.59) if kind=='ceramic' else (.075,.086,.088)
    scratch=(n>.996 and (x%19)<12)
    grain=(n-.5)*(.038 if kind=='ceramic' else .014)
    c=tuple(max(0,min(1,b+grain+(.09 if scratch else 0))) for b in base)
   pixels.extend((*c,1))
 image.pixels=pixels;image.filepath_raw=str(SOURCE/(name+'.png'));image.file_format='PNG';image.save();image.pack();return image

maps={k:texture('forge-'+k,k) for k in ('ceramic','metal','plasma','reactor')}
def mat(name,color,metal=.5,rough=.34,tex=None,emission=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=(*color,1)
 b.inputs['Metallic'].default_value=metal;b.inputs['Roughness'].default_value=rough
 if tex:
  t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=maps[tex];m.node_tree.links.new(t.outputs['Color'],b.inputs['Base Color'])
 if emission:
  b.inputs['Emission Color'].default_value=(*color,1);b.inputs['Emission Strength'].default_value=emission
  if tex:m.node_tree.links.new(t.outputs['Color'],b.inputs['Emission Color'])
 palette[name]={'baseColorFactor':([1,1,1,1] if tex else [*color,1]),'metallicFactor':metal,'roughnessFactor':rough}
 return m
ivory=mat('FORGE ceramic armor',(.66,.65,.59),.25,.36,'ceramic')
dark=mat('FORGE graphite titanium',(.075,.086,.088),.8,.32,'metal')
steel=mat('FORGE edge machined steel',(.23,.26,.26),.85,.27)
black=mat('FORGE rubber and bore',(.012,.016,.018),.05,.66)
copper=mat('FORGE bronze conductors',(.24,.13,.055),.85,.3)
green=mat('FORGE active plasma',(.02,.7,.006),.1,.25,'plasma',1.4)
led=mat('FORGE indicator',(.11,.8,.015),.25,.3,None,1.1)
reactor=mat('FORGE contained reactor',(.06,.72,.008),.18,.18,'reactor',1.8)
flux=mat('FORGE reactor flux',(.12,.8,.025),.1,.3,None,1.2)
ink=mat('FORGE markings',(.52,.56,.52),.25,.5)

def finish(o,name,material,bevel=.003):
 o.name=name;o.data.materials.append(material)
 if bevel:
  mod=o.modifiers.new('authored edge radius','BEVEL');mod.width=bevel;mod.segments=3
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
  mod=o.modifiers.new('weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=45
  bpy.ops.object.modifier_apply(modifier=mod.name)
 return o

def box(name,pos,size,material,bevel=.003):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return finish(o,name,material,bevel)

def panel(name,x,thickness,outline,material,bevel=.004):
 # Contoured armor in the longitudinal (Z,Y) plane, extruded across width.
 verts=[(xx,y,z) for xx in (x-thickness/2,x+thickness/2) for z,y in outline];n=len(outline)
 faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 return finish(o,name,material,bevel)

def ring(name,z,r,inner,length,material,y=.1,x=0,n=96):
 verts=[]
 for zz,rr in ((z-length/2,r),(z+length/2,r),(z-length/2,inner),(z+length/2,inner)):
  for i in range(n):a=i*math.tau/n;verts.append((x+math.cos(a)*rr,y+math.sin(a)*rr,zz))
 faces=[]
 for i in range(n):j=(i+1)%n;faces += [(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(j,i,2*n+i,2*n+j),(n+i,n+j,3*n+j,3*n+i)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 finish(o,name,material,.0012)
 for f in o.data.polygons:f.use_smooth=True
 if material==green:
  # Keep one continuous cylindrical unwrap; generic smart projection creates visible stripes.
  uv=o.data.uv_layers.new(name='Plasma cylindrical UV');o['authored_uv']=True
  for face in o.data.polygons:
   coords=[]
   for index in face.loop_indices:
    v=o.data.vertices[o.data.loops[index].vertex_index].co
    coords.append((math.atan2(v.y-y,v.x-x)/math.tau+.5,(v.z-z)/length+.5))
   wrap=max(a for a,b in coords)-min(a for a,b in coords)>.5
   for index,(u,v) in zip(face.loop_indices,coords):uv.data[index].uv=(u+1 if wrap and u<.5 else u,v)
 return o

def rod(name,a,b,r,material,vertices=24):
 a,b=Vector(a),Vector(b);d=b-a
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(a+b)/2)
 o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();finish(o,name,material,.0008)
 for f in o.data.polygons:f.use_smooth=True
 return o

def wire(name,points,r,material):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=r;c.bevel_resolution=3
 s=c.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
 for b,co in zip(s.bezier_points,points):b.co=co;b.handle_left_type='AUTO';b.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');return bpy.context.object

def bolt(x,y,z,side):
 rod('recessed socket seat',(x-side*.001,y,z),(x+side*.003,y,z),.0105,black)
 rod('hex socket screw',(x+side*.003,y,z),(x+side*.007,y,z),.007,steel,6)
 rod('socket recess',(x+side*.007,y,z),(x+side*.0075,y,z),.0032,black,6)

def text_mark(text,pos,size,side=1):
 bpy.ops.object.text_add(location=pos);o=bpy.context.object;o.name='etched '+text;o.data.body=text;o.data.size=size;o.data.extrude=.00015;o.data.align_x='CENTER'
 # text local X along barrel, local Y upwards, normal outward
 o.rotation_euler=Matrix(((0,0,side),(0,1,0),(-side,0,0))).to_4x4().to_euler();o.data.materials.append(ink)
 bpy.ops.object.convert(target='MESH');return bpy.context.object

# Main monocoque and receiver: major shapes first, visible recesses instead of slab stack.
panel('continuous dark chassis',0,.21,[(-.37,-.03),(-.38,.17),(-.28,.25),(-.12,.28),(.29,.27),(.41,.20),(.42,-.04),(.29,-.105),(-.17,-.105),(-.30,-.08)],dark,.012)
# Expose both cylinders by replacing the center with slender structural rails; center chassis only at rear.
bpy.data.objects.remove(bpy.data.objects['continuous dark chassis'],do_unlink=True)
panel('forged rear receiver',0,.225,[(-.37,-.03),(-.39,.16),(-.30,.25),(-.17,.26),(-.105,.19),(-.10,-.08),(-.28,-.105)],dark,.012)
panel('lower sculpted spine',0,.19,[(-.31,-.035),(.36,-.035),(.41,-.07),(.30,-.13),(.03,-.14),(-.21,-.11)],dark,.008)
panel('upper sculpted spine',0,.13,[(-.35,.23),(-.25,.295),(.27,.295),(.37,.24),(.25,.23),(-.12,.25)],dark,.008)
# Two protected plasma chambers, layered end seals, silver ferrules, bronze coil ribs.
for y in (.035,.185):
 ring('plasma chamber',.055,.061,.001,.34,green,y)
 for z in (-.133,.245):
  ring('capsule rubber seal',z,.071,.058,.025,black,y)
  ring('capsule titanium coupling',z+.014,.078,.06,.025,steel,y)
  ring('capsule shoulder',z-.015,.08,.061,.019,dark,y)
  for dz in (-.021,-.012,0):ring('induction winding',z+dz,.0805,.075,.0028,copper,y,n=64)
 for side in (-1,1):
  wire('capsule protective rail',[(side*.055,y+.047,-.11),(side*.067,y+.048,.02),(side*.067,y+.048,.15),(side*.055,y+.047,.22)],.0055,steel)
# Segmented contoured shells. Negative space shows the capsules; panel silhouette matches concept 01.
for side in (-1,1):
 x=side*.128
 panel('rear armor gasket',x,.041,[(-.37,.02),(-.35,.22),(-.25,.285),(-.135,.25),(-.105,.18),(-.12,-.03),(-.22,-.11),(-.34,-.07)],black,.008)
 panel('rear ceramic shell',x+side*.015,.034,[(-.365,.04),(-.335,.215),(-.248,.266),(-.15,.235),(-.128,.173),(-.14,-.018),(-.225,-.085),(-.325,-.054)],ivory,.006)
 panel('front armor gasket',side*.139,.045,[(.20,-.08),(.18,.02),(.205,.23),(.255,.292),(.335,.265),(.388,.18),(.39,.025),(.34,-.095),(.25,-.13)],black,.008)
 panel('front ceramic collar',side*.155,.038,[(.222,-.055),(.207,.025),(.23,.225),(.267,.27),(.321,.246),(.367,.17),(.369,.03),(.325,-.073),(.255,-.105)],ivory,.005)
 panel('capsule bridge armor',side*.084,.037,[(-.20,.28),(-.13,.315),(.16,.315),(.245,.275),(.195,.265),(-.12,.285)],ivory,.006)
 panel('lower ceramic keel',side*.10,.026,[(-.10,-.125),(.15,-.145),(.24,-.113),(.18,-.09),(-.065,-.087)],ivory,.004)
 # Recessed rear service module, segmented cooling vanes and visible inset hardware.
 panel('service recess',side*.166,.012,[(-.317,.015),(-.316,.16),(-.266,.192),(-.18,.16),(-.18,.015),(-.24,-.035)],dark,.005)
 for j in range(5):
  z=-.30+j*.023
  box('radiator channel',(side*.176,.078,z),(.008,.089,.015),black,.003)
  box('radiator fin',(side*.181,.084,z-.003),(.008,.073,.004),steel,.001)
 for z,y in [(-.335,.18),(-.32,-.03),(-.17,.196),(-.19,-.045),(.246,.23),(.341,.17),(.344,.029),(.267,-.075)]:bolt(side*(.177 if z<0 else .183),y,z,side)
 for z in (-.09,-.06,-.03,.0,.03,.06,.09):
  box('lower dissipator',(side*.113,-.105,z),(.018,.014,.013),steel,.0015)
 wire('rear power conduit',[(side*.103,-.055,-.31),(side*.117,-.11,-.27),(side*.13,-.135,-.18),(side*.104,-.15,-.08)],.008,black)
 wire('conduit bronze connector',[(side*.105,-.055,-.31),(side*.115,-.078,-.30)],.010,copper)
 box('status bezel',(side*.172,.204,-.26),(.009,.026,.066),dark,.003)
 box('status green bar',(side*.178,.204,-.26),(.004,.007,.046),led,.001)
 text_mark('FORGE', (side*.18,.012,-.26),.018,side)
 text_mark('F-01', (side*.182,.21,.30),.013,side)
 # Recessed front service pocket with nested ceramic shoulder and charge monitor.
 panel('front armor inset',side*.180,.008,[(.247,.00),(.243,.145),(.263,.184),(.315,.166),(.340,.12),(.333,.015),(.301,-.034)],dark,.004)
 panel('front inset ceramic relief',side*.185,.006,[(.25,.01),(.248,.07),(.304,.072),(.331,.043),(.325,.014),(.30,-.024)],ivory,.002)
 for j in range(4):
  box('front charge cell',(side*.186,.112,.263+j*.017),(.004,.030,.008),black,.001)
  box('front charge led',(side*.189,.119,.263+j*.017),(.002,.013,.004),led,.0006)
 wire('front protective bevel',[(side*.186,.166,.266),(side*.187,.15,.312),(side*.187,.124,.334)],.003,steel)
 for z in (-.11,.23):
  for yy in (.035,.185):
   bolt(side*.079,yy,z,side)
 # Segmented muzzle adapter fins, each visibly independent.
 for z in (.365,.388,.411):
  panel('front heat fin',side*.125,.024,[(z,.24),(z+.012,.215),(z+.012,-.015),(z,-.04)],steel,.001)
# Top identity spine and detailed rear cap, shaped to be worth seeing in FPS.
for z in (-.23,-.10,.03,.16):
 box('top ceramic segment',(0,.309,z),(.125,.025,.105),ivory,.006)
 box('top inset slot',(0,.324,z),(.067,.004,.064),dark,.002)
 for x in (-.047,.047):box('machined spine bevel',(x,.326,z),(.008,.004,.082),steel,.001)
# F4 rear reactor: a recessed inspection chamber, rather than another gun bore.
# All details stay within the original weapon envelope and away from grip contacts.
ring('reactor housing',-.390,.150,.105,.071,dark,y=.10)
ring('reactor ceramic mantle',-.399,.160,.139,.042,ivory,y=.10)
ring('reactor outer isolation seal',-.416,.162,.149,.013,black,y=.10)
ring('reactor machined bezel',-.423,.149,.126,.015,steel,y=.10)
ring('reactor bezel engraving',-.432,.141,.137,.001,black,y=.10)
ring('reactor inset shoulder',-.410,.127,.108,.025,dark,y=.10)
ring('reactor inner race',-.416,.113,.107,.008,steel,y=.10)
ring('reactor green inner trace',-.410,.106,.103,.004,led,y=.10)
ring('reactor bronze field cage',-.384,.108,.096,.010,copper,y=.10)
ring('reactor cavity backing',-.368,.104,.001,.010,black,y=.10)
# A curved, UV-mapped energy lens supplies depth and the contained swirl.
verts=[(0,.10,-.402)];uvs=[(.5,.5)];segments=96;bands=12;faces=[]
for band in range(1,bands+1):
 r=.099*band/bands
 for i in range(segments):
  a=i*math.tau/segments
  verts.append((math.cos(a)*r,.10+math.sin(a)*r,-.402+.018*(r/.099)**2))
  uvs.append((.5+math.cos(a)*r/.198,.5+math.sin(a)*r/.198))
for i in range(segments):faces.append((0,1+(i+1)%segments,1+i))
for band in range(bands-1):
 for i in range(segments):
  a=1+band*segments+i;b=1+band*segments+(i+1)%segments
  faces.append((a,b,b+segments,a+segments))
mesh=bpy.data.meshes.new('reactor energy lens');mesh.from_pydata(verts,[],faces);mesh.update()
o=bpy.data.objects.new('reactor energy lens',mesh);bpy.context.collection.objects.link(o);finish(o,o.name,reactor,0);o['authored_uv']=True
uv=mesh.uv_layers.new(name='Reactor radial UV')
for face in mesh.polygons:
 face.use_smooth=True
 for li in face.loop_indices:uv.data[li].uv=uvs[mesh.loops[li].vertex_index]
# Fine three-dimensional flux ribbons sit in front of the lens, contained inside the rim.
for arm in range(6):
 points=[]
 for j in range(34):
  t=j/33;r=.012+.074*t;a=arm*math.tau/6+t*2.5+.14*math.sin(t*13+arm)
  points.append((math.cos(a)*r,.10+math.sin(a)*r,-.405+.012*t*t))
 wire('contained flux filament',points,.00085,flux)
# Three radial clamps with separate pads and indicators read as a sealed power core.
for i in range(3):
 a=math.pi/6+i*math.tau/3;x=math.cos(a);y=math.sin(a)
 o=box('reactor retention bridge',(x*.132,.10+y*.132,-.421),(.065,.030,.018),dark,.004);o.rotation_euler.z=a
 o=box('reactor ceramic clamp',(x*.151,.10+y*.151,-.413),(.029,.038,.022),ivory,.004);o.rotation_euler.z=a
 o=box('reactor clamp indicator',(x*.127,.10+y*.127,-.431),(.021,.006,.002),led,.0008);o.rotation_euler.z=a
for i in range(12):
 a=i*math.tau/12
 rod('reactor recessed fastener',(math.cos(a)*.146,.10+math.sin(a)*.146,-.429),(math.cos(a)*.146,.10+math.sin(a)*.146,-.432),.003,black,6)
# Twin short top power feeds and cooling gills connect the chamber to the original spine.
for side in (-1,1):
 wire('reactor armored feed',[(side*.09,.225,-.367),(side*.086,.251,-.31),(side*.076,.278,-.25),(side*.06,.287,-.19)],.006,black)
 for j in range(4):
  box('rear shoulder cooling fin',(side*.097,.242+j*.010,-.29+j*.016),(.037,.005,.019),steel,.001)
# Deep concentric emitter. Recess is physically open, focus small and well inside muzzle.
ring('emitter rear body',.405,.174,.118,.12,dark)
ring('emitter ceramic armor',.457,.184,.142,.075,ivory)
ring('emitter titanium rim',.501,.181,.139,.026,steel)
ring('emitter outer gasket',.483,.187,.177,.013,black)
ring('emitter inner bore',.43,.14,.12,.14,black)
ring('inner focusing ring',.447,.127,.107,.012,copper)
ring('green focusing ring',.454,.114,.104,.009,led)
ring('recessed throat',.385,.111,.048,.024,dark)
ring('plasma aperture',.397,.049,.001,.008,green)
for i in range(12):
 a=i*math.tau/12;x=math.cos(a);y=math.sin(a)
 rod('front recessed bolt',(x*.16,.1+y*.16,.514),(x*.16,.1+y*.16,.52),.005,black,6)
 o=box('emitter radial latch',(x*.181,.1+y*.181,.455),(.022,.014,.043),dark,.003);o.rotation_euler.z=a-math.pi/2
# Pistol grip and angled support grip share actual legacy contact centers; sculpted bellies and wrap ribs.
for name,z,y,height,angle in [('primary',-.215,-.325,.30,-.16),('support',.27,-.222,.23,-.12)]:
 pivot=Vector((0,y,z));parts=[]
 parts.append(panel(name+' ergonomic grip',0,.092,[(z-.046,y+height*.48),(z+.042,y+height*.48),(z+.049,y+height*.20),(z+.036,y-height*.4),(z-.015,y-height*.53),(z-.044,y-height*.30)],black,.012))
 for side in (-1,1):
  parts.append(panel(name+' inset rubber pad',side*.047,.006,[(z-.028,y+height*.34),(z+.025,y+height*.32),(z+.029,y-height*.3),(z-.015,y-height*.39),(z-.028,y-height*.24)],dark,.003))
  for j in range(11):
   yy=y-height*.3+j*height*.057
   parts.append(box(name+' grip traction',(side*.051,yy,z),(.004,.0035,.045),black,.001))
  # knurled side stipple, actual small embossed relief
  for j in range(9):
   for k in range(4):
    parts.append(box(name+' grip checkering',(side*.052,y-height*.27+j*height*.06,z-.017+k*.011),(.0015,.003,.003),black,.0003))
 parts.append(box(name+' grip cap',(0,y-height*.53,z-.003),(.108,.024,.122),steel,.007))
 for o in parts:
  # Grip depth follows the real glove span, including the opposing thumb pad.
  depth=1.8 if name=='primary' else 1.55
  o.matrix_world=Matrix.Translation(pivot)@Matrix.Diagonal((1,1,depth,1))@Matrix.Translation(-pivot)@o.matrix_world
  o.matrix_world=Matrix.Translation(pivot)@Matrix.Rotation(angle,4,'X')@Matrix.Translation(-pivot)@o.matrix_world
box('primary grip collar',(0,-.15,-.17),(.095,.11,.12),dark,.009)
box('support grip saddle',(0,-.112,.27),(.10,.05,.15),dark,.008)
# Rounded guard follows trigger opening, no solid box obstruction.
wire('trigger protective loop',[(.0,-.14,-.12),(.0,-.16,-.015),(.0,-.27,.012),(.0,-.32,-.045),(.0,-.33,-.13)],.010,dark)
wire('curved trigger',[(0,-.18,-.09),(0,-.235,-.10),(0,-.25,-.125)],.006,steel)
# Global grip origin follows existing mount contract.
for o in list(bpy.context.scene.objects):o.location.y+=.105;o.location.z+=.19
# UV projection per part before batching, maps remain standard glTF image textures.
for o in list(bpy.context.scene.objects):
 if o.type!='MESH' or o.get('authored_uv'):continue
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.012);bpy.ops.object.mode_set(mode='OBJECT')
for name,pos in [('forge-grip',(0,-.09,0)),('forge-muzzle',(0,.205,.716)),('forge-support-grip',(0,-.065,.46))]:
 bpy.ops.object.empty_add(location=pos);bpy.context.object.name=name
# Digit-joint contact targets in the exported grip frame (Unity reflects X).
# These are rig fitting anchors, not collision shapes. Each point includes glove pad clearance.
contacts={
 'left':[(.052,-.015,.515),(.050,-.075,.515),(.050,-.168,.515),(.048,-.225,.510),(-.005,-.031,.360)],
 'right':[(.006,-.13,.065),(-.015,-.20,.073),(-.010,-.275,.060),(-.015,-.325,.040),(.0,-.136,-.132)]}
for side,points in contacts.items():
 for digit,pos in zip(('Index','Middle','Ring','Little','Thumb'),points):
  bpy.ops.object.empty_add(location=(-pos[0],pos[1],pos[2]));bpy.context.object.name='forge-contact-'+side+'-'+digit
axis=Matrix.Rotation(math.pi/2,4,'X')
for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world
# Editable, unjoined high-detail master retained before non-destructive export batching.
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'forge.blend'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
triangles=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons);before=len(meshes)
groups={}
for o in meshes:groups.setdefault(o.data.materials[0].name,[]).append(o)
for name,objects in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=name+' geometry'
bpy.ops.object.select_all(action='SELECT')
# Keep detailed geometry in both slots for this art-first iteration. No visual decimation.
for lod in (0,1):
 path=ART/f'forge-plasma-lod{lod}.glb'
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True)
 data=path.read_bytes();length=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+length]);tail=data[20+length:]
 for material in doc['materials']:
  pbr=material.setdefault('pbrMetallicRoughness',{});pbr.update(palette[material['name']])
 payload=json.dumps(doc,separators=(',',':')).encode();payload+=b' '*((-len(payload))%4)
 path.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)
manifest=ART/'asset-manifest.json';doc=json.loads(manifest.read_text())
for entry in doc['entries']:
 if entry['key'] in ('forge-lod0','forge-lod1'):
  path=ART/f"forge-plasma-lod{entry['lod']}.glb";entry['sha256']=hashlib.sha256(path.read_bytes()).hexdigest();entry['source']='Detailed FORGE concept 01 + F5 animated open rear reactor (isolated flux material); scripts/trooper/build_forge.py; editable assets/source/forge/forge.blend; full detail preserved in both LOD slots per art-first approval.'
manifest.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n')
report={'triangles_before':triangles,'triangles_after':sum(len(p.vertices)-2 for o in bpy.context.scene.objects if o.type=='MESH' for p in o.data.polygons),'renderers_before':before,'renderers_after':len(groups),'materials':len(groups),'textures':4,'texture_size':512,'lod_policy':'both full detail; decimation explicitly deferred','geometry_preserved':True}
(OUT/'asset-metrics.json').write_text(json.dumps(report,indent=2));print(report)
