"""F19 reference reconstruction. Offline authored geometry; retained bind rig/FPS.

Rebuilds the rejected F18 broad forms rather than adding surface noise to them.
Maps are authored tileable PBR surfaces; dimensions here do not tune gameplay.
"""
import sys, math, hashlib
from pathlib import Path
import bpy, bmesh, numpy as np
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build as g


def image(name,values,color=False):
    h,w=values.shape[:2]
    if values.ndim==2:values=np.repeat(values[:,:,None],3,axis=2)
    rgba=np.concatenate([values,np.ones((h,w,1),dtype=np.float32)],axis=2).astype(np.float32)
    im=bpy.data.images.new(name,width=w,height=h)
    im.colorspace_settings.name='sRGB' if color else 'Non-Color'
    im.pixels.foreach_set(rgba.ravel());im.pack();return im


def normal(height,strength):
    dy,dx=np.gradient(height);n=np.stack([-dx*strength,-dy*strength,np.ones_like(dx)],axis=2)
    n/=np.linalg.norm(n,axis=2)[:,:,None];return n*.5+.5


def surfaces():
    global steel,paint,edge,cloth,seal,glass,webbing
    rng=np.random.default_rng(190128);n=1024;y,x=np.mgrid[0:n,0:n].astype(np.float32)/n
    grain=rng.normal(0,1,(n,n)).astype(np.float32)
    roll=(np.sin(x*43+y*29)+np.sin(x*103-y*71))*.5
    scratches=np.zeros((n,n),dtype=np.float32)
    # Sparse local abrasion, never an all-over white-noise camouflage.
    for _ in range(85):
        cx,cy=rng.random(2);length=rng.uniform(.006,.08);angle=rng.uniform(-.8,.8)
        u=(x-cx)*math.cos(angle)+(y-cy)*math.sin(angle);v=-(x-cx)*math.sin(angle)+(y-cy)*math.cos(angle)
        scratches=np.maximum(scratches,np.clip(1-np.abs(v)/.0009,0,1)*(np.abs(u)<length)*rng.uniform(.2,.75))
    coating=np.clip(.145+grain*.004+roll*.003+scratches*.055,.08,.25)
    coat=image('vector-reference-coating-albedo',coating,True)
    coatnormal=image('vector-reference-coating-normal',normal(grain*.0005-scratches*.009,35))
    orm=image('vector-reference-coating-ORM',np.stack([np.ones_like(x),np.clip(.66+grain*.015-scratches*.15,.42,.82),np.full_like(x,.32)],axis=2))
    # Interlaced warp and weft: alternating over/under yarns, not glossy rubber.
    u=x*64;v=y*64;warp=np.cos((u%1-.5)*math.pi);weft=np.cos((v%1-.5)*math.pi)
    alternate=(np.floor(u)+np.floor(v))%2
    weave=warp*(1-alternate)+weft*alternate
    textile=np.clip(.024+weave*.015+grain*.0015,.012,.05)
    textilecolor=image('vector-reference-ballistic-weave',textile,True)
    textilenormal=image('vector-reference-weave-normal',normal(weave*.025+grain*.0006,22))
    def mat(name,col,metal,rough,albedo=None,norm=None,packed=None):
        m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True
        nodes=m.node_tree.nodes;links=m.node_tree.links;bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');bs.name='Principled BSDF'
        bs.inputs['Base Color'].default_value=(*col,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
        if albedo:
            t=nodes.new('ShaderNodeTexImage');t.image=albedo;links.new(t.outputs['Color'],bs.inputs['Base Color'])
        if norm:
            t=nodes.new('ShaderNodeTexImage');t.image=norm;nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.65
            links.new(t.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bs.inputs['Normal'])
        if packed:
            t=nodes.new('ShaderNodeTexImage');t.image=packed;sep=nodes.new('ShaderNodeSeparateColor');links.new(t.outputs['Color'],sep.inputs['Color'])
            links.new(sep.outputs['Green'],bs.inputs['Roughness']);links.new(sep.outputs['Blue'],bs.inputs['Metallic'])
        return m
    steel=mat('vector-reference-coated-steel',(.32,)*3,.32,.66,coat,coatnormal,orm)
    paintmap=image('vector-reference-paint-albedo',np.clip(coating*2.35,.18,.6),True)
    paint=mat('vector-fighter-identity-reference',(.41,)*3,.25,.65,paintmap,coatnormal,orm)
    edge=mat('vector-reference-edge-metal',(.065,.075,.085),.55,.55,None,coatnormal)
    cloth=mat('vector-reference-woven-fabric',(.1,)*3,0,.9,textilecolor,textilenormal)
    webbing=mat('vector-reference-webbing',(.055,)*3,0,.95,textilecolor,textilenormal)
    seal=mat('vector-reference-flexible-seals',(.022,.025,.028),0,.85,None,textilenormal)
    glass=mat('vector-reference-opaque-visor',(.006,.009,.012),.3,.18)


def poly(name,points,bone,material=None,depth=.012,normal_axis=None,hardware=False):
    """Closed fitted plate with a flat primary face, deliberate chamfer and gasket."""
    p=[Vector(v) for v in points];n=len(p);center=sum(p,Vector())/n
    normal_axis=Vector(normal_axis) if normal_axis else (p[1]-p[0]).cross(p[2]-p[0]).normalized()
    axis=normal_axis.normalized();verts=[]
    # Explicit surface triangles over one convex face; no overlapping inset
    # ngons and no reverse diagonal crossing a concave manufactured boundary.
    for scale,offset in [(1,0),(.975,.001),(1,-min(depth,.006))]:
        verts.extend(center+(v-center)*scale+axis*offset for v in p)
    verts.append(center+axis*.002);faces=[];face_materials=[]
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,n+j,n+i),(n+i,n+j,3*n),(i,2*n+i,2*n+j,j)])
        face_materials.extend([0,0,1])
    faces.append(tuple(reversed(range(2*n,3*n))));face_materials.append(2)
    o=g.finish('VECTOR R '+name,verts,faces,material or steel,bone)
    o.data.materials.append(edge);o.data.materials.append(seal)
    for f,slot in zip(o.data.polygons,face_materials):f.material_index=slot;f.use_smooth=False
    # Stable physical texel scale with per-panel offset prevents repeated scratches.
    offset=int(hashlib.sha256(name.encode()).hexdigest()[:4],16)/65535
    axes=(0,2) if abs(axis.y)>.5 else (1,2) if abs(axis.x)>.5 else (0,1)
    for f in o.data.polygons:
        for li in f.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co;o.data.uv_layers.active.data[li].uv=(v[axes[0]]*3+offset,v[axes[1]]*3+offset*.7)
    if hardware:
        for i in [0,n//2]:fastener(name+' screw',p[i].lerp(center,.18)+axis*.005,axis,bone)
    return o


def fastener(name,p,axis,bone):
    # Hexagonal recessed socket with dark center, shared materials.
    q=Vector((0,0,1)).rotation_difference(Vector(axis));verts=[];faces=[];count=6
    for radius,z in [(.0034,0),(.0023,.0008),(.00125,.0008),(.00125,-.001)]:
        verts.extend(Vector(p)+q@Vector((radius*math.cos(i*math.tau/count),radius*math.sin(i*math.tau/count),z)) for i in range(count))
    for r in range(3):
        for i in range(count):j=(i+1)%count;faces.append((r*count+i,r*count+j,(r+1)*count+j,(r+1)*count+i))
    faces.append(tuple(range(18,24)));o=g.finish('VECTOR R '+name,verts,faces,edge,bone);o.data.materials.append(seal)
    o.data.polygons[-1].material_index=1
    for f in o.data.polygons:f.use_smooth=False


def line(name,points,bone,material=None,r=.0014):
    return g.tube('VECTOR R '+name,points,r,material or seal,bone,radial=5)


def foundation():
    keep=('UpperArm fabric','continuous lower garment','torso fabric','gloves')
    for o in list(bpy.context.scene.objects):
        if o.type!='MESH' or o.name=='weapon:joined':continue
        if not any(k in o.name for k in keep):bpy.data.objects.remove(o,do_unlink=True);continue
        if 'gloves' in o.name:
            for index,m in enumerate(o.data.materials):
                if m and (m.name.startswith('vector-') or 'chassis' in m.name):o.data.materials[index]=steel if 'ceramic' in m.name else edge
            continue
        o.data.materials.clear();o.data.materials.append(cloth)
        for f in o.data.polygons:f.material_index=0
        for v in o.data.vertices:
            p=o.matrix_world@v.co
            if 'torso fabric' in o.name:
                t=max(0,min(1,(p.z-1.06)/.35));p.x*=.82+.09*t;p.y=.035+(p.y-.035)*.80
            elif 'lower garment' in o.name and p.z>.18:
                center=(.145 if p.x>0 else -.145) if p.z<.82 else 0
                p.x=center+(p.x-center)*(.88 if p.z<.82 else .91);p.y=.035+(p.y-.035)*.87
                fold=math.exp(-((p.z-.53)/.065)**2)*.0025*math.sin(p.z*220+p.x*40)+math.exp(-((p.z-.83)/.045)**2)*.002*math.sin(p.z*190+p.x*60)
                p.y+=math.copysign(fold,p.y-.035)
            elif 'UpperArm fabric' in o.name:
                # Keep wrist contacts, narrow only the soft sleeve around the bone line.
                side='Left' if p.x>0 else 'Right';a=g.rig.data.bones[side+'UpperArm'].head_local;b=g.rig.data.bones[side+'Hand'].head_local
                d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared));anchor=a+d*t;p=anchor+(p-anchor)*.88
            v.co=o.matrix_world.inverted()@p
        o.data.update()
        for face in o.data.polygons:
            normal=face.normal;axes=(0,2) if abs(normal.y)>.5 else (1,2) if abs(normal.x)>.5 else (0,1)
            for li in face.loop_indices:
                point=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co
                o.data.uv_layers.active.data[li].uv=(point[axes[0]]*8,point[axes[1]]*8)


def torso():
    front=lambda x,z:(x,-.137+1.25*x*x,z)
    # Backing shell and independent pectoral panels produce actual layered construction.
    outline=[(-.065,1.465),(.065,1.465),(.199,1.414),(.226,1.337),(.166,1.253),(.063,1.238),(-.063,1.238),(-.166,1.253),(-.226,1.337),(-.199,1.414)]
    poly('chest backing',[front(x,z) for x,z in outline],'Chest',seal,.016,(0,-1,0))
    for sign in [-1,1]:
        pts=[(.009,1.431),(.070,1.449),(.188,1.404),(.206,1.350),(.144,1.315),(.015,1.337)]
        poly('pectoral plate '+str(sign),[(sign*x,front(x,z)[1]-.014,z) for x,z in pts],'Chest',paint,.017,(0,-1,0),True)
        poly('costal plate '+str(sign),[(sign*x,front(x,z)[1]-.009,z) for x,z in [(.013,1.323),(.142,1.300),(.199,1.338),(.185,1.281),(.102,1.256),(.028,1.271)]],'Chest',steel,.012,(0,-1,0),True)
        # Side wraps join front armor to rear panels; no floating breastplate patches.
        poly('thoracic wrap '+str(sign),[(sign*.203,-.075,1.389),(sign*.217,.009,1.371),(sign*.197,.113,1.345),(sign*.171,.103,1.267),(sign*.183,-.059,1.274)],'Chest',steel,.013,(sign,0,0),True)
        poly('clavicle mount '+str(sign),[(sign*x,y,z) for x,y,z in [(.055,-.062,1.474),(.10,-.065,1.474),(.192,-.042,1.422),(.174,-.085,1.412),(.091,-.104,1.447),(.058,-.103,1.447)]],'UpperChest',steel,.012,(0,-1,1),True)
        poly('rear scapula '+str(sign),[(sign*x,.15-.5*x*x,z) for x,z in [(.009,1.447),(.10,1.449),(.204,1.383),(.187,1.290),(.101,1.260),(.018,1.293)]],'Chest',steel,.015,(0,1,0),True)
        poly('rear ID '+str(sign),[(sign*x,.159-.5*x*x,z) for x,z in [(.045,1.418),(.10,1.418),(.158,1.384),(.145,1.358),(.096,1.383),(.045,1.386)]],'Chest',paint,.006,(0,1,0))
    for i,(top,bottom,width) in enumerate([(1.253,1.194,.144),(1.191,1.131,.129),(1.128,1.073,.112)]):
        pts=[(-width,top),(-.045,top-.009),(.045,top-.009),(width,top),(width*.80,bottom+.016),(.04,bottom),(-.04,bottom),(-width*.80,bottom+.016)]
        poly('abdomen '+str(i),[(x,-.116+1.0*x*x,z) for x,z in pts],'Spine',steel,.012,(0,-1,0),True)
        poly('lumbar '+str(i),[(x,.132-.5*x*x,z) for x,z in pts],'Spine',steel,.011,(0,1,0))
    # Load belt with separate sewn webbing, compact pouches and flush buckle.
    rings=[(0,.035,z,.173,.117) for z in [1.011,1.056]]
    belt=g.loft('VECTOR R woven belt',rings,webbing,'Hips',N=32)
    for a in [-1.24,-.68,.68,1.24,2.2,-2.2]:
        p=Vector((.174*math.sin(a),.035-.122*math.cos(a),1.033));tangent=Vector((math.cos(a),math.sin(a),0));out=Vector((math.sin(a),-math.cos(a),0))
        corners=[p+tangent*u+Vector((0,0,z)) for u,z in [(-.026,.028),(.026,.028),(.03,.018),(.028,-.031),(-.028,-.031),(-.03,.018)]]
        poly('belt pouch '+str(a),corners,'Hips',steel,.022,out,True)
    poly('belt buckle',[(-.028,-.094,1.056),(.028,-.094,1.056),(.036,-.094,1.04),(.028,-.094,1.013),(-.028,-.094,1.013),(-.036,-.094,1.04)],'Hips',edge,.012,(0,-1,0))
    for sign in [-1,1]:
        line('torso seam',[(sign*.15,-.068,z) for z in [1.08,1.12,1.18,1.24]],'Spine',webbing,.002)
    for sign in [-1,1]:
        poly('pelvic wing '+str(sign),[(sign*x,y,z) for x,y,z in [(.075,-.073,1.01),(.158,-.055,1.00),(.195,-.035,.956),(.178,-.055,.900),(.123,-.081,.925),(.086,-.094,.968)]],'Hips',steel,.008,(0,-1,0),True)
        line('upper trouser seam '+str(sign),[(sign*.108,-.071,.925),(sign*.092,-.064,.87),(sign*.077,-.055,.81)],'Hips',webbing,.0015)
    # Flexible high collar supports the lower helmet rather than exposing a thin neck.
    g.loft('VECTOR R neck textile',[(0,.028,z,rx,ry) for z,rx,ry in [(1.43,.067,.059),(1.49,.065,.060),(1.555,.061,.060)]],cloth,'Neck',N=24)


def limb_frame(side,part):
    bone=g.rig.data.bones[side+part];a=bone.head_local.copy();b=(g.rig.data.bones[side+'Hand'].head_local.copy() if part=='LowerArm' else bone.tail_local.copy())
    axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized()
    return a,b,front,lateral


def limb_plate(name,side,part,outline,rx,ry,mat=None,back=False):
    # Evaluate every interior ring on the limb surface. A nonplanar boundary
    # filled by an ngon cuts through the cloth and makes hollow shoulder frames.
    a,b,front,lateral=limb_frame(side,part)
    if back:front=-front
    corners=[Vector(p) for p in outline];uv=[p.lerp(corners[(i+1)%len(corners)],j/3) for i,p in enumerate(corners) for j in range(3)];center=sum(uv,Vector((0,0)))/len(uv);n=len(uv)
    def surface(p,offset):
        angle,t=p;radial=lateral*math.sin(angle)+front*math.cos(angle)
        scale=1
        if name.startswith('shoulder'):
            scale=float(np.interp(t,[-.40,-.30,-.13,.08,.35,.68],[.13,.53,.88,1,.98,.82]))
        return a.lerp(b,t)+lateral*(rx*scale*math.sin(angle))+front*(ry*scale*math.cos(angle))+radial*offset
    verts=[];coords=[]
    for scale,offset in [(1,0),(.975,.001),(.60,.002),(.10,.002),(1,-.006)]:
        for p in uv:
            q=center+(p-center)*scale;coords.append(q);verts.append(surface(q,offset))
    faces=[];slots=[]
    for ring in range(3):
        for i in range(n):
            j=(i+1)%n;faces.append((ring*n+i,ring*n+j,(ring+1)*n+j,(ring+1)*n+i));slots.append(1 if ring==0 else 0)
    faces.append(tuple(range(3*n,4*n)));slots.append(0)
    for i in range(n):j=(i+1)%n;faces.append((i,j,4*n+j,4*n+i));slots.append(2)
    faces.append(tuple(reversed(range(4*n,5*n))));slots.append(2)
    obj=g.finish('VECTOR R '+name+' '+side,verts,faces,mat or steel,side+part)
    obj.data.materials.append(edge);obj.data.materials.append(seal)
    for f,slot in zip(obj.data.polygons,slots):
        f.material_index=slot;f.use_smooth=slot==0
        for li in f.loop_indices:
            q=coords[obj.data.loops[li].vertex_index];obj.data.uv_layers.active.data[li].uv=(q.x*rx*3,q.y*(b-a).length*3)
    for i in [0,n//2]:
        q=uv[i].lerp(center,.18);axis=lateral*math.sin(q.x)+front*math.cos(q.x)
        fastener(name+' screw '+side,surface(q,.005),axis,side+part)
    return obj


def limbs():
    for side in ['Left','Right']:
        a,b,f,l=limb_frame(side,'UpperArm');verts=[];faces=[];n=24
        for t,r in [(-.42,.012),(-.30,.055),(-.13,.095),(.08,.109),(.35,.106),(.68,.086)]:
            for j in range(n):
                angle=j*math.tau/n;verts.append(a.lerp(b,t)+l*(r*math.sin(angle))+f*(r*math.cos(angle)))
        for row in range(5):
            for j in range(n):k=(j+1)%n;faces.append((row*n+j,row*n+k,(row+1)*n+k,(row+1)*n+j))
        faces.extend([tuple(reversed(range(n))),tuple(range(5*n,6*n))])
        g.finish('VECTOR R shoulder backing '+side,verts,faces,seal,side+'UpperArm')
        # Three nested shoulder shells, compact and sharply bevelled instead of spheres.
        for i,(start,end,rx,ry) in enumerate([(-.37,.20,.115,.117),(.16,.43,.117,.119),(.39,.66,.115,.117)]):
            outline=[(-1.5,start+.05),(-.7,start),(.55,start+.01),(1.5,start+.08),(1.35,end-.02),(.55,end),(-.55,end-.02),(-1.35,end-.04)]
            limb_plate('shoulder tier '+str(i),side,'UpperArm',outline,rx,ry,paint if i==1 else steel)
            limb_plate('shoulder rear '+str(i),side,'UpperArm',outline,rx,ry,steel,True)
        # Bicep guard and close-set forearm gauntlet (wrist remains fixed).
        limb_plate('bicep guard',side,'UpperArm',[(-1.1,.62),(1.1,.62),(.95,.85),(.5,.93),(-.55,.92),(-.98,.81)],.081,.085)
        limb_plate('forearm main',side,'LowerArm',[(-1.25,.08),(-.55,.02),(.55,.02),(1.22,.16),(1,.80),(.4,.91),(-.6,.89),(-1.04,.78)],.064,.068)
        limb_plate('forearm rear',side,'LowerArm',[(-1.05,.12),(1.05,.12),(.88,.78),(0,.87),(-.88,.78)],.061,.065,back=True)
        limb_plate('forearm ID',side,'LowerArm',[(-.32,.22),(.30,.22),(.35,.66),(-.25,.72)],.067,.073,paint)
        # Fitted thigh armor follows the quad, a separate lateral plate wraps its edge.
        limb_plate('thigh main',side,'UpperLeg',[(-.85,.08),(-.3,.01),(.5,.03),(.98,.17),(.90,.68),(.25,.87),(-.58,.79),(-.93,.59)],.100,.109)
        limb_plate('thigh ID',side,'UpperLeg',[(.30,.10),(.90,.20),(.86,.47),(.35,.56),(.25,.40)],.105,.114,paint)
        limb_plate('thigh lateral',side,'UpperLeg',[(1.02,.14),(1.75,.21),(1.9,.67),(1.2,.81),(1.06,.63)],.105,.108)
        limb_plate('knee',side,'LowerLeg',[(-.85,-.04),(-.40,-.08),(.43,-.08),(.94,.01),(.65,.18),(0,.22),(-.7,.15)],.065,.072)
        limb_plate('shin main',side,'LowerLeg',[(-.89,.24),(-.22,.21),(.54,.23),(1.02,.34),(.75,.87),(.18,.97),(-.62,.90),(-.86,.56)],.066,.082)
        limb_plate('shin ridge',side,'LowerLeg',[(-.16,.31),(.25,.28),(.32,.70),(-.10,.84)],.071,.087)
        limb_plate('calf',side,'LowerLeg',[(-.90,.20),(.9,.20),(1,.40),(.70,.85),(-.60,.87),(-.95,.45)],.062,.081,back=True)
        a,b,f,l=limb_frame(side,'LowerLeg')
        # Tailoring seams and true articulated joint ribs, under shell clearance.
        for part,start,end,radius in [('UpperArm',.90,1.02,.060),('LowerLeg',.04,.18,.058)]:
            a,b,f,l=limb_frame(side,part)
            for j in range(5):
                t=start+(end-start)*j/4;c=a.lerp(b,t)
                line(side+' joint rib '+part+str(j),[c+l*(radius*math.sin(ang))+f*(radius*math.cos(ang)) for ang in np.linspace(-1.3,1.3,12)],side+part,seal,.0018)
        # Three overlapping instep/toe plates sit flush on the original boot body.
        x=g.rig.data.bones[side+'Foot'].head_local.x
        for j,(v0,v1,z) in enumerate([(-.145,-.076,.054),(-.083,-.014,.075),(-.020,.045,.093)]):
            poly(side+' boot '+str(j),[(x+u,v,z+(.005 if v==v1 else 0)) for u,v in [(-.053,v0),(.053,v0),(.061,v1-.012),(.045,v1),(-.045,v1),(-.061,v1-.012)]],side+'Foot',steel,.009,(0,0,1),j==2)
        line(side+' boot welt',[(x-.060,-.143,.022),(x-.068,-.05,.022),(x-.055,.080,.022),(x+.055,.080,.022),(x+.068,-.05,.022),(x+.060,-.143,.022)],side+'Foot',seal,.003)


def helmet():
    # Faceted black inner enclosure; the visor and armor share a coherent head volume.
    g.loft('VECTOR R helmet interior',[(0,.012,z,rx,ry) for z,rx,ry in [(1.50,.055,.075),(1.56,.095,.106),(1.64,.108,.120),(1.70,.09,.105),(1.735,.045,.060)]],steel,'Head',N=12)
    visor=[(-.084,-.116,1.675),(0,-.143,1.689),(.084,-.116,1.675),(.074,-.132,1.593),(.027,-.157,1.521),(-.027,-.157,1.521),(-.074,-.132,1.593)]
    poly('opaque visor',visor,'Head',glass,.008,(0,-1,0))
    for sign in [-1,1]:
        poly('brow crown '+str(sign),[(sign*x,y,z) for x,y,z in [(.005,-.144,1.701),(.068,-.124,1.721),(.108,-.066,1.699),(.095,-.112,1.672),(.047,-.139,1.675),(.006,-.151,1.679)]],'Head',steel,.014,(0,-1,1),True)
        poly('crown roof '+str(sign),[(sign*x,y,z) for x,y,z in [(.005,-.124,1.724),(.055,-.106,1.740),(.078,-.025,1.738),(.060,.077,1.721),(.008,.107,1.714)]],'Head',steel,.012,(0,0,1),True)
        poly('temple '+str(sign),[(sign*x,y,z) for x,y,z in [(.112,-.063,1.686),(.109,.048,1.691),(.106,.103,1.634),(.115,.059,1.585),(.116,-.042,1.617)]],'Head',steel,.012,(sign,0,0),True)
        poly('jaw rail '+str(sign),[(sign*x,y,z) for x,y,z in [(.099,-.099,1.656),(.117,-.055,1.622),(.111,-.045,1.569),(.047,-.151,1.505),(.016,-.171,1.507),(.061,-.147,1.574),(.080,-.126,1.607)]],'Head',steel,.015,(sign*.3,-1,0),True)
        poly('ear inset '+str(sign),[(sign*.125,y,z) for y,z in [(-.025,1.652),(.019,1.668),(.055,1.645),(.054,1.612),(.016,1.594),(-.021,1.610)]],'Head',edge,.005,(sign,0,0))
        poly('ear ID '+str(sign),[(sign*.129,y,z) for y,z in [(.012,1.650),(.022,1.649),(.026,1.616),(.016,1.612)]],'Head',paint,.004,(sign,0,0))
    poly('chin cap',[(-.028,-.166,1.526),(.028,-.166,1.526),(.025,-.174,1.500),(-.025,-.174,1.500)],'Head',steel,.012,(0,-1,-.4))
    poly('rear helmet',[(-.075,.112,1.689),(.075,.112,1.689),(.095,.126,1.634),(.063,.122,1.565),(-.063,.122,1.565),(-.095,.126,1.634)],'Head',steel,.010,(0,1,0),True)
    for z in [1.61,1.625,1.64]:line('rear vent',[(-.035,.137,z),(.035,.137,z)],'Head',seal,.002)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f18-heavy-agile.blend'))
    g.rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');g.parts=[]
    surfaces();foundation();torso();limbs();helmet()
    bpy.context.preferences.filepaths.save_version=0
    target=ROOT/'scripts/vector_fighter/f19-reference.blend';bpy.ops.wm.save_as_mainfile(filepath=str(target))
    print('REFERENCE_SOURCE',target)

if __name__=='__main__':main()
