"""F22: authored facet cages, nested armor construction, no runtime promotion.
Rebuild entire hard costume on retained F21 rig/textile/gloves. No reduction.
"""
import bpy,bmesh,sys,math,json,hashlib,random
from pathlib import Path
from mathutils import Vector
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import build as g
import author_reference as r
from study_upper import profile
OUT=ROOT/'.local/vector-f22';OUT.mkdir(exist_ok=True,parents=True)
solids=[]
PANEL_GRID=48
SHELL_BEVEL_SEGMENTS=2
SOURCE_OUTPUT=ROOT/'scripts/vector_fighter/f22-reconstruction.blend'

def solid(name,points,bone,mat=None,axis=(0,-1,0),depth=.009,crown=.003,inset=.12,hardware=True):
    """Explicit broad center face, sloped perimeter, full return and inner wall.
    Center is planar; perimeter preserves the sculpted 3D cage. No smooth dome.
    """
    p=[Vector(x) for x in points];n=len(p);requested=Vector(axis).normalized();c=sum(p,Vector())/n
    # Fit the actual authored patch, never project a tilted panel onto the
    # global forward plane. Seat the face on the outermost cage support point.
    cloud=np.array([tuple(v-c) for v in p]);_,_,basis=np.linalg.svd(cloud,full_matrices=False)
    normal=Vector(basis[-1]);normal*=1 if normal.dot(requested)>0 else -1
    distance=max((v-c).dot(normal) for v in p)
    p=[v+normal*(distance-(v-c).dot(normal)+.001) for v in p]
    c=sum(p,Vector())/n
    face=[c+(v-c)*(1-inset)+normal*crown for v in p]
    verts=p+face+[v-normal*depth for v in p]
    faces=[];slots=[]
    for i in range(n):
        j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(i,2*n+i,2*n+j,j)]);slots.extend([0,1])
    faces.extend([tuple(range(n,2*n)),tuple(reversed(range(2*n,3*n)))]);slots.extend([0,2])
    obj=g.finish('VECTOR F22 '+name,verts,faces,mat or r.steel,bone)
    obj.data.materials.append(r.glass if mat==r.glass else r.edge);obj.data.materials.append(r.seal)
    for f,slot in zip(obj.data.polygons,slots):f.material_index=slot;f.use_smooth=False
    # World-space planar mapping preserves a physical grain size per facet.
    seed=int(hashlib.sha256(name.encode()).hexdigest()[:6],16)/0xffffff
    for f in obj.data.polygons:
        axes=(0,2) if abs(f.normal.y)>.5 else (1,2) if abs(f.normal.x)>.5 else (0,1)
        for li in f.loop_indices:
            v=obj.data.vertices[obj.data.loops[li].vertex_index].co
            obj.data.uv_layers.active.data[li].uv=(v[axes[0]]*3.3+seed,v[axes[1]]*3.3+seed*.73)
    obj.modifiers.remove(obj.modifiers['Skin'])
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    m=obj.modifiers.new('Tool radius','BEVEL');m.width=.0008;m.segments=3;m.affect='EDGES';m.harden_normals=True;m.material=1
    bpy.ops.object.modifier_apply(modifier=m.name)
    # Keep broad facets flat; bevel normals alone interpolate across the radius.
    obj.select_set(False);m=obj.modifiers.new('Skin','ARMATURE');m.object=g.rig
    obj['f22_closed_armor']=True;solids.append(obj)
    if hardware:
        for i in [0,n//2]:r.fastener(name+' countersunk',p[i].lerp(c,.16)+normal*(crown+.001),normal,bone)
    return obj

def shellpanel(name,coords,bone,surface,outward,mat=None):
    """Sample a closed surface with a regular, clipped parameter grid.
    The grid follows the shared cage and avoids long interior fan triangles.
    """
    from mathutils.geometry import tessellate_polygon
    outline=[Vector((u,v)) for u,v in coords]
    center=sum(outline,Vector((0,0)))/len(outline)
    inner=[center+(q-center)*.975 for q in outline]
    poly3=[Vector((q.x,q.y,0)) for q in inner]
    triangles=tessellate_polygon([poly3])
    xmin=min(q.x for q in inner);xmax=max(q.x for q in inner)
    ymin=min(q.y for q in inner);ymax=max(q.y for q in inner)
    nx=PANEL_GRID;ny=PANEL_GRID;dx=(xmax-xmin)/nx;dy=(ymax-ymin)/ny
    def clipped(poly,axis,value,positive):
        result=[]
        for a,b in zip(poly,poly[1:]+poly[:1]):
            da=(a[axis]-value)*positive;db=(b[axis]-value)*positive
            if da>=-1e-10:result.append(a)
            if (da>1e-10 and db < -1e-10) or (da < -1e-10 and db>1e-10):result.append(a.lerp(b,da/(da-db)))
        return result
    uv=[];lookup={};faces=[]
    def index(q):
        key=(round(q.x,7),round(q.y,7))
        if key not in lookup:lookup[key]=len(uv);uv.append(Vector(key))
        return lookup[key]
    for tri in triangles:
        tri=[Vector((poly3[v].x,poly3[v].y)) if isinstance(v,int) else Vector((v.x,v.y)) for v in tri]
        ix0=max(0,int((min(q.x for q in tri)-xmin)/dx));ix1=min(nx-1,int((max(q.x for q in tri)-xmin)/dx))
        iy0=max(0,int((min(q.y for q in tri)-ymin)/dy));iy1=min(ny-1,int((max(q.y for q in tri)-ymin)/dy))
        for j in range(iy0,iy1+1):
            for i in range(ix0,ix1+1):
                poly=tri
                for axis,value,positive in [(0,xmin+i*dx,1),(0,xmin+(i+1)*dx,-1),(1,ymin+j*dy,1),(1,ymin+(j+1)*dy,-1)]:
                    poly=clipped(poly,axis,value,positive)
                    if len(poly)<3:break
                ids=[]
                for q in poly:
                    k=index(q)
                    if not ids or k!=ids[-1]:ids.append(k)
                if ids and ids[-1]==ids[0]:ids.pop()
                for k in range(1,len(ids)-1):
                    a,b,c=ids[0],ids[k],ids[k+1]
                    if len({a,b,c})==3 and abs((uv[b]-uv[a]).cross(uv[c]-uv[a]))>1e-13:faces.append((a,b,c))
    # Remove duplicate triangles from numerically coincident clipped edges.
    faces=list({tuple(sorted(f)):f for f in faces}.values())
    # Weld numerical boundary intersections, including any subpixel slivers.
    verts=[surface(q)+outward(q).normalized()*.002 for q in uv]
    obj=g.finish('VECTOR F22 '+name,verts,faces,mat or r.steel,bone)
    obj.modifiers.remove(obj.modifiers['Skin'])
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000002)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    # An open sheet's coherent normal is chosen explicitly, before solidify.
    want=outward(center)
    if sum(f.normal.dot(want)*f.calc_area() for f in bm.faces)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free()
    obj.data.materials.append(r.edge);obj.data.materials.append(r.seal)
    for face in obj.data.polygons:face.use_smooth=True
    # Analytic normals before thickening; stable regular tessellation provides
    # good interpolated shading even near a crease in the manufacturing cage.
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('Full inward wall','SOLIDIFY');mod.thickness=.007;mod.offset=-1;mod.use_even_offset=True;mod.material_offset=2;mod.material_offset_rim=1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=obj.modifiers.new('Machined lip','BEVEL');mod.width=.0007;mod.segments=SHELL_BEVEL_SEGMENTS;mod.limit_method='ANGLE';mod.angle_limit=.65;mod.material=1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False);mod=obj.modifiers.new('Skin','ARMATURE');mod.object=g.rig
    seed=int(hashlib.sha256(name.encode()).hexdigest()[:6],16)/0xffffff
    for face in obj.data.polygons:
        axes=(0,2) if abs(face.normal.y)>.5 else (1,2) if abs(face.normal.x)>.5 else (0,1)
        for li in face.loop_indices:
            p=obj.data.vertices[obj.data.loops[li].vertex_index].co
            obj.data.uv_layers.active.data[li].uv=(p[axes[0]]*3.3+seed,p[axes[1]]*3.3+seed*.7)
    obj['f22_closed_armor']=True;solids.append(obj)
    # Sparse physical contact wear follows panel perimeters, with no noise veil.
    if mat is None:
        rng=random.Random(int(hashlib.sha256(name.encode()).hexdigest()[:8],16))
        verts=[];triangles=[]
        for a,b in zip(inner,inner[1:]+inner[:1]):
            if rng.random()>.65:continue
            for t in [rng.uniform(.15,.45),rng.uniform(.55,.85)]:
                q=a.lerp(b,t);length=(surface(b)-surface(a)).length
                half=min(.055,rng.uniform(.001,.003)/max(.01,length))
                qs=[a.lerp(b,t-half).lerp(center,.003),a.lerp(b,t+half).lerp(center,.003),q.lerp(center,rng.uniform(.008,.022))]
                start=len(verts)
                verts.extend(surface(v)+outward(v).normalized()*.0031 for v in qs);triangles.append((start,start+1,start+2))
        if triangles:g.finish('VECTOR F22 perimeter wear '+name,verts,triangles,r.wear,bone)
    if mat not in [r.glass,r.seal]:
        for k in [0,len(outline)//2]:
            q=outline[k].lerp(center,.15);r.fastener(name+' recessed fastener',surface(q)+outward(q).normalized()*.003,outward(q),bone)
    return obj


def ribbon(name,points,bone,mat=None,width=.003):
    return r.line('F22 '+name,points,bone,mat or r.edge,width)

def interp(stations,t):
    return profile(stations,t,.008)

def octagon(a):
    a=(a+math.pi)%(math.tau)-math.pi
    # manufactured octagon: wide central and oblique planes
    angles=[-math.pi,-2.45,-1.57,-.75,0,.75,1.57,2.45,math.pi]
    return (profile(list(zip(angles,[math.sin(t) for t in angles])),a,.08),profile(list(zip(angles,[math.cos(t) for t in angles])),a,.08))

def materials():
    # All material detail is embedded and exported as images, including cloth normals.
    n=1024;y,x=np.mgrid[0:n,0:n].astype(np.float32)/n;rng=np.random.default_rng(22)
    noise=rng.normal(0,1,(n,n)).astype(np.float32)
    marks=np.zeros((n,n),np.float32)
    for _ in range(180):
        cx,cy=rng.random(2);angle=rng.uniform(-math.pi,math.pi);length=rng.uniform(.002,.045)
        u=(x-cx)*math.cos(angle)+(y-cy)*math.sin(angle);v=-(x-cx)*math.sin(angle)+(y-cy)*math.cos(angle)
        marks=np.maximum(marks,np.maximum(0,1-abs(v)/rng.uniform(.0005,.0011))*(abs(u)<length)*rng.uniform(.03,.15))
    mottled=np.sin(x*45+np.sin(y*26))*np.sin(y*51+x*12)*.003
    col=np.clip(.215+mottled+noise*.006+marks*.48,.1,.44)
    tex=r.image('F22 ceramic coating albedo',col,True)
    nm=r.image('F22 brushed coating normal',r.normal(noise*.00015-marks*.002,18))
    orm=r.image('F22 coating ORM',np.stack([np.ones_like(x),np.clip(.60+noise*.015-marks*.3,.4,.75),np.full_like(x,.3)],axis=2))
    def replace(mat,albedo,normal=None,packed=None):
        ns=mat.node_tree.nodes;ls=mat.node_tree.links;bs=ns.get('Principled BSDF')
        for node in list(ns):
            if node.type not in ['BSDF_PRINCIPLED','OUTPUT_MATERIAL']:ns.remove(node)
        t=ns.new('ShaderNodeTexImage');t.image=albedo;ls.new(t.outputs['Color'],bs.inputs['Base Color'])
        if normal:
            t=ns.new('ShaderNodeTexImage');t.image=normal;nm=ns.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.8;ls.new(t.outputs['Color'],nm.inputs['Color']);ls.new(nm.outputs['Normal'],bs.inputs['Normal'])
        if packed:
            t=ns.new('ShaderNodeTexImage');t.image=packed;sep=ns.new('ShaderNodeSeparateColor');ls.new(t.outputs['Color'],sep.inputs[0]);ls.new(sep.outputs['Green'],bs.inputs['Roughness']);ls.new(sep.outputs['Blue'],bs.inputs['Metallic'])
    replace(r.steel,tex,nm,orm)
    r.edge.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.032,.036,.041,1)
    r.edge.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.55
    # Eight interlacing yarns per tile; UVs below map tiles at 180/m (0.7 mm yarn).
    u=x*8;v=y*8;warp=np.sin((u%1)*math.pi);weft=np.sin((v%1)*math.pi)
    check=(np.floor(u)+np.floor(v))%2
    h=(warp*(1-check)+weft*check)*.7+noise*.025
    c=np.clip(.014+h*.018+noise*.001,.007,.055)
    replace(r.cloth,r.image('F22 aramid weave albedo',c,True),r.image('F22 aramid weave normal',r.normal(h,12)))
    r.cloth.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.92
    r.glass.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.35
    r.wear=bpy.data.materials.new('vector-reference-exposed-substrate');r.wear.use_nodes=True
    bs=next(n for n in r.wear.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.name='Principled BSDF'
    bs.inputs['Base Color'].default_value=(.28,.30,.32,1);bs.inputs['Metallic'].default_value=.55;bs.inputs['Roughness'].default_value=.56


def helmet():
    # Cage stations create a peaked brow, long slanted face and squared jaw.
    # Coordinates are half-width, front and rear depth measured from y=.018.
    rows=[(1.515,.038,.142,.050),(1.545,.060,.155,.078),(1.605,.091,.139,.105),(1.665,.104,.146,.112),(1.705,.094,.112,.100),(1.739,.062,.060,.067),(1.750,.021,.012,.023)]
    def surf(a,z):
        rx=interp([(q,w) for q,w,f,b in rows],z);dep=interp([(q,f if math.cos(a)>=0 else b) for q,w,f,b in rows],z)
        s,c=octagon(a);return Vector((rx*s,.018-dep*c,z))
    def plate(name,coords,mat=None,depth=.008,crown=.001,hardware=True):
        ps=[surf(a,z) for a,z in coords];c=sum(ps,Vector())/len(ps);normal=Vector((c.x,c.y-.018,(c.z-1.625)*.5)).normalized()
        return shellpanel(name,coords,'Head',lambda q:surf(q.x,q.y),lambda q:Vector((math.sin(q.x),-math.cos(q.x),(q.y-1.64)*4)),mat)
    # Inner structural liner, only visible in panel joints.
    verts=[];faces=[];n=96
    for z,w,fr,ba in rows:
        for j in range(n):
            p=surf(j*math.tau/n,z);p.x*=.94;p.y=.018+(p.y-.018)*.94;verts.append(p)
    for row in range(len(rows)-1):
        for j in range(n):a=row*n+j;b=row*n+(j+1)%n;faces.append((a,b,b+n,a+n))
    faces.extend([tuple(reversed(range(n))),tuple(range((len(rows)-1)*n,len(rows)*n))])
    g.finish('VECTOR F22 helmet continuous inner shell',verts,faces,r.seal,'Head')
    # One continuous opaque visor: no artificial white center seam.
    # Three optically flat facets, explicitly sharing the same edge coordinates.
    solid('visor central optic',[(-.047,-.121,1.662),(.047,-.121,1.662),(.026,-.143,1.536),(-.026,-.143,1.536)],'Head',r.glass,(0,-1,-.2),.004,0,.005,False)
    for side in [-1,1]:
        solid('visor peripheral optic '+str(side),[(side*.047,-.121,1.662),(side*.077,-.092,1.660),(side*.082,-.095,1.630),(side*.050,-.126,1.560),(side*.026,-.143,1.536)],'Head',r.glass,(side*.6,-1,0),.004,0,.005,False)
    for sign in [-1,1]:
        # Swept forward brow and crown lanes.
        plate('brow '+str(sign),[(0,1.658),(sign*.76,1.663),(sign*1.05,1.693),(sign*.88,1.711),(sign*.36,1.711),(0,1.698)],crown=.003)
        plate('crown front '+str(sign),[(.03*sign,1.703),(.35*sign,1.716),(.85*sign,1.718),(.91*sign,1.736),(.55*sign,1.746),(.03*sign,1.747)],crown=.002)
        plate('temple bridge '+str(sign),[(.80*sign,1.671),(1.10*sign,1.693),(1.56*sign,1.711),(1.88*sign,1.696),(1.91*sign,1.667),(1.43*sign,1.659),(1.16*sign,1.627),(.88*sign,1.624)],crown=.002)
        plate('parietal '+str(sign),[(.98*sign,1.721),(1.59*sign,1.721),(2.22*sign,1.713),(2.51*sign,1.735),(1.72*sign,1.748),(1.02*sign,1.743)],crown=.001)
        plate('back shell upper '+str(sign),[(1.96*sign,1.700),(2.56*sign,1.724),(3.10*sign,1.721),(3.10*sign,1.668),(2.55*sign,1.655),(2.13*sign,1.667)])
        plate('back shell lower '+str(sign),[(2.18*sign,1.652),(2.60*sign,1.646),(3.10*sign,1.658),(3.10*sign,1.564),(2.58*sign,1.551),(2.02*sign,1.583)])
        plate('cheek structural rail '+str(sign),[(.93*sign,1.633),(1.20*sign,1.631),(1.50*sign,1.607),(1.44*sign,1.579),(.64*sign,1.535),(.31*sign,1.516),(.31*sign,1.532),(.62*sign,1.562)],crown=.001)
        plate('jaw inset '+str(sign),[(1.47*sign,1.581),(1.91*sign,1.590),(1.95*sign,1.555),(.72*sign,1.520),(.52*sign,1.525)],r.edge)
        # Asymmetric polygon communications housing, eight planes and nested socket.
        p=[(sign*.105,y,z) for y,z in [(-.017,1.659),(.013,1.676),(.048,1.660),(.064,1.630),(.045,1.601),(.009,1.591),(-.016,1.612)]]
        solid('comms elastomer seat '+str(sign),p,'Head',r.seal,(sign,0,0),.010,.003,.08,False)
        c=sum((Vector(v) for v in p),Vector())/len(p)
        p=[c+(Vector(v)-c)*.86+Vector((sign*.007,0,0)) for v in p]
        solid('comms segmented cover '+str(sign),p,'Head',None,(sign,0,0),.007,.003,.18)
        p=[c+(Vector(v)-c)*.54+Vector((sign*.011,0,0)) for v in p]
        solid('comms inset '+str(sign),p,'Head',r.edge,(sign,0,0),.004,0,.1,False)
        ribbon('comms indicator '+str(sign),[(sign*.121,.013,1.653),(sign*.121,.022,1.642),(sign*.121,.022,1.619),(sign*.121,.012,1.611)],'Head',r.paint,.0018)
    solid('chin clasp',[(-.018,-.132,1.533),(.018,-.132,1.533),(.025,-.127,1.518),(.015,-.119,1.511),(-.015,-.119,1.511),(-.025,-.127,1.518)],'Head',axis=(0,-1,-.4),crown=.001)
    ribbon('visor left indicator',[(-.043,-.136,1.609),(-.038,-.140,1.584),(-.032,-.145,1.559)],'Head',r.paint,.0017)
    for z in [1.586,1.597,1.608]:ribbon('rear exhaust',[(-.032,.119,z),(.032,.119,z)],'Head',r.seal,.002)


def torso():
    # Low fabric is retained; panels envelope it rather than sit on its front.
    def cp(x,z):
        depth=interp([(1.07,-.115),(1.20,-.122),(1.29,-.137),(1.36,-.151),(1.43,-.131),(1.47,-.083)],z)
        return (x,depth+interp([(0,0),(.09,.008),(.16,.040),(.21,.088)],abs(x)),z)
    def plate(name,xz,bone='Chest',mat=None,depth=.010,crown=.004):
        layer=.004 if name.startswith('oblique') else .001 if name.startswith('costal') else -.003 if name.startswith('pectoral') else 0
        return shellpanel(name,xz,bone,lambda q:Vector(cp(q.x,q.y))+Vector((0,layer,0)),lambda q:Vector((q.x*2,-1,0)),mat)
    # Recessed continuous load-bearing shell behind the individual armor modules.
    outline=[(-.06,1.448),(.06,1.448),(.179,1.423),(.205,1.365),(.167,1.269),(.10,1.235),(-.10,1.235),(-.167,1.269),(-.205,1.365),(-.179,1.423)]
    shellpanel('continuous thoracic chassis',outline,'Chest',lambda q:Vector(cp(q.x,q.y))+Vector((0,.009,0)),lambda q:Vector((q.x*2,-1,0)),r.seal)
    # Central sternum opens at the collar, narrowing below the slot.
    plate('sternum main',[(-.063,1.438),(-.038,1.451),(.038,1.451),(.063,1.438),(.065,1.393),(.043,1.377),(-.043,1.377),(-.065,1.393)])
    for z in [1.418,1.385]:
        shellpanel('sternum recessed port',[(-.037,z+.004),(.037,z+.004),(.033,z-.004),(-.033,z-.004)],'Chest',lambda q:Vector(cp(q.x,q.y))+Vector((0,-.003,0)),lambda q:Vector((0,-1,0)),r.seal)
    for sign in [-1,1]:
        mirror=lambda a:[(sign*x,z) for x,z in a]
        plate('pectoral upper '+str(sign),mirror([(.068,1.431),(.101,1.451),(.172,1.423),(.194,1.394),(.183,1.375),(.129,1.354),(.071,1.371)]),crown=.009)
        plate('pectoral lower '+str(sign),mirror([(.074,1.364),(.132,1.346),(.184,1.369),(.188,1.339),(.156,1.305),(.114,1.296),(.064,1.319)]),crown=.008)
        plate('costal articulated '+str(sign),mirror([(.067,1.310),(.111,1.287),(.155,1.296),(.174,1.267),(.118,1.236),(.077,1.245),(.040,1.279)]),crown=.005)
        face=[(sign*x,z) for x,z in [(.083,1.412),(.113,1.433),(.166,1.411),(.181,1.388),(.167,1.369),(.126,1.357),(.087,1.380)]]
        shellpanel('pectoral machined face '+str(sign),face,'Chest',lambda q:Vector(cp(q.x,q.y))+Vector((0,-.008,0)),lambda q:Vector((q.x*2,-1,0)))
        # Diagonal rebate and indicator in the outer costal fitting.
        ribbon('pectoral diagonal rebate '+str(sign),[Vector(cp(sign*x,z))+Vector((0,-.011,0)) for x,z in [(.094,1.399),(.121,1.387),(.164,1.405)]],'Chest',r.edge,.0009)
        ribbon('costal identity '+str(sign),[Vector(cp(sign*x,z))+Vector((0,-.004,0)) for x,z in [(.150,1.295),(.169,1.307)]],'Chest',r.paint,.0023)
        # Perimeter return forms the vest side/back, with tilted articulated ribs.
        for i in range(3):
            z=1.355-i*.054
            solid('rib wrap '+str(sign)+str(i),[(sign*.187,-.067,z+.017),(sign*.210,-.010,z+.028),(sign*.192,.096,z-.001),(sign*.174,.102,z-.040),(sign*.179,-.041,z-.035)],'Chest',axis=(sign,-.2,0),crown=.003)
        # Shoulder harness is a solid saddle from sternum to back.
        for i,(y,z) in enumerate([(-.083,1.454),(-.033,1.477),(.021,1.474),(.076,1.456)]):
            pts=[(sign*.081,y-.021,z-.010),(sign*.120,y-.017,z-.021),(sign*.155,y+.022,z-.038),(sign*.112,y+.025,z+.001)]
            solid('clavicle saddle '+str(sign)+str(i),[(x,y,z-.008) for x,y,z in pts],'UpperChest',axis=(0,-.35,1),crown=.002,depth=.010)
            if i<2:
                ribbon('harness light '+str(sign)+str(i),[(sign*.098,y-.010,z+.002),(sign*.120,y-.007,z-.009)],'UpperChest',r.paint,.0027)
        # Collar: actual angled flange with inner lining and visible neck aperture.
        for i,(a,b) in enumerate([(0,.7),(.73,1.4),(1.43,2.15),(2.18,3.12)]):
            pts=[]
            for angle,top in [(a,False),(b,False),(b,True),(a,True)]:
                rx=.083 if top else .096;ry=.071 if top else .086
                pts.append((sign*rx*math.sin(angle),.030-ry*math.cos(angle),1.452+(.023+.04*(1-math.cos(angle))/2 if top else 0)))
            solid('collar wall '+str(sign)+str(i),pts,'UpperChest',axis=(sign*math.sin((a+b)/2),-math.cos((a+b)/2),.3),crown=0,depth=.006,hardware=False)
        solid('scapula '+str(sign),[(sign*x,y,z) for x,y,z in [(.018,.138,1.438),(.098,.147,1.444),(.177,.135,1.397),(.196,.106,1.336),(.120,.140,1.285),(.021,.146,1.309)]],'Chest',axis=(0,1,0),crown=.004)
        solid('rear identification '+str(sign),[(sign*x,.154,z) for x,z in [(.040,1.416),(.083,1.420),(.122,1.396),(.101,1.380),(.042,1.394)]],'Chest',r.paint,(0,1,0),.005,.001)
    plate('sternum articulated',[(-.042,1.369),(.042,1.369),(.056,1.346),(.041,1.321),(-.041,1.321),(-.056,1.346)],crown=.004)
    # Narrow abdominal central column with enveloping diagonal side lamellae.
    for i,(top,bottom,w) in enumerate([(1.308,1.246,.040),(1.242,1.181,.034),(1.177,1.112,.029),(1.108,1.065,.025)]):
        plate('abdominal center '+str(i),[(-w,top),(w,top),(w+.009,top-.018),(w*.78,bottom),(-w*.78,bottom),(-w-.009,top-.018)],'Spine',crown=.003)
        for sign in ([] if i==0 else [-1,1]):
            plate('oblique lamella '+str(sign)+str(i),[(sign*x,z) for x,z in [(w+.012,top-.012),(.113-i*.005,top-.003),(.154-i*.006,top+.012),(.166-i*.009,top-.007),(.113-i*.006,bottom+.007),(w+.009,bottom+.006)]],'Spine',crown=.002)
        solid('lumbar '+str(i),[(-.135,.124,top),(.135,.124,top),(.115,.133,bottom),(-.115,.133,bottom)],'Spine',axis=(0,1,0),crown=.002)


def belt():
    g.loft('VECTOR F22 woven utility belt',[(0,.035,z,.180,.122) for z in [1.008,1.061]],r.webbing,'Hips',N=48)
    for i,a in enumerate([-.38,-.78,-1.2,-1.7,.38,.78,1.2,1.7,2.4,-2.4]):
        p=Vector((.179*math.sin(a),.035-.126*math.cos(a),1.035));t=Vector((math.cos(a),math.sin(a),0));n=Vector((math.sin(a),-math.cos(a),0))
        def outline(w,h):return [p+t*x+Vector((0,0,z))+n*.010 for x,z in [(-w,h),(w,h),(w+.004,h-.009),(w,-h),(-w,-h),(-w-.004,h-.009)]]
        solid('utility cartridge '+str(i),outline(.022,.034),'Hips',axis=n,depth=.019,crown=.004)
        face=[p+t*x+Vector((0,0,z))+n*.017 for x,z in [(-.017,.025),(.017,.025),(.015,.006),(-.015,.006)]]
        solid('cartridge locking flap '+str(i),face,'Hips',r.edge,n,.004,.001,.08,False)
        for offset in [-.015,.015]:ribbon('cartridge molding '+str(i)+str(offset),[p+t*offset+Vector((0,0,z))+n*.017 for z in [-.023,0]],'Hips',r.edge,.0009)
    solid('belt shield',[(-.039,-.104,1.062),(.039,-.104,1.062),(.047,-.107,1.044),(.027,-.112,.998),(-.027,-.112,.998),(-.047,-.107,1.044)],'Hips',crown=.004)
    solid('pelvic center',[(-.031,-.098,.995),(.031,-.098,.995),(.048,-.092,.963),(.022,-.078,.913),(-.022,-.078,.913),(-.048,-.092,.963)],'Hips',r.edge,crown=.003)
    for sign in [-1,1]:
        solid('hip wrap '+str(sign),[(sign*x,y,z) for x,y,z in [(.104,-.048,1.004),(.165,-.019,1.011),(.211,.027,.960),(.203,-.027,.905),(.155,-.058,.916),(.121,-.075,.961)]],'Hips',axis=(sign,-.3,0),crown=.003)


def limb_surface(side,part,rxs,rys):
    a,b,f,l=r.limb_frame(side,part)
    def surf(q,off=0):
        angle,t=q;s,c=octagon(angle)
        return a.lerp(b,t)+l*(interp(rxs,t)*s)+f*(interp(rys,t)*c)+(l*math.sin(angle)+f*math.cos(angle))*off
    return surf,a,b,f,l

def limbs():
    for side,sign in [('Left',1),('Right',-1)]:
        # Deltoid shells: front quadrant shield and separate crown/side/back return.
        a,b,f,l=r.limb_frame(side,'UpperArm');out=-sign*l
        def shoulder(angle,t):
            rr=interp([(-.40,.018),(-.29,.052),(-.10,.080),(.16,.085),(.38,.075),(.51,.061)],t)
            s,c=octagon(angle);return a.lerp(b,t)+out*rr*c+f*rr*s
        sections=[('crown bridge',[(-1.3,-.31),(0,-.40),(1.3,-.30),(.96,-.25),(.22,-.36),(-.64,-.25)]),('cap',[(-.64,-.25),(.22,-.36),(.94,-.25),(1.31,-.05),(.96,.13),(.18,.10),(-.69,-.02)]),('front shield',[(.96,-.20),(1.70,-.15),(2.10,.03),(2.02,.28),(1.46,.40),(.94,.24),(.71,.09)]),('outer shield',[(-.64,.02),(.18,.13),(.83,.27),(.54,.42),(-.24,.46),(-.83,.27)]),('back shield',[(-.75,-.20),(-1.52,-.15),(-2.02,.08),(-1.88,.31),(-1.06,.39),(-.85,.19)])]
        for name,coords in sections:
            pts=[shoulder(ang,t) for ang,t in coords];ang=sum(p[0] for p in coords)/len(coords)
            layer=.004 if name=='front shield' else .002 if name=='outer shield' else -.002 if name=='back shield' else 0
            shellpanel(side+' shoulder '+name,coords,side+'UpperArm',lambda q:shoulder(q.x,q.y)+(out*math.cos(q.x)+f*math.sin(q.x))*layer,lambda q:out*math.cos(q.x)+f*math.sin(q.x))
        # Segmented outer pauldron face repeats the silhouette at a smaller inset.
        coords=[(.99,-.08),(1.49,-.10),(1.86,.03),(1.81,.24),(1.46,.31),(1.12,.19)]
        shellpanel(side+' pauldron face insert',coords,side+'UpperArm',lambda q:shoulder(q.x,q.y)+(out*math.cos(q.x)+f*math.sin(q.x))*.009,lambda q:out*math.cos(q.x)+f*math.sin(q.x))
        ribbon('pauldron recessed stripe '+side,[shoulder(ang,t)+(out*math.cos(ang)+f*math.sin(ang))*.012 for ang,t in [(1.10,-.02),(1.45,.025),(1.72,.13)]],side+'UpperArm',r.edge,.0011)
        # Profile-specific wrap shells; quads are compact at the knee, tapering at wrist.
        for part,rxs,rys,defs in [
            ('UpperArm',[(.4,.064),(1,.054)],[(.4,.065),(1,.053)],[('bicep',[(sign*.0,.48),(sign*1.1,.47),(sign*1.45,.61),(sign*1.18,.87),(sign*.44,.89),(sign*.04,.74)],None),('triceps',[(2.25,.42),(3.7,.42),(3.85,.74),(3.1,.88),(2.2,.78)],None)]),
            ('LowerArm',[(0,.062),(.3,.065),(.85,.045),(1,.039)],[(0,.066),(.3,.070),(.85,.050),(1,.042)],[('forearm main',[(-.64,.12),(-.24,.03),(.38,.05),(.69,.19),(.51,.79),(.23,.92),(-.47,.84),(-.72,.66)],None),('forearm lateral',[(.75,.16),(1.48,.18),(1.85,.34),(1.63,.80),(.65,.91),(.60,.67)],None),('forearm rear',[(2.05,.15),(3.62,.10),(4.08,.26),(3.88,.75),(3.22,.90),(2.12,.79)],None)]),
            ('UpperLeg',[(-.15,.105),(.15,.112),(.55,.101),(.9,.071)], [(-.15,.108),(.15,.116),(.55,.104),(.9,.077)],[('thigh crown',[(-.64,-.12),(.10,-.22),(.84,-.12),(1.24,.07),(1.10,.30),(.72,.18),(.26,.08),(-.04,.19),(-.30,.34),(-.77,.20),(-.91,.06)],None),('thigh inner',[(-.83,.13),(-.39,.20),(-.06,.33),(-.19,.70),(-.50,.82),(-.86,.63)],None),('thigh primary',[(-.12,.27),(.36,.19),(.80,.30),(.88,.61),(.47,.84),(.16,.79),(-.12,.66)],None),('thigh outer',[(.98,.25),(1.64,.17),(1.88,.30),(1.69,.70),(1.02,.87),(.96,.59)],None),('thigh rear',[(2.0,.12),(3.45,.08),(3.95,.25),(3.69,.63),(2.8,.78),(2.07,.63)],r.edge)]),
            ('LowerLeg',[(-.1,.068),(.15,.073),(.45,.072),(.90,.045),(1,.042)],[(-.1,.077),(.15,.079),(.45,.087),(.90,.064),(1,.056)],[('kneecap',[(-.82,-.07),(-.24,-.13),(.37,-.11),(.88,.02),(.65,.17),(.05,.23),(-.65,.15)],None),('tibia main',[(-.71,.27),(-.23,.31),(.18,.26),(.62,.38),(.31,.78),(.07,.91),(-.42,.84),(-.69,.58)],None),('shin outer',[(.77,.24),(1.48,.33),(1.68,.48),(1.37,.85),(.52,.96),(.40,.78),(.71,.56)],None),('calf',[(2.0,.24),(3.25,.17),(4.03,.30),(3.92,.55),(3.48,.89),(2.36,.87),(2.10,.60)],None)])]:
            surf,a,b,f,l=limb_surface(side,part,rxs,rys)
            for name,coords,mat in defs:
                # Mirror surface-angle pattern to the anatomical outer side.
                coords=[(-sign*angle,t) for angle,t in coords] if part!='UpperArm' else coords
                pts=[surf(q) for q in coords];mean=sum(q[0] for q in coords)/len(coords);axis=l*math.sin(mean)+f*math.cos(mean)
                layer=.010 if name=='thigh crown' else 0
                shellpanel(side+' '+name,coords,side+part,lambda q:surf(q,layer),lambda q:l*math.sin(q.x)+f*math.cos(q.x),mat)
            if part=='UpperLeg':
                coords=[(-sign*a,t) for a,t in [(.06,.32),(.37,.26),(.67,.35),(.64,.60),(.40,.75),(.16,.70),(.06,.54)]]
                shellpanel(side+' quadriceps inset',coords,side+part,lambda q:surf(q,.005),lambda q:l*math.sin(q.x)+f*math.cos(q.x))
                ribbon('thigh locking seam '+side,[surf((-sign*a,t),.008) for a,t in [(.18,.40),(.45,.38),(.52,.55)]],side+part,r.edge,.0012)
            if part=='LowerLeg':
                coords=[(-sign*a,t) for a,t in [(-.20,.38),(.10,.34),(.30,.43),(.12,.75),(-.09,.83),(-.25,.66)]]
                shellpanel(side+' tibial crest',coords,side+part,lambda q:surf(q,.005),lambda q:l*math.sin(q.x)+f*math.cos(q.x))
                # Recessed knee face with a distinct articulated border.
                coords=[(-sign*a,t) for a,t in [(-.56,-.04),(-.14,-.08),(.34,-.06),(.62,.025),(.43,.13),(.02,.17),(-.48,.11)]]
                shellpanel(side+' knee face insert',coords,side+part,lambda q:surf(q,.005),lambda q:l*math.sin(q.x)+f*math.cos(q.x))
            if part=='LowerArm':
                for aoff,mat,width in [(0,r.edge,.008),(.0,r.paint,.0025)]:
                    ribbon('gauntlet channel '+side+str(width),[surf((aoff,t),.006 if mat==r.edge else .010) for t in [.23,.29,.67,.72]],side+part,mat,width)
            if part=='LowerLeg':
                ribbon('shin identification '+side,[surf((-sign*1.06,t),.008) for t in [.48,.68]],side+part,r.paint,.0027)
            if part=='UpperLeg':
                coords=[(-sign*a,t) for a,t in [(.99,.26),(1.34,.24),(1.45,.37),(1.31,.51),(1.0,.54)]]
                shellpanel(side+' lateral team insert',coords,side+part,lambda q:surf(q,.004),lambda q:l*math.sin(q.x)+f*math.cos(q.x),r.paint)
        # Tailored transverse pleats and stitched seams at knees/elbows.
        for part,start,end,rx,ry,count in [('UpperArm',.88,1.04,.051,.051,7),('UpperLeg',.83,1.01,.055,.058,8)]:
            a,b,f,l=r.limb_frame(side,part)
            for j in range(count):
                t=start+(end-start)*j/(count-1)
                pts=[a.lerp(b,t)+l*(rx*math.sin(q))+f*(ry*math.cos(q)) for q in np.linspace(-1.6,1.6,25)]
                ribbon('joint sewn pleat '+side+part+str(j),pts,side+part,r.cloth,.0025)


def boots():
    for side,sign in [('Left',1),('Right',-1)]:
        x=g.rig.data.bones[side+'Foot'].head_local.x
        # A sewn boot upper fills the instep/heel; hard plates never bridge empty air.
        g.loft('VECTOR F22 sewn boot upper '+side,[(x,-.031,.041,.055,.121),(x,-.025,.067,.054,.117),(x,-.007,.096,.045,.072),(x,.030,.131,.040,.046),(x,.035,.165,.043,.050)],r.cloth,side+'Foot',N=48)
        # Rounded trapezoidal outsole carries individual tread blocks and sidewall.
        outline=[(-.058,-.169),(.041,-.177),(.065,-.151),(.058,.082),(.036,.102),(-.048,.102),(-.064,.064),(-.067,-.139)]
        solid(side+' rubber outsole',[(x+u,y,.032) for u,y in outline],side+'Foot',r.seal,(0,0,1),.025,0,.06,False)
        solid(side+' boot welt',[(x+u*.98,y,.050) for u,y in outline],side+'Foot',r.edge,(0,0,1),.018,.003,.07,False)
        for j,y in enumerate([-.15,-.105,-.06,-.015,.035,.080]):
            for s in [-1,1]:g.box('VECTOR F22 tread lug '+side+str(j)+str(s),(x+s*.054,y,.020),(.023,.028,.022),r.seal,side+'Foot',.003)
        # Toe cap is a compound sloping box; successive instep shells overlap.
        solid(side+' armored toe',[(x-.054,-.152,.049),(x-.045,-.171,.054),(x+.035,-.171,.054),(x+.057,-.145,.057),(x+.049,-.111,.079),(x-.049,-.111,.078)],side+'Foot',axis=(0,-.3,1),depth=.013,crown=.004)
        for j,(y,z) in enumerate([(-.086,.089),(-.045,.110),(-.004,.131)]):
            solid(side+' instep segment '+str(j),[(x-.050,y-.023,z-.009),(x+.046,y-.023,z-.009),(x+.048,y+.016,z+.011),(x+.030,y+.027,z+.017),(x-.039,y+.024,z+.012),(x-.054,y+.011,z)],side+'Foot',axis=(0,-.45,1),depth=.010,crown=.002)
        for s in [-1,1]:
            solid(side+' heel counter '+str(s),[(x+s*.056,-.008,.070),(x+s*.054,.061,.063),(x+s*.047,.088,.095),(x+s*.049,.067,.155),(x+s*.056,.014,.171),(x+s*.063,-.018,.125)],side+'Foot',axis=(s,0,0),crown=.003)


def main():
    baseline=ROOT/'scripts/vector_fighter/f21-upper-study.blend'
    assert hashlib.sha256(baseline.read_bytes()).hexdigest()=='e9f59ef3fcc1e9cf4449edc42e827491e9788176122e19006288f40067049ea1','Pinned F21 source'
    bpy.ops.wm.open_mainfile(filepath=str(baseline))
    g.rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');g.rig.data.pose_position='REST';g.parts=[]
    for key,name in [('steel','vector-reference-coated-steel'),('paint','vector-fighter-identity-reference'),('edge','vector-reference-edge-metal'),('cloth','vector-reference-woven-fabric'),('seal','vector-reference-flexible-seals'),('webbing','vector-reference-webbing'),('glass','vector-reference-opaque-visor')]:setattr(r,key,bpy.data.materials[name])
    keep=['fabric','lower garment','gloves','textile ribcage','closed shoulder yoke','woven axillary gusset','neck textile']
    for obj in list(bpy.context.scene.objects):
        if obj.type=='MESH' and obj.name!='weapon:joined' and not any(k in obj.name for k in keep):bpy.data.objects.remove(obj,do_unlink=True)
    # Retained fabric is fitted inside the new solid cage; weighting is unchanged.
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or 'gloves' in obj.name or obj.name=='weapon:joined':continue
        for vertex in obj.data.vertices:
            p=obj.matrix_world@vertex.co
            if any(k in obj.name for k in ['UpperArm fabric','axillary gusset']):
                side='Left' if p.x>0 else 'Right';part='UpperArm'
                a,b,f,l=r.limb_frame(side,part);d=b-a;t=(p-a).dot(d)/d.length_squared
                if t>1:a,b,f,l=r.limb_frame(side,'LowerArm');d=b-a;t=(p-a).dot(d)/d.length_squared
                anchor=a+d*t;blend=.90 if t<.83 else 1
                p=anchor+(p-anchor)*blend
            elif 'lower garment' in obj.name and p.z<.94:
                side='Left' if p.x>0 else 'Right';part='UpperLeg' if p.z>.505 else 'LowerLeg'
                a,b,f,l=r.limb_frame(side,part);d=b-a;t=(p-a).dot(d)/d.length_squared;anchor=a+d*t
                blend=max(0,min(1,(p.z-.82)/.12));blend=blend*blend*(3-2*blend)
                p=anchor+(p-anchor)*(.84+.16*blend)
            elif 'neck textile' in obj.name:
                a=math.atan2(p.x,p.y-.028);radius=math.hypot(p.x,p.y-.028)
                fold=.0018*math.sin(p.z*250+a*3)*math.sin((p.z-1.43)/.125*math.pi)**2
                p.x*=1+fold/max(.01,radius);p.y=.028+(p.y-.028)*(1+fold/max(.01,radius))
            elif any(k in obj.name for k in ['torso fabric','textile ribcage']):
                p.x*=.94;p.y=.025+(p.y-.025)*.87
            vertex.co=obj.matrix_world.inverted()@p
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and 'lower garment' in obj.name:
            bm=bmesh.new();bm.from_mesh(obj.data)
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=obj.matrix_world.inverted()@Vector((0,0,.145)),plane_no=Vector((0,0,1)),clear_inner=True)
            bm.to_mesh(obj.data);bm.free()
    materials();helmet();torso();belt();limbs();boots()
    # Physical weave scale, same in all existing fabric components.
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or not any(k in obj.name for k in keep[:-1]+['neck textile']):continue
        if 'gloves' in obj.name:continue
        for face in obj.data.polygons:
            axes=(0,2) if abs(face.normal.y)>.5 else (1,2) if abs(face.normal.x)>.5 else (0,1)
            for li in face.loop_indices:
                p=obj.matrix_world@obj.data.vertices[obj.data.loops[li].vertex_index].co
                obj.data.uv_layers.active.data[li].uv=(p[axes[0]]*65,p[axes[1]]*65)
    rows=[]
    for obj in solids:
        bm=bmesh.new();bm.from_mesh(obj.data);bad=sum(not e.is_manifold for e in bm.edges);bm.free();rows.append({'name':obj.name,'nonmanifold_edges':bad});assert bad==0,obj.name
    (OUT/'construction.json').write_text(json.dumps({'closed_solids':len(rows),'objects':rows},indent=2)+'\n')
    g.rig.data.pose_position='POSE';bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_OUTPUT))
    print('F22_COMPLETE',len(solids))
if __name__=='__main__':main()
