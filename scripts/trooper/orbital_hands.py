"""Strata armor authoring over retained, attributed anatomical glove topology/UVs. Dimensions in metres are asset design, not runtime tuning.
The source rig's dorsum is (sign,0,1), never global -Y. All shells use that frame.
"""
import bpy, math
from mathutils import Vector


def build(api, rig):
 mesh_object,skin,join,tube,white,navy,rubber,metal = [api[k] for k in ('mesh_object','skin_detail','join','tube','white','navy','rubber','metal')]
 leather=api['leather']
 thread=api['thread']
 cloth=api['cloth']
 pieces=list(api.get('source_gloves',[]))
 def surface(name, verts, faces, material, bone):
  o=mesh_object(name,verts,faces,material);skin(o,rig,bone);return o
 def frame(a,b,dorsal):
  axis=(b-a).normalized();normal=(dorsal-axis*axis.dot(dorsal)).normalized();lateral=axis.cross(normal).normalized();return axis,lateral,normal
 def loft(name,a,b,width,depth,dorsal,bone,profile,material=rubber):
  axis,lat,norm=frame(a,b,dorsal);verts=[];faces=[];n=32
  for t,w,d in profile:
   for i in range(n):
    angle=math.tau*i/n
    verts.append(a.lerp(b,t)+lat*(math.cos(angle)*width*w/2)+norm*(math.sin(angle)*depth*d/2))
  for j in range(len(profile)-1):
   for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
  faces.extend([tuple(reversed(range(n))),tuple(range((len(profile)-1)*n,len(profile)*n))])
  return surface(name,verts,faces,material,bone)
 def shell(name,a,b,width,dorsal,bone,shape,material=white,thickness=.0016):
  # A closed, thin tailored panel with a broad sculptural ridge and tightly rolled edge.
  axis,lat,norm=frame(a,b,dorsal);length=(b-a).length
  contour=api['smooth_outline'](shape,4);center=sum(contour,Vector((0,0)))/len(contour);n=len(contour)
  verts=[]
  for scale,lift in [(1,0),(.97,thickness),(.84,thickness+.0002),(.42,thickness+.0005),(.02,thickness+.0006)]:
   for p in contour:
    q=center+(p-center)*scale
    verts.append(a+axis*(q.x*length)+lat*(q.y*width)+norm*(lift-.0008*(q.y*2)**2))
  faces=[]
  for j in range(4):
   for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
  faces.extend([tuple(reversed(range(n))),tuple(range(4*n,5*n))])
  o=surface(name,verts,faces,material,bone);pieces.append(o)
  return axis,lat,norm
 def cuff(name,a,b,width,depth,dorsal,bone,material,thickness,angle=118):
  axis,lat,norm=frame(a,b,dorsal);n=36;verts=[]
  stations=[(.06,.86),(.09,.98),(.18,1),(.76,.95),(.89,.90),(.93,.82)]
  for inner in [False,True]:
   for t,span in stations:
    for i in range(n):
     theta=math.radians(angle)*(2*i/(n-1)-1)*span
     # Elliptic wrap hugs the sleeve; axial scallop exposes flexible fabric at each join.
     tt=t+.028*math.sin(theta*2)
     w=width*(1-.075*t)/2+(0 if inner else thickness)
     d=depth*(1-.075*t)/2+(0 if inner else thickness)
     verts.append(a.lerp(b,tt)+lat*(math.sin(theta)*w)+norm*(math.cos(theta)*d))
  faces=[];count=len(stations)*n
  for layer in range(2):
   for j in range(len(stations)-1):
    for i in range(n-1):
     q=(layer*count+j*n+i,layer*count+j*n+i+1,layer*count+(j+1)*n+i+1,layer*count+(j+1)*n+i)
     faces.append(q if layer==0 else tuple(reversed(q)))
  perimeter=list(range(n))+[j*n+n-1 for j in range(1,len(stations))]+list(reversed(range(count-n,count-1)))+[j*n for j in reversed(range(1,len(stations)-1))]
  for i,k in enumerate(perimeter):l=perimeter[(i+1)%len(perimeter)];faces.append((k,l,l+count,k+count))
  pieces.append(surface(name,verts,faces,material,bone))
 def seam(name,points,bone,r=.00045):
  pieces.append(skin(tube(name+' bound edge',points,r,rubber),rig,bone))
  curve=bpy.data.curves.new(name+' stitches','CURVE');curve.dimensions='3D';curve.bevel_depth=r*.38;curve.bevel_resolution=1
  for a,b in zip(points,points[1:]):
   a,b=Vector(a),Vector(b);count=max(1,int((b-a).length/.0018))
   for i in range(count):
    spline=curve.splines.new('POLY');spline.points.add(1)
    for p,co in zip(spline.points,[a.lerp(b,i/count),a.lerp(b,(i+.48)/count)]):p.co=(*co,1)
  o=bpy.data.objects.new(name+' stitching',curve);bpy.context.collection.objects.link(o)
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH')
  pieces.append(skin(api['finish'](bpy.context.object,name+' stitching',thread),rig,bone))
 bones=rig.data.bones
 for side,sign in [('Left',1),('Right',-1)]:
  dorsal=Vector((sign,0,1)).normalized();w=bones[side+'Hand'].head_local;el=bones[side+'LowerArm'].head_local;shoulder=bones[side+'UpperArm'].head_local
  # Sleeves retain twist bindings; overlapping open ends stay behind the cuff.
  for region,start,end,width0,width1,depth0,depth1 in [('UpperArm',shoulder,el,.102,.077,.088,.068),('LowerArm',el,w,.078,.052,.068,.047)]:
   for i in range(3):
    bone=side+region+('' if i==0 else 'Twist'+str(i));bone=bone if bone in bones else side+region;a=start.lerp(end,i/3);b=start.lerp(end,(i+1)/3+.015)
    width=width0+(width1-width0)*i/3;depth=depth0+(depth1-depth0)*i/3
    pieces.append(loft(side+' tailored sleeve '+region+str(i),a,b,width,depth,dorsal,bone,[(0,1,1),(.2,1,1),(.75,.96,.96),(1,.93,.93)],cloth))
    if region=='UpperArm':continue
    axis,lat,norm=frame(a,b,dorsal)
    # Thin circumferential gauntlet shells reveal construction from the FPS viewing side.
    cuff('gauntlet structural wrap',a,b,width*1.02,depth*1.02,dorsal,bone,navy,.0013,126)
    cuff('sculpted ceramic forearm wrap',a.lerp(b,.025),a.lerp(b,.975),width*1.025,depth*1.025,dorsal,bone,white,.0022,112)
    cuff('palmar ceramic clamshell',a.lerp(b,.09),a.lerp(b,.91),width*1.025,depth*1.025,-dorsal,bone,white,.0018,46)
    for sign in [-1,1]:
     angle=math.radians(73)*sign;out=norm*math.cos(angle)+lat*math.sin(angle)
     off=norm*(math.cos(angle)*depth*.51)+lat*(math.sin(angle)*width*.51)
     aa=a.lerp(b,.28)+off;bb=a.lerp(b,.72)+off
     shape=[(0,-.2),(.1,-.42),(.83,-.4),(1,.1),(.78,.42),(.1,.36)]
     shell('gauntlet inset titanium frame',aa+out*.002,bb+out*.002,.013,out,bone,shape,metal,.0006)
     shell('gauntlet dark service insert',aa+out*.003,bb+out*.003,.009,out,bone,shape,navy,.0003)
    for s in [-1,1]:seam('fabric sleeve binding',[a.lerp(b,t)+lat*s*width*.43+norm*depth*.20 for t in [.05,.3,.6,.95]],bone,.0006)
  axis,lat,norm=frame(el,w,dorsal)
  for j in range(3):
   center=w-axis*(.006+j*.007)
   pts=[center+lat*math.cos(t)*.027+norm*math.sin(t)*.024 for t in [k*math.tau/48 for k in range(48)]]
   pieces.append(skin(tube('flexible cuff gasket',pts,.0015,metal if j==1 else navy,True),rig,side+'Hand'))
  # Anatomical palm: width follows the index-to-little row, depth follows the dorsum.
  starts=[bones[side+d+'Proximal'].head_local for d in ['Index','Middle','Ring','Little']]
  end=sum(starts,Vector())/4;axis,lat,norm=frame(w,end,dorsal);bone=side+'Hand'
  for digit in ['Thumb','Index','Middle','Ring','Little']:
   for ix,suffix in enumerate(['Proximal','Intermediate','Distal']):
    bn=side+digit+suffix;db=bones[bn];a=db.head_local;b=db.tail_local
    width=({'Thumb':.025,'Index':.023,'Middle':.024,'Ring':.022,'Little':.019}[digit])*(1-ix*.085)
    profile=[(-.16,.78,.82),(0,1,1),(.18,1.02,1),(.55,.96,.95),(.85,.86,.88),(1.13,.70,.74)]
    if ix==2:profile=[(-.13,.85,.87),(0,.98,1),(.35,.98,1),(.72,.84,.93),(.95,.51,.65),(1.04,.06,.10)]
    da,dl,dn=frame(a,b,dorsal)
    if ix<2:
     aa=a.lerp(b,.20)+dn*(width*.46+.004);bb=a.lerp(b,.80)+dn*(width*.42+.004)
     shape=[(0,-.35),(.15,-.49),(.8,-.42),(1,-.2),(.93,.30),(.65,.46),(.12,.43)]
     shell('phalanx flexible perimeter '+bn,aa-dn*.002,bb-dn*.002,width*.90,dorsal,bn,shape,navy,.0008)
     shell('thin phalanx shield '+bn,aa+dn*.0004,bb,width*.76,dorsal,bn,shape,white,.001)
    for s in [-1,1]:seam('finger saddle seam',[a.lerp(b,t)+dl*s*width*.43+dn*width*.20 for t in [.08,.3,.6,.86]],bn,.0003)
    # Soft transverse leather creases at the joint, matte and submillimetre.
    for k in range(2):
     p=a.lerp(b,.025+k*.075)
     seam('knuckle flex crease',[p+dl*x+dn*(width*.44-abs(x)*.12) for x in [-width*.33,0,width*.33]],bn,.00035)
  # Two asymmetric dorsal plates follow metacarpals; no white caps on fingertips.
  for j in [-1,1]:
   aa=w.lerp(end,.28)+lat*j*.022+norm*.035;bb=w.lerp(end,.79)+lat*j*.023+norm*.033
   shape=[(0,-.26),(.13,-.46),(.76,-.5),(1,-.12),(.92,.30),(.50,.46),(.1,.34)]
   shell('metacarpal structural rim',aa-norm*.002,bb-norm*.003,.043,dorsal,bone,shape,navy,.001)
   shell('contoured metacarpal ceramic',aa+norm*.001,bb+norm*.001,.038,dorsal,bone,shape,white,.0013)
  for s in [-1,1]:seam('glove perimeter binding',[w.lerp(end,t)+lat*s*(.029+.018*t)+norm*.004 for t in [.05,.3,.6,.85,1]],bone,.00065)
 return join(pieces,'vector-armored-hands')
