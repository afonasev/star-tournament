"""F02: section-built helmet; editable manufactured solids, dedicated UV/PBR.

Run in Blender. Source is immutable F23 color-3; only rigid head meshes change.
Dimensions below describe the approved asset, never gameplay/camera tuning.
"""
import bpy
import bmesh
import math
import sys
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).parent))
import build as g
import author_reference as r
from military_f23_helmet import fingerprint

OUT = ROOT / '.local/f23-reconstruction'
TARGET = ROOT / 'scripts/vector_fighter/f23-reconstructed.blend'
SOURCE = ROOT / 'scripts/vector_fighter/f23-military-color.blend'
parts = []


def activate(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def mesh(name, vertices, faces, material, thickness=0, bevel=.0007, smooth=False, subdiv=0):
    o = g.finish('VECTOR F23R ' + name, vertices, faces, material, 'Head')
    o.modifiers.remove(o.modifiers['Skin'])
    for p in o.data.polygons:
        p.use_smooth = smooth
    activate(o)
    if subdiv:
        mod=o.modifiers.new('Crown surface interpolation','SUBSURF')
        mod.levels=subdiv
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if thickness:
        mod = o.modifiers.new('Wall thickness', 'SOLIDIFY')
        mod.thickness = thickness
        mod.offset = -1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if bevel:
        mod = o.modifiers.new('Manufactured edge radius', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = 'ANGLE'
        mod.angle_limit = .45
        mod.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod = o.modifiers.new('Area weighted corner normals', 'WEIGHTED_NORMAL')
        mod.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    mod = o.modifiers.new('Skin', 'ARMATURE')
    mod.object = g.rig
    o['f23_reconstructed'] = True
    parts.append(o)
    return o


def loft(name, rows, material, thickness=.006, smooth=False, bevel=.0007, subdiv=0):
    n = len(rows[0])
    vertices = [p for row in rows for p in row]
    faces = [(j*n+i, j*n+i+1, (j+1)*n+i+1, (j+1)*n+i)
             for j in range(len(rows)-1) for i in range(n-1)]
    return mesh(name, vertices, faces, material, thickness, bevel, smooth, subdiv)


def slab(name, outline, axis, material, depth=.008, chamfer=.003):
    """Keep authored noncoplanar outline; bevel face is an explicit second ring."""
    pts = [Vector(v) for v in outline]
    center = sum(pts, Vector()) / len(pts)
    normal = Vector(axis).normalized()
    inner = [p.lerp(center, .10) + normal*chamfer for p in pts]
    n = len(pts)
    vertices = pts + inner + [p-normal*depth for p in pts]
    faces = [tuple(range(n, 2*n)), tuple(reversed(range(2*n, 3*n)))]
    for i in range(n):
        j = (i+1) % n
        faces += [(i,j,n+j,n+i), (i,2*n+i,2*n+j,j)]
    return mesh(name, vertices, faces, material)


def section(a, width, front, rear, height, center=.014):
    sn, cs = math.sin(a), math.cos(a)
    return (width*math.copysign(abs(sn)**.9, sn),
            center-(front if cs >= 0 else rear)*cs, height)


def shell():
    # Crown cross sections are fitted independently: wide brow, swept apex,
    # flattened rear shoulder. No common sphere or scaled half-ellipsoid.
    rings = [(.114,.140,.119,1.698,.014),
             (.109,.130,.119,1.720,.020),
             (.092,.104,.104,1.746,.030),
             (.059,.067,.069,1.764,.035),
             (.019,.023,.024,1.772,.038),
             (.0006,.0007,.0007,1.773,.038)]
    for name, amin, amax in [('fore crown',-1.13,1.13),
                            ('right rear crown',1.15,math.pi-.012),
                            ('left rear crown',-math.pi+.012,-1.15)]:
        rows = [[section(amin+(amax-amin)*i/16,*ring) for i in range(17)] for ring in rings]
        loft(name,rows,paint,.006,True,subdiv=1)
    for s in (-1,1):
        a=[s*(1.14+(math.pi-1.14)*i/32) for i in range(33)]
        rows=[]
        for z,w,f,rear,cy in [(1.697,.113,.137,.120,.014),
                              (1.653,.117,.133,.120,.014),
                              (1.597,.108,.122,.105,.014),
                              (1.559,.092,.106,.086,.014)]:
            # The upper seam must meet the crown's actual lower section.
            # Raising its rear caused two coincident shells in native side views.
            rise=0 if z>1.69 else .019
            rows.append([section(v,w,f,rear,z+rise*max(0,-math.cos(v)),cy) for v in a])
        loft('rear shell '+str(s), rows,paint,.007,True)
        # Side lower sill ties jaw to the rear shell, under the ear housing.
        slab('jaw sill '+str(s),[(s*.109,-.025,1.649),(s*.118,.034,1.643),
             (s*.106,.063,1.588),(s*.089,.020,1.560),(s*.071,-.034,1.551)],
             (s,0,0),paint,.007,.002)


def visor_and_brow():
    angles=[-1.30+2.60*i/48 for i in range(49)]
    rows=[]
    for j in range(7):
        t=j/6
        row=[]
        for a in angles:
            lower=1.610+.024*(abs(a)/1.30)**1.8
            row.append(section(a,.109,.146,.12,lower+(1.686-lower)*t,.011))
        rows.append(row)
    loft('sealed panoramic visor',rows,glass,.004,True,.0004)
    # Five longitudinal bends and bevelled returns give the brow real section.
    aa=[-1.29,-1.02,-.60,0,.60,1.02,1.29]
    specs=[(1.684,.113,.150),(1.688,.119,.158),
           (1.715,.117,.154),(1.719,.113,.145)]
    rows=[[section(a,w,f,.12,z,.011) for a in aa] for z,w,f in specs]
    loft('heavy brow with lower return', rows,steel,.008,False,.0012)
    for s in (-1,1):
        # Rearward anchor lug has a visible recessed channel.
        slab('brow anchor '+str(s),[(s*.105,-.069,1.700),(s*.119,-.014,1.706),
             (s*.120,-.011,1.691),(s*.109,-.066,1.686)],(s,-.15,0),steel,.007,.001)
        r.line('F23R anchor slot',[(s*.119,-.052,1.697),(s*.125,-.025,1.700)],'Head',seal,.002)


def jaw():
    # One continuous, broad U-shaped chin; the lower return recedes to the neck.
    # Face columns follow the cheek flow and leave four actual vent apertures.
    xs=[-.071,-.054,-.022,0,.022,.054,.071]
    zs=[1.543,1.554,1.565,1.574,1.585,1.600,1.613]
    def chin(x,z):
        low=max(0,(1.558-z)/.015)
        upper=max(0,(z-1.601)/.012)
        return (x*(1-.10*low),-.152+.40*abs(x)+.006*low+.009*upper,z)
    vertices=[chin(x,z) for z in zs for x in xs]
    faces=[]
    for j in range(len(zs)-1):
        for i in range(len(xs)-1):
            if j in (1,3) and i in (1,4):continue
            k=j*len(xs)+i
            faces.append((k,k+1,k+1+len(xs),k+len(xs)))
    mesh('continuous vented chin',vertices,faces,steel,.010,.0013)
    # Gaskets and crossed mesh are physically recessed behind each opening.
    for s in (-1,1):
        for lo,hi in [(1.554,1.565),(1.574,1.585)]:
            pts=[Vector(chin(s*x,z))+Vector((0,.004,0)) for x,z in [(.021,lo-.001),(.055,lo-.001),(.055,hi+.001),(.021,hi+.001)]]
            mesh('vent well',pts,[(0,1,2,3)],seal,.002,0)
            for i in range(13):
                x=.022+i*.0026
                for direction in (-1,1):
                    x1=max(.022,min(.054,x+direction*.004))
                    points=[Vector(chin(s*x,lo+.001))+Vector((0,.0015,0)),Vector(chin(s*x1,hi-.001))+Vector((0,.0015,0))]
                    r.line('F23R vent woven grille',points,'Head',edge,.00036)
        # Nonplanar transition occupies the corner, not a flat floating shield.
        outline=[(s*.066,-.116,1.611),(s*.099,-.071,1.637),
                 (s*.117,-.025,1.627),(s*.115,.010,1.598),
                 (s*.100,-.004,1.570),(s*.068,-.080,1.544),
                 (s*.061,-.125,1.548)]
        slab('formed cheek '+str(s),outline,(s*.65,-.65,.10),steel,.008,.003)
        # Ear case overlaps the shell and cheek seam, as in the reference.
        outline=[(s*.119,-.021,1.672),(s*.123,.019,1.686),
                 (s*.122,.063,1.663),(s*.116,.074,1.623),
                 (s*.117,.037,1.600),(s*.122,-.003,1.615)]
        slab('ear gasket '+str(s),outline,(s,0,0),seal,.006,.001)
        pts=[(x+s*.005,y,z) for x,y,z in outline]
        slab('ear armored housing '+str(s),pts,(s,0,0),steel,.007,.003)
        pts=[(s*.135,-.002,1.650),(s*.135,.018,1.665),(s*.135,.038,1.652),
             (s*.135,.037,1.625),(s*.135,.018,1.615),(s*.135,-.003,1.628)]
        slab('ear recessed insert '+str(s),pts,(s,0,0),seal,.003,.0006)
        r.line('F23R ear color piping',[(s*.138,.018,1.664),(s*.138,.037,1.650),(s*.138,.036,1.626),(s*.138,.019,1.616)],'Head',paint,.0015)
        for y,z in [(-.009,1.658),(.055,1.645),(.011,1.614)]:
            r.fastener('F23R ear fixing',(s*.134,y,z),(s,0,0),'Head')
        r.fastener('F23R shell anchor',(s*.096,-.061,1.723),(s*.5,-.7,.3),'Head')


def main():
    global paint,steel,seal,edge,glass
    OUT.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    g.rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    g.rig.data.pose_position='REST';g.parts=[]
    paint=bpy.data.materials['vector-fighter-identity-reference']
    steel=bpy.data.materials['vector-reference-coated-steel']
    seal=bpy.data.materials['vector-reference-flexible-seals']
    edge=bpy.data.materials['vector-reference-edge-metal']
    glass=bpy.data.materials['vector-reference-opaque-visor']
    r.paint,r.steel,r.seal,r.edge,r.glass=paint,steel,seal,edge,glass
    kept={}
    for o in list(bpy.context.scene.objects):
        if o.type!='MESH':continue
        if len(o.vertex_groups)==1 and o.vertex_groups[0].name=='Head':
            bpy.data.objects.remove(o,do_unlink=True)
        else:kept[o.name]=fingerprint(o)
    g.ellipsoid('VECTOR F23R internal liner',(0,.018,1.635),(.092,.106,.113),seal,'Head')
    shell();visor_and_brow();jaw()
    assert all(fingerprint(bpy.data.objects[n])==v for n,v in kept.items())
    bpy.context.preferences.filepaths.save_version=0
    g.rig.data.pose_position='POSE'
    bpy.ops.wm.save_as_mainfile(filepath=str(TARGET),compress=True)
    (OUT/'authoring.json').write_text(json.dumps({'preserved_objects':len(kept),'nonhead_exact':True,'source':str(SOURCE),'target':str(TARGET),'method':'independent crown sections + manufactured brow and continuous vented chin, formed cheeks'},indent=2))


if __name__=='__main__':main()
