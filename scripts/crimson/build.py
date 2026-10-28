"""Crimson authored shoulder launcher. Blender -b -noaudio --python scripts/crimson/build.py.
Metres, +Y up/+Z forward before glTF export. Existing semantic grip frame retained.
Editable source is kept before lossless joining; export has no physics/colliders.
"""
from pathlib import Path
import sys
ROOT=Path(__file__).resolve().parents[2]
legacy=(ROOT/'scripts/pulse_mk2/build.py').read_text()
# Reuse the established procedural material and mesh primitives, never execute its asset build.
exec(legacy.split('DARK=material')[0].replace('size=1024','size=512').replace('range(180)', 'range(18)').replace('range(420)', 'range(100)').replace('*=.45', '*=.82').replace("ROOT/'.local/pulse-mk2'","ROOT/'.local/crimson'"))
exec(legacy[legacy.index('def finish('):legacy.index("exec((SOURCE/'launcher_body.py')")].replace("deferred by explicit user request", "lossless export, separate optimization pass").replace("b.segments=3", "b.segments=1 if o.name.startswith('Crimson stepped') else 3"))
RED=material('crimson red enamel',(.48,.043,.028),.24,.45)
DARK=material('crimson graphite',(.055,.061,.068),.62,.39)
BLACK=material('crimson rubber',(.012,.015,.018),.04,.7)
STEEL=material('crimson titanium',(.42,.45,.47),.82,.3)
# A continuous stepped shell. It is deliberately unequal in height and width along its length.
outline=[(.055,-.105),(.265,-.105),(.285,-.035),(.282,.22),(.325,.255),(.345,.64),(.312,.705),(.025,.705),(.012,.63),(.058,.44),(.085,.31),(.075,.18),(.015,.105),(.015,-.04)]
body=profile('Crimson stepped enamel monocoque',outline,.135,RED,bevel=.024)
# Deep physical mouth cut into the broad triangular nose.
bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=.105,depth=.42,location=(0,.18,.63));cut(body,bpy.context.object)
lathe('recessed dark bore',[(.708,.111),(.712,.103),(.705,.094),(.465,.094),(.465,.108)],DARK,n=64,center=(0,.18),closed=True)
lathe('deep throat shadow',[(.465,.094),(.43,.073)],BLACK,n=64,center=(0,.18))
# Shadowed nose bevel. A flat front face with bevelled corners frames one round opening.
for side in (-1,1):
 profile('nose side gasket',[(.04,.672),(.31,.672),(.30,.704),(.05,.714)],.003,BLACK,x=side*.136,bevel=.002)
 profile('lower enamel shadow line',[(.055,.10),(.075,.16),(.091,.32),(.069,.44),(.040,.60),(.028,.60),(.055,.43),(.077,.31),(.061,.17),(.041,.11)],.002,BLACK,x=side*.137,bevel=.001)
 # Intentional sparse fasteners, not repetitive surface greeble.
 for y,z in ((.10,-.06),(.245,-.04),(.28,.61),(.066,.61),(.23,.24)):
  bolt('crimson shell fastener',(side*.140,y,z),r=.003)
# Open black load-bearing keel. The negative space is real topology below the shell.
profile('lower open structural keel',[(-.01,-.07),(.047,-.09),(.052,.06),(-.055,.12),(-.085,.52),(-.04,.58),(.035,.60),(.022,.66),(-.098,.60),(-.117,.53),(-.105,.10),(-.055,-.03)],.068,DARK,bevel=.007)
# Recessed side instrument bezel, oriented inward on both sides; runtime screen is left side.
for side in (-1,1):
 bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.069,depth=.020,location=(side*.139,.039,.014));o=bpy.context.object;o.rotation_euler[1]=math.pi/2;o.name='round inset instrument bezel';finish(o,DARK,.003)
 bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.059,depth=.021,location=(side*.145,.039,.014));o=bpy.context.object;o.rotation_euler[1]=math.pi/2;o.name='instrument black backing';finish(o,BLACK,.001)
# Shoulder extension: much thicker than the concept's thin stick; integrated underside saddle.
profile('tapered shoulder chassis',[(.095,-.11),(.11,-.43),(.115,-.62),(.28,-.62),(.27,-.56),(.245,-.50),(.245,-.13),(.26,-.08)],.084,DARK,bevel=.009)
for side in (-1,1):
 profile('red shoulder upper rail',[(.229,-.12),(.242,-.51),(.263,-.56),(.27,-.61),(.248,-.63),(.222,-.58),(.208,-.13)],.007,RED,x=side*.083,bevel=.004)
 profile('shoulder side silver inlay',[(.135,-.18),(.143,-.47),(.153,-.48),(.148,-.19)],.002,STEEL,x=side*.086,bevel=.001)
# Concave shoulder pad: high center clears the rounded shoulder, lower ends wrap it.
profile('concave shoulder saddle',[(.112,-.17),(.095,-.24),(.075,-.31),(.065,-.41),(.075,-.50),(.10,-.56),(.12,-.58),(.095,-.59),(.075,-.54),(.05,-.49),(.04,-.40),(.05,-.30),(.075,-.23),(.095,-.17)],.092,BLACK,bevel=.006)
profile('rear bumper',[(.095,-.63),(.276,-.63),(.276,-.66),(.10,-.66)],.094,BLACK,bevel=.006)
# Short dorsal fin and recessed vent grid, matching selected Crimson silhouette.
profile('red forward dorsal ridge',[(.34,.47),(.40,.58),(.40,.64),(.342,.65)],.016,RED,bevel=.003)
box('upper recessed ventilation',(0,.288,.10),(.085,.008,.15),BLACK,.004)
for z in (.045,.065,.085,.105,.125,.145):box('vent transverse bar',(0,.294,z),(.09,.008,.004),DARK,.001)
# Existing hand-contact volumes retained, broadening body does not scale hands.
profile('rear grip structural spine',[(-.024,-.058),(-.038,.025),(-.18,.002),(-.213,-.018),(-.205,-.063),(-.17,-.073)],.029,DARK,bevel=.006)
for s in (-1,1):
 profile('rear grip rubber inlay',[(-.064,-.049),(-.068,.004),(-.177,-.016),(-.189,-.028),(-.184,-.051),(-.163,-.058)],.003,BLACK,x=s*.031,bevel=.003)
 for i in range(10):
  o=box('rear grip tread',(s*.035,-.08-i*.009,-.027),(.0015,.002,.028),DARK,.0005);o.rotation_euler[0]=-.45
profile('support grip core',[(-.018,.318),(-.018,.38),(-.16,.38),(-.194,.366),(-.194,.322),(-.163,.314)],.028,BLACK,bevel=.006)
for s in (-1,1):
 profile('support metal heel',[(-.177,.314),(-.199,.32),(-.199,.374),(-.18,.385),(-.18,.37),(-.19,.36),(-.19,.33)],.003,DARK,x=s*.029,bevel=.002)
 for i in range(12):box('support grip tread',(s*.030,-.052-i*.009,.35),(.002,.002,.040),DARK,.0004)
profile('open trigger guard',[(-.032,.025),(-.027,.100),(-.044,.112),(-.121,.098),(-.137,.075),(-.127,.023),(-.113,.024),(-.119,.073),(-.109,.084),(-.045,.096),(-.043,.026)],.023,DARK,bevel=.003)
profile('red trigger',[(-.037,.049),(-.064,.043),(-.089,.059),(-.084,.067),(-.059,.058),(-.038,.06)],.006,RED,bevel=.0015)
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 rear=o.name.startswith('rear grip');support=o.name.startswith(('support grip','support metal'))
 if not (rear or support):continue
 m=o.matrix_world.copy();inv=m.inverted();center=-.018 if rear else .35
 for v in o.data.vertices:
  q=m@v.co;q.z=center+(q.z-center)*(1.65 if rear else 1.6);v.co=inv@q
exec((SOURCE/'details.py').read_text(),globals())
mark('pulse-grip',(0,-.09,0));mark('pulse-muzzle',(0,.18,.744));mark('pulse-support-grip',(0,-.1,.35))
mark('crimson-shoulder',(0,.055,-.40));mark('pulse-display-mount',(.158,.039,.014))
# Runtime builds screen normal from explicit authored marker basis, ignoring Blender empty rotation.
mark('pulse-display-normal',(.258,.039,.014));mark('pulse-display-up',(.158,.139,.014))
# Save high-detail editable source, then export losslessly joined production mesh.
# Existing export implementation keeps compatibility with the project's GLB material importer.
export('crimson-master',[('pulse-lod0','pulse-launcher-lod0.glb'),('pulse-lod1','pulse-launcher-lod1.glb')])
manifest=OUT/'asset-manifest.json';d=json.loads(manifest.read_text())
for e in d['entries']:
 if e['key'] in ('pulse-lod0','pulse-lod1'):e['source']='Original Crimson; scripts/crimson/build.py and crimson-master.blend; same silhouette lossless material batching'
manifest.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')

# Blender may localize default node names; the legacy helpers then create a second
# output. glTF materials are explicitly authored above; activate the textured
# output in the editable source as well, so its preview matches the exported asset.
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'crimson-master.blend'))
for mat in bpy.data.materials:
 if not mat.use_nodes:continue
 for node in mat.node_tree.nodes:
  if node.type!='OUTPUT_MATERIAL':continue
  links=node.inputs['Surface'].links
  if links and links[0].from_node.type=='BSDF_PRINCIPLED' and links[0].from_node.inputs['Base Color'].is_linked:
   node.is_active_output=True
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'crimson-master.blend'))
