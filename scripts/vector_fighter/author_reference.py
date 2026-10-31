"""F19 reference reconstruction. Offline authored geometry; retained bind rig/FPS.

Rebuilds the rejected F18 broad forms rather than adding surface noise to them.
Maps are authored tileable PBR surfaces; dimensions here do not tune gameplay.
"""
import sys, math, hashlib
from pathlib import Path
import bpy, bmesh, numpy as np
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
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
    # Inspectable image sources: retain restrained pigment variation, with relief
    # derived from the same luminance rather than unrelated procedural damage.
    def sampled(filename):
        src=bpy.data.images.load(str(ROOT/'scripts/vector_fighter/textures'/filename),check_existing=False)
        src.scale(n,n);pixels=np.empty(n*n*4,dtype=np.float32);src.pixels.foreach_get(pixels)
        rgb=pixels.reshape(n,n,4)[:,:,:3].copy();bpy.data.images.remove(src)
        return rgb
    coating_rgb=sampled('reference-coating-albedo.png')
    lum=coating_rgb.mean(axis=2)
    coating=np.clip(.22+(lum-lum.mean())*.15+scratches*.055,.16,.33)
    coat=image('vector-reference-coating-albedo',coating,True)
    coatnormal=image('vector-reference-coating-normal',normal((lum-lum.mean())*.003-scratches*.0015,8))
    orm=image('vector-reference-coating-ORM',np.stack([np.ones_like(x),np.clip(.56+(lum-lum.mean())*.4,.43,.70),np.full_like(x,.32)],axis=2))
    # Interlaced warp and weft: alternating over/under yarns, not glossy rubber.
    u=x*64;v=y*64;warp=np.cos((u%1-.5)*math.pi);weft=np.cos((v%1-.5)*math.pi)
    alternate=(np.floor(u)+np.floor(v))%2
    weave=warp*(1-alternate)+weft*alternate
    textile=np.clip(.024+weave*.015+grain*.0015,.012,.05)
    cloth_rgb=sampled('reference-cloth-albedo.png');cloth_lum=cloth_rgb.mean(axis=2)
    textilecolor=image('vector-reference-ballistic-weave',np.clip(cloth_rgb*.72,.009,.12),True)
    textilenormal=image('vector-reference-weave-normal',normal(cloth_lum*.035,14))
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
    paintmap=image('vector-reference-paint-albedo',np.clip(.54+(coating-.22)*1.6,.39,.69),True)
    paint=mat('vector-fighter-identity-reference',(.41,)*3,.25,.65,paintmap,coatnormal,orm)
    edge=mat('vector-reference-edge-metal',(.065,.075,.085),.55,.55,None,coatnormal)
    cloth=mat('vector-reference-woven-fabric',(.1,)*3,0,.9,textilecolor,textilenormal)
    webbing=mat('vector-reference-webbing',(.055,)*3,0,.95,textilecolor,textilenormal)
    seal=mat('vector-reference-flexible-seals',(.022,.025,.028),0,.85,None,textilenormal)
    glass=mat('vector-reference-opaque-visor',(.006,.009,.012),.15,.30)
    glass.node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=.25


def poly(name,points,bone,material=None,depth=.012,normal_axis=None,hardware=False):
    """Closed fitted plate with a flat primary face, deliberate chamfer and gasket."""
    p=[Vector(v) for v in points];n=len(p);center=sum(p,Vector())/n
    requested=Vector(normal_axis) if normal_axis else (p[1]-p[0]).cross(p[2]-p[0]).normalized()
    # Fit a single manufactured face. Nonplanar control outlines formerly made
    # arbitrary triangle fans, which showed as broken glass across broad armor.
    cloud=np.array([tuple(v-center) for v in p]);_,_,basis=np.linalg.svd(cloud,full_matrices=False)
    axis=Vector(basis[-1]);axis*=1 if axis.dot(requested)>=0 else -1
    p=[v-axis*(v-center).dot(axis) for v in p]
    verts=[]
    for scale,offset in [(1,0),(.965,.0015),(1,-depth)]:
        verts.extend(center+(v-center)*scale+axis*offset for v in p)
    faces=[];face_materials=[]
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,n+j,n+i),(i,2*n+i,2*n+j,j)])
        face_materials.extend([1,2])
    faces.extend([tuple(range(n,2*n)),tuple(reversed(range(2*n,3*n)))])
    face_materials.extend([0,2])
    o=g.finish('VECTOR R '+name,verts,faces,material or steel,bone)
    o['reference_plate']='planar'
    o.data.materials.append(edge);o.data.materials.append(seal)
    for f,slot in zip(o.data.polygons,face_materials):f.material_index=slot;f.use_smooth=False
    # Stable physical texel scale with per-panel offset prevents repeated scratches.
    offset=int(hashlib.sha256(name.encode()).hexdigest()[:4],16)/65535
    axes=(0,2) if abs(axis.y)>.5 else (1,2) if abs(axis.x)>.5 else (0,1)
    for f in o.data.polygons:
        for li in f.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co;o.data.uv_layers.active.data[li].uv=(v[axes[0]]*3+offset,v[axes[1]]*3+offset*.7)
    if hardware:
        for i in [0,n//3,2*n//3]:fastener(name+' screw',p[i].lerp(center,.18)+axis*.005,axis,bone)
    return o


def fastener(name,p,axis,bone):
    # Hexagonal recessed socket with dark center, shared materials.
    q=Vector((0,0,1)).rotation_difference(Vector(axis));verts=[];faces=[];count=6
    for radius,z in [(.0022,0),(.0015,.0004),(.00085,.0004),(.00085,-.0006)]:
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
                # Raise the central crotch bridge; avoid a straight apron hem.
                if .76<p.z<.95 and abs(p.x)<.075:
                    p.z+=.065*(1-abs(p.x)/.075)*max(0,1-abs(p.z-.85)/.10)
            elif 'UpperArm fabric' in o.name:
                # Keep wrist contacts, narrow only the soft sleeve around the bone line.
                side='Left' if p.x>0 else 'Right';a=g.rig.data.bones[side+'UpperArm'].head_local;b=g.rig.data.bones[side+'Hand'].head_local
                d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared));anchor=a+d*t;p=anchor+(p-anchor)*.88
                shoulder=g.rig.data.bones[side+'UpperArm'];d=shoulder.tail_local-a
                t=(p-a).dot(d)/d.length_squared
                if t<.72:
                    anchor=a+d*t;radial=p-anchor
                    limit=.073*math.sqrt(max(.04,1-((t-.18)/.60)**2))
                    if radial.length>limit:p=anchor+radial.normalized()*limit
            v.co=o.matrix_world.inverted()@p
        o.data.update()
        for face in o.data.polygons:
            normal=face.normal;axes=(0,2) if abs(normal.y)>.5 else (1,2) if abs(normal.x)>.5 else (0,1)
            for li in face.loop_indices:
                point=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co
                o.data.uv_layers.active.data[li].uv=(point[axes[0]]*20,point[axes[1]]*20)


def torso():
    front=lambda x,z:(x,-.137+1.25*x*x,z)
    g.loft('VECTOR R fitted ballistic vest',[(0,.029,z,rx,ry) for z,rx,ry in [(1.26,.166,.107),(1.29,.196,.128),(1.35,.210,.130),(1.41,.195,.120),(1.448,.123,.077)]],cloth,'Chest',N=64)
    # Backing shell and independent pectoral panels produce actual layered construction.
    outline=[(-.065,1.465),(.065,1.465),(.199,1.414),(.226,1.337),(.166,1.253),(.063,1.238),(-.063,1.238),(-.166,1.253),(-.226,1.337),(-.199,1.414)]

    for sign in [-1,1]:
        pts=[(.009,1.431),(.070,1.449),(.188,1.404),(.206,1.350),(.144,1.315),(.015,1.337)]
        poly('pectoral plate '+str(sign),[(sign*x,front(x,z)[1]-.014,z) for x,z in pts],'Chest',steel,.010,(0,-1,0),True)
        poly('chest identity '+str(sign),[(sign*x,front(x,z)[1]-.019,z) for x,z in [(.151,1.416),(.187,1.400),(.201,1.355),(.173,1.337),(.144,1.361),(.139,1.393)]],'Chest',paint,.005,(0,-1,0),True)
        poly('costal plate '+str(sign),[(sign*x,front(x,z)[1]-.009,z) for x,z in [(.013,1.323),(.142,1.300),(.199,1.338),(.185,1.281),(.102,1.256),(.028,1.271)]],'Chest',steel,.012,(0,-1,0),True)
        # Side wraps join front armor to rear panels; no floating breastplate patches.
        poly('thoracic wrap '+str(sign),[(sign*.203,-.075,1.389),(sign*.217,.009,1.371),(sign*.197,.113,1.345),(sign*.171,.103,1.267),(sign*.183,-.059,1.274)],'Chest',steel,.013,(sign,0,0),True)
        poly('clavicle mount '+str(sign),[(sign*x,y,z) for x,y,z in [(.055,-.062,1.474),(.10,-.065,1.474),(.192,-.042,1.422),(.174,-.085,1.412),(.091,-.104,1.447),(.058,-.103,1.447)]],'UpperChest',steel,.012,(0,-1,1),True)
        poly('rear scapula '+str(sign),[(sign*x,.15-.5*x*x,z) for x,z in [(.009,1.447),(.10,1.449),(.204,1.383),(.187,1.290),(.101,1.260),(.018,1.293)]],'Chest',steel,.015,(0,1,0),True)
        poly('rear ID '+str(sign),[(sign*x,.159-.5*x*x,z) for x,z in [(.045,1.418),(.10,1.418),(.158,1.384),(.145,1.358),(.096,1.383),(.045,1.386)]],'Chest',paint,.006,(0,1,0))
    # Central sternum module, collar rail and inset service interfaces break up
    # the chest into fitted manufactured assemblies rather than broad flaps.
    poly('sternum bridge',[(-.065,-.157,1.447),(.065,-.157,1.447),(.087,-.157,1.418),(.074,-.157,1.382),(.025,-.157,1.362),(-.025,-.157,1.362),(-.074,-.157,1.382),(-.087,-.157,1.418)],'Chest',steel,.010,(0,-1,0),True)
    poly('sternum recess',[(-.047,-.160,1.428),(.047,-.160,1.428),(.042,-.160,1.416),(-.042,-.160,1.416)],'Chest',seal,.002,(0,-1,0))
    poly('lower sternum',[(-.023,-.144,1.344),(.023,-.144,1.344),(.039,-.144,1.319),(.025,-.144,1.293),(-.025,-.144,1.293),(-.039,-.144,1.319)],'Chest',steel,.008,(0,-1,0),True)
    for sign in [-1,1]:
        line('collar rail '+str(sign),[(sign*.045,-.107,1.464),(sign*.088,-.108,1.458),(sign*.171,-.081,1.416)],'UpperChest',edge,.004)
        poly('clavicle connector '+str(sign),[(sign*x,y,z) for x,y,z in [(.080,-.114,1.470),(.107,-.109,1.459),(.123,-.099,1.439),(.096,-.109,1.447)]],'UpperChest',edge,.006,(0,-1,1),True)
        for z in [1.448,1.435]:
            line('clavicle inset '+str(sign),[(sign*.102,-.117,z),(sign*.114,-.112,z-.005)],'UpperChest',paint,.002)
        # Offset subpanels on the broad pectoral surface, with a narrow gasket.
        poly('pectoral service inset '+str(sign),[(sign*x,front(x,z)[1]-.018,z) for x,z in [(.095,1.404),(.119,1.395),(.128,1.368),(.108,1.358),(.092,1.376)]],'Chest',edge,.003,(0,-1,0))
        for z in [1.388,1.380]:
            line('chest service groove '+str(sign),[(sign*.105,-.147,z),(sign*.119,-.141,z-.006)],'Chest',seal,.0009)
        # A short fitted diagonal joint gives the outer pectoral a layered return
        # without a broad floating flap across its lower edge.
        poly('pectoral articulated return '+str(sign),[(sign*x,front(x,z)[1]-.018,z) for x,z in [(.044,1.356),(.088,1.354),(.119,1.335),(.101,1.322),(.047,1.338)]],'Chest',steel,.004,(0,-1,0))
    for i,(top,bottom,width) in enumerate([(1.253,1.194,.144),(1.191,1.131,.129),(1.128,1.073,.112)]):
        pts=[(-width,top),(-.045,top-.009),(.045,top-.009),(width,top),(width*.80,bottom+.016),(.04,bottom),(-.04,bottom),(-width*.80,bottom+.016)]
        for sign in [-1,1]:
            half=[(.010,top-.009),(width*.85,top+.006),(width,top-.009),(width*.80,bottom+.016),(.038,bottom),(.010,bottom+.014)]
            poly('abdomen '+str(i)+' '+str(sign),[(sign*x,-.123+x*x,z) for x,z in half],'Spine',steel,.008,(0,-1,0),True)
            line('abdominal seam '+str(i)+str(sign),[(sign*.040,-.130,top-.015),(sign*.087,-.119,bottom+.019)],'Spine',edge,.0012)
        poly('abdominal keystone '+str(i),[(-.022,-.137,top-.014),(.022,-.137,top-.014),(.030,-.137,bottom+.014),(0,-.137,bottom+.004),(-.030,-.137,bottom+.014)],'Spine',edge,.006,(0,-1,0))
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
        poly('pelvic wing '+str(sign),[(sign*x,y,z) for x,y,z in [(.075,-.073,1.01),(.158,-.055,1.00),(.178,-.035,.956),(.160,-.055,.928),(.123,-.081,.925),(.086,-.094,.968)]],'Hips',steel,.008,(0,-1,0),True)
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
    def surface(p,offset):
        angle,t=p;radial=lateral*math.sin(angle)+front*math.cos(angle)
        if name.startswith(('shoulder tier','shoulder rear')):offset+=[0,.006,.014][int(name.rsplit(' ',1)[-1])]
        elif name=='shoulder identity inset':offset+=.003
        scale=math.sqrt(max(.04,1-((t-.18)/.60)**2)) if name.startswith('shoulder') else 1
        return a.lerp(b,t)+lateral*(rx*scale*math.sin(angle))+front*(ry*scale*math.cos(angle))+radial*offset
    return mapped_plate(name+' '+side,outline,side+part,mat or steel,surface,lambda q:lateral*math.sin(q.x)+front*math.cos(q.x),(rx*3,(b-a).length*3))

def mapped_plate(name,outline,bone,mat,surface,outward,uv_scale,refinement=3):
    corners=[Vector(p) for p in outline]
    uv=[p.lerp(corners[(i+1)%len(corners)],j/3) for i,p in enumerate(corners) for j in range(3)]
    center=sum(uv,Vector((0,0)))/len(uv)
    coords=[center+(q-center)*.955 for q in uv]
    verts=[surface(q,.0018) for q in coords]
    planar=[Vector((q.x,q.y,0)) for q in coords]
    triangles=[tuple(v if isinstance(v,int) else planar.index(v) for v in tri) for tri in tessellate_polygon([planar])]
    # Refine in parameter space, evaluating the actual shell at every point.
    for level in range(refinement):
        cache={};refined=[]
        def midpoint(i,j):
            key=tuple(sorted((i,j)))
            if key not in cache:
                q=(coords[i]+coords[j])*.5;cache[key]=len(verts);coords.append(q);verts.append(surface(q,.0018))
            return cache[key]
        for i,j,k in triangles:
            ij=midpoint(i,j);jk=midpoint(j,k);ki=midpoint(k,i)
            refined.extend([(i,ij,ki),(ij,j,jk),(ki,jk,k),(ij,jk,ki)])
        triangles=refined
    # Recover the refined boundary so the bevel shares every edge vertex.
    # This avoids T junctions between high-density fronts and coarse rims.
    edges={}
    for tri in triangles:
        for i,j in zip(tri,tri[1:]+tri[:1]):
            key=tuple(sorted((i,j)));edges[key]=edges.get(key,0)+1
    adjacent={}
    for (i,j),count in edges.items():
        if count==1:adjacent.setdefault(i,[]).append(j);adjacent.setdefault(j,[]).append(i)
    assert all(len(neighbors)==2 for neighbors in adjacent.values()),name+' simple boundary'
    boundary=[min(adjacent)];previous=None
    while True:
        current=boundary[-1];nxt=next(i for i in adjacent[current] if i!=previous)
        if nxt==boundary[0]:break
        boundary.append(nxt);previous=current
        assert len(boundary)<=len(adjacent),name+' closed boundary'
    faces=list(triangles);slots=[0]*len(triangles);backmap={}
    for index in sorted({i for tri in triangles for i in tri}):
        q=coords[index];backmap[index]=len(verts);coords.append(q.copy());verts.append(surface(q,-.005))
    faces.extend(tuple(backmap[i] for i in reversed(tri)) for tri in triangles);slots.extend([2]*len(triangles))
    outer={}
    for index in boundary:
        q=center+(coords[index]-center)/.955;outer[index]=len(verts);coords.append(q);verts.append(surface(q,0))
    for i,j in zip(boundary,boundary[1:]+boundary[:1]):
        faces.extend([(outer[i],outer[j],j,i),(outer[i],backmap[i],backmap[j],outer[j])]);slots.extend([1,2])
    obj=g.finish('VECTOR R '+name,verts,faces,mat,bone)
    obj['reference_plate']='mapped'
    obj.data.materials.append(edge);obj.data.materials.append(seal)
    # Reorient the entire closed solid together; preserve coherent winding
    # across front, bevel and underside (glTF back-face lighting uses it).
    orientation=0
    for face,slot in zip(obj.data.polygons,slots):
        if slot==0:
            q=sum((coords[obj.data.loops[li].vertex_index] for li in face.loop_indices),Vector((0,0)))/len(face.loop_indices)
            orientation+=face.normal.dot(outward(q))*face.area
    if orientation<0:
        for face in obj.data.polygons:face.flip()
    obj.data.update();normals=[]
    # Derive shading from the actual sampled front, excluding bevel/underside.
    # Offset-surface differential normals near tight crown curvature produced
    # large false streaks despite a regular, closed geometric surface.
    front_normals={}
    for face,slot in zip(obj.data.polygons,slots):
        if slot==0:
            for index in face.vertices:front_normals[index]=front_normals.get(index,Vector())+face.normal*face.area
    for index in front_normals:front_normals[index].normalize()
    for face,slot in zip(obj.data.polygons,slots):
        face.material_index=slot;face.use_smooth=slot==0
        for li in face.loop_indices:
            index=obj.data.loops[li].vertex_index;q=coords[index]
            obj.data.uv_layers.active.data[li].uv=(q.x*uv_scale[0],q.y*uv_scale[1])
            normals.append(front_normals[index] if slot==0 else face.normal)
    obj.data.normals_split_custom_set(normals)
    for i in [0,len(uv)//4,len(uv)//2,3*len(uv)//4]:
        q=uv[i].lerp(center,.18)
        if mat!=glass:fastener(name+' screw',surface(q,.0045),outward(q),bone)
    return obj


def limbs():
    for side in ['Left','Right']:
        a,b,f,l=limb_frame(side,'UpperArm');verts=[];faces=[];n=24
        for t in [-.36,-.25,-.10,.08,.35,.68]:
            r=.089*math.sqrt(max(.04,1-((t-.18)/.60)**2))
            for j in range(n):
                angle=j*math.tau/n;verts.append(a.lerp(b,t)+l*(r*math.sin(angle))+f*(r*math.cos(angle)))
        for row in range(5):
            for j in range(n):k=(j+1)%n;faces.append((row*n+j,row*n+k,(row+1)*n+k,(row+1)*n+j))
        faces.extend([tuple(reversed(range(n))),tuple(range(5*n,6*n))])
        g.finish('VECTOR R shoulder backing '+side,verts,faces,seal,side+'UpperArm')
        # Three nested shoulder shells, compact and sharply bevelled instead of spheres.
        for i,(start,end,rx,ry) in enumerate([(-.32,.28,.100,.102),(.24,.48,.102,.104),(.44,.68,.098,.100)]):
            outline=[(-1.40,start+.08),(-.66,start),(.48,start+.015),(1.36,start+.10),(1.18,end-.05),(.40,end),(-.54,end-.025),(-1.24,end-.07)]
            limb_plate('shoulder tier '+str(i),side,'UpperArm',outline,rx,ry,steel)
            limb_plate('shoulder rear '+str(i),side,'UpperArm',outline,rx,ry,steel,True)
        limb_plate('shoulder identity inset',side,'UpperArm',[(.35,.01),(1.12,.08),(1.16,.23),(.82,.29),(.39,.23)],.104,.106,paint)
        # Bicep guard and close-set forearm gauntlet (wrist remains fixed).
        limb_plate('bicep guard',side,'UpperArm',[(-1.1,.72),(1.1,.72),(.95,.85),(.5,.93),(-.55,.92),(-.98,.81)],.081,.085)
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
        # Articulated surface assemblies: narrower floating plates, with inset
        # panels and longitudinal rails fitted to the identical limb surface.
        limb_plate('thigh inner overlap',side,'UpperLeg',[(-.83,.13),(-.36,.06),(-.12,.12),(-.18,.64),(-.55,.77),(-.82,.62)],.104,.113)
        limb_plate('thigh central insert',side,'UpperLeg',[(-.10,.25),(.35,.20),(.65,.29),(.58,.63),(.22,.77),(-.12,.65)],.105,.114)
        limb_plate('thigh outer service',side,'UpperLeg',[(1.13,.29),(1.57,.32),(1.63,.60),(1.23,.67)],.111,.114,edge)
        limb_plate('knee floating face',side,'LowerLeg',[(-.64,.00),(-.27,-.035),(.36,-.03),(.69,.04),(.48,.135),(.05,.17),(-.51,.12)],.072,.080)
        limb_plate('shin outer overlap',side,'LowerLeg',[(.52,.31),(.91,.37),(.71,.81),(.34,.91),(.23,.76)],.071,.088)
        limb_plate('shin service inset',side,'LowerLeg',[(-.51,.40),(-.30,.38),(-.26,.72),(-.41,.78)],.070,.088,edge)
        limb_plate('forearm lateral shell',side,'LowerArm',[(.65,.12),(1.40,.18),(1.45,.64),(.87,.81),(.68,.70)],.068,.072)
        limb_plate('forearm inset',side,'LowerArm',[(-.15,.25),(.18,.23),(.20,.64),(-.12,.69)],.071,.078,edge)
        limb_plate('forearm color channel',side,'LowerArm',[(-.05,.29),(.07,.28),(.08,.61),(-.04,.63)],.073,.081,paint)
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
        sole=poly(side+' boot sole',[(x+u,v,.033) for u,v in [(-.043,-.157),(.043,-.157),(.063,-.138),(.068,-.085),(.059,.070),(.045,.104),(-.045,.104),(-.059,.070),(-.068,-.085),(-.063,-.138)]],side+'Foot',seal,.023,(0,0,1))
        for face in sole.data.polygons:face.material_index=0
        for index,y in enumerate([-.137,-.102,-.067,-.032,.003,.038,.073]):
            for sign in [-1,1]:
                g.box('VECTOR R '+side+' sole tread '+str(index)+str(sign),(x+sign*.058,y,.017),(.018,.020,.014),seal,side+'Foot',.002)
        for sign in [-1,1]:
            poly(side+' heel guard '+str(sign),[(x+sign*.066,y,z) for y,z in [(.020,.045),(.095,.038),(.102,.112),(.059,.137),(.029,.112)]],side+'Foot',steel,.005,(sign,0,0),True)
            poly(side+' ankle overlap '+str(sign),[(x+sign*.063,y,z) for y,z in [(-.028,.087),(.025,.092),(.049,.140),(.020,.155),(-.023,.139)]],side+'Foot',edge,.004,(sign,0,0),True)
        # Subtle sewn channels follow the actual cloth instead of floating
        # as armor. Tiny stitches are retained in the art-stage source.
        for part,angle,start,end,radius in [('UpperLeg',-1.42,.16,.90,.089),('LowerLeg',1.72,.23,.84,.058)]:
            a,b,f,l=limb_frame(side,part)
            seam=[a.lerp(b,t)+l*(radius*math.sin(angle))+f*(radius*math.cos(angle)) for t in np.linspace(start,end,24)]
            line(side+' tailored seam '+part,seam,side+part,webbing,.0015)
            for index in range(28):
                t=start+(end-start)*index/28;c=a.lerp(b,t)+l*(radius*math.sin(angle))+f*(radius*math.cos(angle))
                line(side+' stitch '+part+str(index),[c-l*.0012,c+l*.0012],side+part,cloth,.00045)
        line(side+' boot welt',[(x-.060,-.143,.022),(x-.068,-.05,.022),(x-.055,.080,.022),(x+.055,.080,.022),(x+.068,-.05,.022),(x+.060,-.143,.022)],side+'Foot',seal,.003)


def helmet():
    # One coherent enclosure under individually fitted crown, cheek and jaw
    # plates. Surface coordinates are azimuth/z, with explicit face silhouette.
    stations=[(1.495,.045,.087),(1.53,.073,.169),(1.60,.108,.158),(1.665,.114,.146),(1.70,.103,.120),(1.733,.075,.082),(1.751,.016,.025)]
    def profile(z,column):
        i=max(0,min(len(stations)-2,int(np.searchsorted([r[0] for r in stations],z)-1)))
        z0,z1=stations[i][0],stations[i+1][0];t=max(0,min(1,(z-z0)/(z1-z0)))
        j=max(0,i-1);k=min(len(stations)-1,i+2)
        m0=(stations[i+1][column]-stations[j][column])/(stations[i+1][0]-stations[j][0])
        m1=(stations[k][column]-stations[i][column])/(stations[k][0]-stations[i][0])
        return (2*t**3-3*t*t+1)*stations[i][column]+(t**3-2*t*t+t)*(z1-z0)*m0+(-2*t**3+3*t*t)*stations[i+1][column]+(t**3-t*t)*(z1-z0)*m1
    def base(q):
        angle,z=q;rx=profile(z,1);ry=profile(z,2)
        # Rear cranium is shallower than the projecting face shield.
        depth=ry if math.cos(angle)>0 else ry*.78
        return Vector((rx*math.sin(angle),.016-depth*math.cos(angle),z))
    def outward(q):
        eps=.00001;du=base(q+Vector((eps,0)))-base(q-Vector((eps,0)));dv=base(q+Vector((0,eps)))-base(q-Vector((0,eps)))
        n=du.cross(dv).normalized();radial=Vector((math.sin(q.x),-math.cos(q.x),0))
        return n if n.dot(radial)>0 else -n
    def surface(q,offset):return base(q)+outward(q)*offset
    def plate(name,outline,material= None):return mapped_plate(name,outline,'Head',material or steel,surface,outward,(.35,3))
    verts=[];faces=[];segments=64;rows=48
    for z in np.linspace(1.495,1.751,rows):
        for j in range(segments):
            point=base(Vector((j*math.tau/segments,z)));point.x*=.90;point.y=.016+(point.y-.016)*.90;verts.append(point)
    for row in range(rows-1):
        for j in range(segments):
            k=(j+1)%segments;faces.append((row*segments+j,row*segments+k,(row+1)*segments+k,(row+1)*segments+j))
    faces.extend([tuple(reversed(range(segments))),tuple(range((rows-1)*segments,rows*segments))])
    g.finish('VECTOR R helmet inner shell',verts,faces,seal,'Head')
    plate('opaque visor',[(-.82,1.674),(-.18,1.686),(.18,1.686),(.82,1.674),(.79,1.605),(.40,1.532),(-.40,1.532),(-.79,1.605)],glass)
    for sign in [-1,1]:
        def mirrored(points):return [(sign*a,z) for a,z in points]
        plate('brow fascia '+str(sign),mirrored([(.03,1.688),(.50,1.708),(1.02,1.695),(.91,1.667),(.61,1.674),(.03,1.679)]))
        plate('crown central '+str(sign),mirrored([(.035,1.713),(.54,1.721),(.90,1.741),(.78,1.749),(.04,1.749)]))
        plate('crown lateral '+str(sign),mirrored([(.57,1.717),(1.08,1.704),(1.82,1.698),(2.17,1.721),(1.65,1.741),(.97,1.744)]))
        plate('temple shell '+str(sign),mirrored([(1.04,1.690),(1.80,1.686),(2.12,1.664),(2.03,1.617),(1.74,1.601),(1.18,1.619),(.93,1.645)]))
        plate('occipital shell '+str(sign),mirrored([(2.21,1.715),(3.10,1.714),(3.10,1.661),(2.76,1.644),(2.15,1.657)]))
        plate('rear lower shell '+str(sign),mirrored([(2.13,1.644),(2.76,1.630),(3.10,1.642),(3.10,1.568),(2.50,1.554),(1.98,1.592)]))
        plate('cheek shell '+str(sign),mirrored([(.88,1.646),(1.20,1.630),(1.52,1.600),(1.40,1.568),(.89,1.543),(.47,1.517),(.42,1.539),(.79,1.603)]))
        plate('cheek inset '+str(sign),mirrored([(1.00,1.616),(1.27,1.606),(1.33,1.583),(.98,1.564),(.74,1.553),(.86,1.583)]),edge)
        plate('mandible edge '+str(sign),mirrored([(.46,1.532),(.85,1.546),(1.15,1.558),(1.12,1.537),(.71,1.515),(.30,1.507),(.29,1.521)]))
        # Communications disc is an assembled socket with a nested inner cover.
        x=sign*.121
        poly('ear gasket '+str(sign),[(x,y,z) for y,z in [(-.022,1.655),(.018,1.674),(.063,1.652),(.070,1.614),(.030,1.585),(-.016,1.604)]],'Head',seal,.008,(sign,0,0))
        poly('ear armor '+str(sign),[(x+sign*.006,y,z) for y,z in [(-.015,1.650),(.019,1.665),(.054,1.646),(.059,1.618),(.028,1.594),(-.009,1.611)]],'Head',steel,.006,(sign,0,0),True)
        poly('ear inset '+str(sign),[(x+sign*.010,y,z) for y,z in [(.007,1.647),(.024,1.651),(.038,1.638),(.036,1.614),(.023,1.607),(.006,1.620)]],'Head',edge,.003,(sign,0,0))
        line('ear channel '+str(sign),[(x+sign*.014,.030,1.645),(x+sign*.014,.030,1.619),(x+sign*.014,.021,1.612)],'Head',paint,.0016)
        line('visor edge '+str(sign),[surface(Vector((sign*a,z)),.003) for a,z in [(.78,1.666),(.76,1.606),(.38,1.536)]],'Head',edge,.0018)
    plate('chin closure',[(-.29,1.526),(.29,1.526),(.32,1.507),(-.32,1.507)])
    line('visor identity inset',[surface(Vector((-.73,z)),.004) for z in [1.615,1.594,1.576]],'Head',paint,.0014)
    for z in [1.595,1.609,1.623]:
        line('helmet rear vent',[(-.028,.133,z),(.028,.133,z)],'Head',edge,.0018)


def textile_uvs():
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or obj.name=='weapon:joined':continue
        for face in obj.data.polygons:
            if obj.data.materials[face.material_index] not in [cloth,webbing]:continue
            axis=face.normal;axes=(0,2) if abs(axis.y)>.5 else (1,2) if abs(axis.x)>.5 else (0,1)
            for li in face.loop_indices:
                point=obj.matrix_world@obj.data.vertices[obj.data.loops[li].vertex_index].co
                obj.data.uv_layers.active.data[li].uv=(point[axes[0]]*20,point[axes[1]]*20)


def validate_plates():
    counts={'planar':0,'mapped':0}
    for obj in bpy.context.scene.objects:
        role=obj.get('reference_plate')
        if not role:continue
        bm=bmesh.new();bm.from_mesh(obj.data)
        assert all(edge.is_manifold and edge.is_contiguous for edge in bm.edges),obj.name+' closed coherent surface'
        assert all(all(math.isfinite(c) for c in vertex.co) for vertex in bm.verts),obj.name+' finite vertices'
        bm.free();counts[role]+=1
    print('REFERENCE_SOURCE_SOLIDS',counts)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f18-heavy-agile.blend'))
    g.rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');g.parts=[]
    surfaces();foundation();torso();limbs();helmet();textile_uvs();validate_plates()
    bpy.context.preferences.filepaths.save_version=0
    target=ROOT/'scripts/vector_fighter/f19-reference.blend';bpy.ops.wm.save_as_mainfile(filepath=str(target))
    print('REFERENCE_SOURCE',target)

if __name__=='__main__':main()
