"""Authored A silhouette and mechanical shell, evaluated by build.py before grips.
Coordinates stay in the shared Pulse frame. All cavities are actual geometry.
"""
def radial_box(name,a,z,r,size,mat,bevel=.001):
 o=box(name,(r*math.cos(a),.08+r*math.sin(a),z),size,mat,bevel);o.rotation_euler[2]=a;return o

def panel(name,poly,side,x,depth,mat,bevel=.002):
 return profile(name,poly,depth,mat,x=side*x,bevel=bevel)

# The barrel is a machined pressure vessel. Its narrow collars and tapered stages
# remain visible through the ceramic exoskeleton instead of becoming black voids.
lathe('graphite launch chamber',[(-.205,.081),(-.175,.096),(-.08,.100),(.10,.098),(.225,.096),(.48,.091),(.575,.092),(.600,.089),(.601,.077),(.26,.077),(.25,.081),(-.205,.081)],DARK,closed=True)
for za,zb,r in [(.29,.48,.096),(-.03,.19,.102),(-.17,-.11,.104)]:
 lathe('pressure vessel turned sleeve',[(za,r-.004),(za+.004,r),(zb-.004,r),(zb,r-.004)],DARK)
 for z in (za+.010,zb-.009):ring('barrel circumferential joint',z,.002,r+.001,BLACK)
 for a in (0,math.pi):
  radial_box('pressure vessel longitudinal weld',a,(za+zb)/2,r+.0005,(.0012,.001,(zb-za)*.77),STEEL,.0003)

# Sculpted ceramic shells. Variable radii, chamfered apertures, sloping ends and
# edge-lining make the large form structural rather than a perfect toy cylinder.
for index,za,zb in [(0,.267,.504),(1,-.074,.212)]:
 rings=[(za,.109),(za+.009,.121),(za+.030,.124),(za+.085,.113),(zb-.060,.110),(zb-.019,.118),(zb-.006,.120),(zb,.111),(zb,.100),(za,.100)]
 shell=lathe('ceramic exoskeleton '+str(index),rings,WHITE,n=96,closed=True)
 # Match the asymmetry in the concept: long narrow forward window, wider rear
 # shoulder with an actual service well beneath its upper bridge.
 window=[(.037,za+.033),(.126,za+.021),(.151,za+.056),(.143,zb-.033),(.118,zb-.015),(.057,zb-.029)]
 for side in (-1,1):
  cut(shell,panel('through window cutter',window,side,.105,.060,BLACK,.006))
  # Tapered graphite liner follows the aperture, with the ceramic lip above it.
  upper=[(.139,za+.049),(.152,za+.058),(.144,zb-.037),(.131,zb-.030),(.132,zb-.051),(.140,za+.064)]
  panel('inset window upper gasket',upper,side,.094,.004,BLACK,.001)
  lower=[(.031,za+.035),(.041,za+.041),(.058,zb-.035),(.052,zb-.027),(.036,zb-.046)]
  panel('inset window lower gasket',lower,side,.099,.003,BLACK,.001)
  # Swept lower load-bearing cheek, not a rectangular applied plate.
  outer=[(.048,za+.018),(.031,za+.013),(-.005,za+.047),(-.021,za+.080),(-.015,zb-.065),(.010,zb-.030),(.050,zb-.012),(.060,zb-.032),(.030,zb-.061),(.012,za+.086),(.041,za+.050)]
  cheek=panel('ceramic swept lower cheek',outer,side,.092,.010,WHITE,.0025)
  vent=[(-.008,za+.079),(-.001,za+.069),(.011,za+.082),(.023,zb-.063),(.016,zb-.049),(.005,zb-.068)]
  cut(cheek,panel('lower recessed vent cutter',vent,side,.097,.030,BLACK,.002))
  panel('lower channel recessed floor',vent,side,.082,.002,BLACK,.001)
  for z in (za+.066,zb-.055):
   bolt('ceramic countersunk fastener',(side*.104,.018,z),r=.003)
  # Real diagonal seam/recesses on broad upper shoulders.
  for z in (za+.027,zb-.026):
   cut(shell,panel('upper service keyway cutter',[(.158,z-.008),(.173,z-.008),(.179,z+.016),(.168,z+.019)],side,.080,.028,BLACK,.002))
   panel('keyway dark insert',[(.162,z-.007),(.172,z-.007),(.175,z+.013),(.167,z+.015)],side,.069,.003,BLACK,.001)
 # Small top aperture and offset bridge divide the broad ceramic surface.
 cut(shell,box('top skeleton aperture',(0,.194,(za+zb)/2),(.061,.065,(zb-za)*.60),BLACK,.008))
 for side in (-1,1):
  panel('upper shell panel seam',[(.183,za+.047),(.190,za+.055),(.183,zb-.048),(.178,zb-.039),(.177,zb-.041),(.181,zb-.050),(.188,za+.056),(.181,za+.048)],side,.053,.0008,BLACK,.0003)

# Three heavy locking bands, each broken into structural segments and recessed
# fasteners. Recessed gaps expose dark metal and retain the orange visual identity.
for z,w,r in [(.536,.053,.119),(.239,.041,.123),(-.100,.044,.122)]:
 ring('collar backing sleeve',z,w+.014,r-.004,DARK)
 collar=ring('orange bayonet band',z,w,r,ORANGE)
 for edge in (-1,1):ring('collar steel retaining bead',z+edge*(w/2+.001),.003,r-.002,STEEL)
 for i in range(8):
  a=i*math.tau/8
  cut(collar,radial_box('band deep pocket',a,z,r,(.018,.009,w*.49),BLACK,.002))
  radial_box('band pocket base',a,z,r-.010,(.002,.008,w*.48),BLACK,.001)
  # Surface seams have a central step that reads as removable locking sectors.
  for dz in (-.32,.32):radial_box('band recessed panel join',a,z+w*dz,r+.0001,(.001,.041,.0008),BLACK,.0002)
  a+=math.tau/16
  radial_box('band sunk screw pad',a,z,r+.0002,(.001,.012,.015),DARK,.002)
  # Radial round pins (local cylinder axis is transformed onto the band normal).
  bpy.ops.mesh.primitive_cylinder_add(vertices=20,radius=.0025,depth=.0015,location=((r+.001)*math.cos(a),.08+(r+.001)*math.sin(a),z))
  o=bpy.context.object;o.name='band captive torx';o.rotation_euler=(Vector((math.cos(a),math.sin(a),0)).to_track_quat('Z','Y').to_euler());finish(o,STEEL,.0003)
 for side in (-1,1):panel('band identity chevron',[(.073,z-.006),(.091,z-.006),(.081,z+.007)],side,r+.001,.0005,WHITE,.0002)

# Layered muzzle: chamfered bare-metal front, recessed throat and separated vents.
front=lathe('machined muzzle sleeve',[(.560,.107),(.568,.116),(.590,.116),(.600,.108),(.604,.099),(.604,.080),(.592,.078),(.566,.078),(.560,.089)],DARK,closed=True)
for i in range(10):
 a=i*math.tau/10
 cut(front,radial_box('muzzle port cutter',a,.583,.115,(.027,.012,.016),BLACK,.002))
 radial_box('muzzle port recessed wall',a,.583,.100,(.003,.010,.018),BLACK,.001)
ring('muzzle machined leading face',.602,.004,.105,STEEL)
ring('inner bore machined lip',.601,.004,.081,STEEL)
ring('inner muzzle shadow joint',.595,.002,.097,BLACK)
for i in range(8):
 a=i*math.tau/8;r=.097
 bolt('front face captive pin',(r*math.cos(a),.08+r*math.sin(a),.605),axis='Z',r=.002)

# Breech ends close behind the shooting hand, as in A, rather than a long drum.
lathe('rear breech pressure closure',[(-.212,.074),(-.205,.092),(-.195,.104),(-.130,.105),(-.121,.108)],DARK)
ring('breech ceramic locking ring',-.185,.021,.108,WHITE)
ring('breech exposed retaining rim',-.205,.009,.099,STEEL)
ring('breech cap dark inset',-.211,.005,.074,BLACK)
for side in (-1,1):
 fork=[(.019,-.193),(.044,-.193),(.050,-.161),(.113,-.150),(.145,-.126),(.168,-.109),(.163,-.070),(.145,-.061),(.136,-.099),(.100,-.121),(.030,-.136)]
 panel('rear ceramic diagonal fork',fork,side,.097,.005,WHITE,.002)
 # Socket rim is a real chamfered opening cut through the lower ceramic receiver.
 poly=[(.001,-.078),(.077,-.078),(.095,-.061),(.094,.040),(.076,.065),(.029,.053),(.002,.018)]
 outer=panel('receiver ceramic well surround',poly,side,.107,.010,WHITE,.003)
 hole=[(.020,-.062),(.067,-.062),(.078,-.050),(.078,.031),(.064,.043),(.038,.035),(.020,.006)]
 cut(outer,panel('receiver well cutter',hole,side,.110,.040,BLACK,.003))
 liner=panel('receiver machined well lip',hole,side,.104,.006,DARK,.002)
 inner=[(.029,-.054),(.060,-.054),(.068,-.044),(.068,.024),(.058,.032),(.042,.024),(.029,.002)]
 cut(liner,panel('receiver lip cutter',inner,side,.105,.025,BLACK,.002))
 panel('receiver deep black cavity',inner,side,.086,.002,BLACK,.001)
 # Receding shelf and inside socket sill are visible from both sides.
 box('well lower ledge',(side*.097,.029,-.009),(.018,.006,.073),STEEL,.001)
 box('well inner channel',(side*.090,.043,-.009),(.004,.006,.051),DARK,.001)
 for y,z in ((.080,-.060),(.074,.041),(.010,-.043)):
  bolt('receiver sunken pin',(side*.118,y,z),r=.0028)
 panel('receiver angled lower steel truss',[(-.004,-.072),(-.030,-.050),(-.044,.006),(-.022,.040),(.014,.051),(.001,.020),(-.018,-.003),(-.010,-.042),(.006,-.056)],side,.089,.007,DARK,.002)
 box('receiver status recess',(side*.106,.070,-.151),(.002,.022,.025),BLACK,.002)
 for sign in (-1,1):
  o=box('cyan check light',(side*.108,.070+sign*.002,-.151+sign*.004),(.001,.002,.010),CYAN,.0005);o.rotation_euler[0]=sign*.65
 for z in (-.165,-.137):bolt('breech fork countersink',(side*.105,.035,z),r=.003)

# Low integrated upper gas/sight housing, interrupted by exposed steel pistons.
profile('upper angled saddle',[(.175,-.084),(.195,-.071),(.204,.033),(.195,.088),(.177,.112)],.033,WHITE,bevel=.002)
lathe('upper mechanism steel core',[(-.075,.019),(-.064,.024),(.075,.024),(.095,.018)],DARK,n=64,center=(0,.210))
lathe('upper ceramic sleeve',[(-.062,.025),(-.053,.030),(.005,.029),(.018,.025)],WHITE,n=64,center=(0,.210))
for z in (-.063,.021,.075):
 lathe('upper machined ring',[(z-.003,.025),(z+.003,.025)],STEEL,n=64,center=(0,.210))
profile('upper sight machined rail',[(.227,-.054),(.241,-.040),(.242,.043),(.231,.068),(.225,.058)],.021,DARK,bevel=.0015)
for z in (-.024,-.005,.014,.033):box('rail cut transverse notch',(0,.242,z),(.044,.003,.006),BLACK,.0006)
for side in (-1,1):
 for z in (-.040,-.014):bolt('sight sleeve pin',(side*.028,.212,z),r=.003)
 # Identity and orange index marks are physical curves, flush on the shell.
 label('A / PULSE MK II',(side*.10,.155,.103),.006,DARK,side)
 label('LOCK  //  02',(side*.10,.035,.402),.003,DARK,side)
# Finish boolean boundaries after the cuts: real bevels catch light on the
# ceramic apertures and reveal the thickness of each machined lip.
for o in list(bpy.context.scene.objects):
 if o.type=='MESH' and o.name.startswith(('ceramic exoskeleton','orange bayonet band','machined muzzle sleeve','receiver ceramic well surround','receiver machined well lip')):
  bpy.context.view_layer.objects.active=o
  b=o.modifiers.new('Post-cut machining bevel','BEVEL');b.width=.0012;b.segments=3;b.limit_method='ANGLE'
  bpy.ops.object.modifier_apply(modifier=b.name)
  n=o.modifiers.new('Finished cut normals','WEIGHTED_NORMAL');n.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=n.name)

# Narrow the radial envelope by 10%, not the semantic frame or hand grips. This
# improves the long/slender proportion without shifting the muzzle or wrist axes.
for o in list(bpy.context.scene.objects):
 if o.type!='MESH':continue
 m=o.matrix_world.copy();inv=m.inverted()
 for v in o.data.vertices:
  q=m@v.co;q.x*=.90;q.y=.08+(q.y-.08)*.90;v.co=inv@q
