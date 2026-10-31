"""Authored structural detail for the selected Crimson concept; imported by build.py."""
# Small hardware uses 12 sides and a single bevel instead of the high-detail
# primitive's 24 sides/three bevel segments; broad armor silhouette is unchanged.
def detail_bolt(name,pos,axis='X',r=.004):
 bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=.003,location=pos)
 o=bpy.context.object;o.name=name
 if axis=='X':o.rotation_euler[1]=math.pi/2
 elif axis=='Y':o.rotation_euler[0]=math.pi/2
 b=o.modifiers.new('Small machined bevel','BEVEL');b.width=.0005;b.segments=1
 bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=b.name);finish(o,STEEL)
 size=(.0035,.0012,r*1.3) if axis=='X' else (r*1.3,.0035,.0012)
 box(name+' inset drive',pos,size,BLACK,0)
# A deep octagonal armored mouth surrounds the recessed circular bore.
# Ring coordinates are source art, not gameplay tuning.
mouth=[(-.092,.344),(.092,.344),(.148,.294),(.148,.060),(.097,.006),(-.097,.006),(-.148,.060),(-.148,.294)]
def muzzle_rim(name,rings,mat):
 verts=[(x*scale,.175+(y-.175)*scale,z) for z,scale in rings for x,y in mouth]
 faces=[]
 for j in range(len(rings)):
  k=(j+1)%len(rings)
  for i in range(8):faces.append((j*8+i,j*8+(i+1)%8,k*8+(i+1)%8,k*8+i))
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);finish(o,mat)
 for p in mesh.polygons:p.use_smooth=False
 return o
muzzle_rim('eight sided inset muzzle armor',[(.677,1.02),(.712,1.02),(.742,.94),(.742,.79),(.695,.74),(.677,.79)],DARK)
muzzle_rim('thin titanium muzzle bevel',[(.740,.949),(.744,.935),(.744,.906),(.740,.911)],STEEL)
muzzle_rim('red nose lip',[(.686,1.033),(.719,1.033),(.729,.999),(.719,.982),(.688,.982)],RED)
# Real layered armor: broad edge facets and dark panel joints are readable at distance.
for side in (-1,1):
 plate=[(.087,.110),(.250,.095),(.268,.237),(.313,.277),(.327,.624),(.285,.665),(.052,.655),(.048,.600),(.086,.427),(.112,.322),(.103,.188)]
 profile('recessed armor seam',plate,.004,DARK,x=side*.135,bevel=.009)
 panel=[(.096,.124),(.243,.113),(.256,.246),(.300,.286),(.314,.617),(.279,.650),(.064,.640),(.061,.600),(.098,.429),(.124,.324),(.115,.187)]
 profile('faceted floating side armor',panel,.006,RED,x=side*.140,bevel=.012)
 # Narrow break separates the nose service panel from the longer rear armor.
 profile('nose access diagonal seam',[(.065,.574),(.069,.582),(.312,.572),(.312,.564)],.0018,DARK,x=side*.148,bevel=.0008)
 profile('main diagonal titanium identity',[(.115,.485),(.123,.526),(.281,.435),(.277,.390)],.0025,STEEL,x=side*.150,bevel=.002)
 # Angular lower receiver encloses the instrument and ties both grips into the keel.
 receiver=[(-.112,-.076),(.027,-.105),(.101,-.059),(.105,.068),(.060,.114),(-.082,.135),(-.143,.070)]
 profile('instrument load bearing receiver',receiver,.012,DARK,x=side*.116,bevel=.008)
 profile('receiver inset',[(.015,-.068),(.065,-.045),(.068,.060),(.025,.087),(-.077,.095),(-.100,.049)],.002,BLACK,x=side*.131,bevel=.004)
 profile('lower frame layered rail',[(-.094,.103),(-.113,.528),(-.096,.568),(-.084,.517),(-.075,.129)],.004,BLACK,x=side*.071,bevel=.003)
 profile('front load brace',[(-.098,.552),(-.046,.590),(.033,.586),(.026,.630),(-.067,.609),(-.121,.569)],.008,DARK,x=side*.068,bevel=.004)
 for y,z in ((.245,.132),(.285,.298),(.292,.612),(.070,.620),(-.101,.520),(-.082,.125),(.085,-.061)):
  detail_bolt('recessed armor attachment',(side*.151 if y>0 and z>.1 else side*.134,y,z),r=.0045)
 # Shoulder connector is a collar, not a plain extrusion from the red shell.
 profile('rear locking collar',[(.075,-.129),(.093,-.185),(.245,-.187),(.278,-.145),(.275,-.102),(.240,-.080),(.105,-.093)],.010,DARK,x=side*.090,bevel=.006)
 profile('rear collar metal edge',[(.100,-.167),(.114,-.179),(.236,-.178),(.256,-.153),(.247,-.148),(.233,-.165),(.120,-.166)],.002,STEEL,x=side*.102,bevel=.001)
 profile('tail recessed channel',[(.142,-.215),(.152,-.539),(.199,-.565),(.211,-.547),(.190,-.529),(.182,-.214)],.003,BLACK,x=side*.088,bevel=.002)
 profile('tail lower load rail',[(.112,-.212),(.123,-.537),(.143,-.559),(.152,-.551),(.137,-.529),(.129,-.212)],.004,DARK,x=side*.091,bevel=.002)
 for z in (-.232,-.520):detail_bolt('tail captive pin',(side*.096,.214,z),r=.004)
 profile('rear armored end cap',[(.093,-.615),(.276,-.615),(.288,-.637),(.284,-.667),(.091,-.667),(.081,-.644)],.008,DARK,x=side*.089,bevel=.005)
 for y in (.118,.250):detail_bolt('end cap pin',(side*.100,y,-.643),r=.004)
# Top rail and black vent cassette give the FPS top surface useful structure.
box('top vent cassette',(0,.302,.125),(.098,.018,.195),DARK,.005)
for z in (.050,.073,.096,.119,.142,.165,.188):box('cassette recessed fin',(0,.313,z),(.078,.008,.006),BLACK,.001)
for side in (-1,1):
 box('top cassette latch',(side*.058,.305,.138),(.018,.021,.055),RED,.003)
 detail_bolt('cassette latch screw',(side*.058,.318,.138),axis='Y',r=.0035)
# The dorsal blade sits in a machined shoe; small ribs reveal thickness from the side.
box('dorsal blade mounting shoe',(0,.346,.570),(.067,.018,.188),DARK,.004)
for side in (-1,1):
 for z in (.513,.625):detail_bolt('dorsal shoe fastener',(side*.027,.359,z),axis='Y',r=.003)
