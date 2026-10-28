"""ASSAULT V2: shaped armor over a stronger anatomical foundation, on the F17 rig.

Offline dimensions in metres are authored asset geometry, not gameplay controls.
The F17 source, first-person hands, weapon, skeleton and animation data are retained.
"""
import sys, math, random
from pathlib import Path
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build as g


def foundation():
    for obj in list(bpy.context.scene.objects):
        if obj.type != 'MESH' or obj.name == 'weapon:joined' or 'gloves' in obj.name:
            continue
        name = obj.name
        # Rebuild the major shells instead of stretching the old shallow patches.
        if any(k in name for k in ('pectoral shell', 'costal shell', 'scapular shell',
                                  'waist yoke', 'clavicle yoke', 'UpperLeg contoured',
                                  'LowerLeg contoured', 'knee shield', 'articulated instep')):
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        for v in obj.data.vertices:
            p = obj.matrix_world @ v.co
            if 'torso fabric' in name:
                # Fill the core; shoulders remain anatomically continuous with it.
                p.x *= 1.10
                p.y = .025 + (p.y-.025)*1.14
            elif 'trousers' in name:
                if p.z > .19:
                    sign = 1 if p.x > 0 else -1
                    center = sign*(.15 if p.z < .55 else .137)
                    weight = min(1, max(0, (1.02-p.z)/.15))
                    p.x = center+(p.x-center)*(1+.23*weight)
                    p.y = .035+(p.y-.035)*(1+.20*weight)
            elif 'UpperArm' in name:
                w = min(1, max(0, (p.z-1.2)/.20))
                p.x *= 1+.10*w
                p.y = .05+(p.y-.05)*(1+.18*w)
            elif 'LowerArm' in name:
                w=min(1,max(0,(p.z-1.06)/.13))
                p.y=.05+(p.y-.05)*(1+.2*w)
            elif 'visor' in name:
                # Compact visor, stronger jaw coverage; keep the closed helmet.
                p.z = 1.60+(p.z-1.60)*.79
            v.co = obj.matrix_world.inverted() @ p
        obj.data.update()


def tailored_legs():
    """Replace bulky source trouser folds by anatomical lofts; retain original boots."""
    cloth=bpy.data.materials['vector-fighter-sewn-pants']
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH' or 'trousers' not in obj.name:continue
        bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.delete(bm,geom=[v for v in bm.verts if (obj.matrix_world@v.co).z>.19],context='VERTS')
        bm.to_mesh(obj.data);bm.free();obj.data.update()
    # Continuous pelvic girdle overlaps the soft thigh tops under articulated armor.
    rings=[(0,.035,z,rx,ry) for z,rx,ry in [(.82,.207,.105),(.91,.218,.123),(.98,.190,.123),(1.04,.174,.108)]]
    g.loft('VECTOR V2 pelvis fabric',rings,cloth,'Hips',N=48)
    for side in ['Left','Right']:
        for region,stations in [
          ('UpperLeg',[(-.08,.089,.103),(0,.099,.115),(.22,.106,.119),(.48,.097,.108),(.73,.080,.091),(1.04,.059,.065)]),
          ('LowerLeg',[(-.06,.059,.065),(.13,.066,.078),(.32,.071,.085),(.55,.060,.071),(.78,.046,.055),(1.01,.037,.045)])]:
            bone=g.rig.data.bones[side+region];a=bone.head_local;b=bone.tail_local
            axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized()
            verts=[];faces=[];weights=[];n=48
            for t,rx,ry in stations:
                for i in range(n):
                    angle=math.tau*i/n
                    # Small longitudinal tailoring relief rather than noisy cloth inflation.
                    seam=.001*math.cos(angle*8)
                    verts.append(a.lerp(b,t)+lateral*((rx+seam)*math.sin(angle))+front*((ry+seam)*math.cos(angle)))
                    blend=max(0,min(1,(t-.78)/.25))
                    weights.append({side+region:1-blend,side+('LowerLeg' if region=='UpperLeg' else 'Foot'):blend})
            for row in range(len(stations)-1):
                for i in range(n):
                    j=row*n+i;k=row*n+(i+1)%n;faces.append((j,k,k+n,j+n))
            faces.extend([tuple(reversed(range(n))),tuple(range((len(stations)-1)*n,len(stations)*n))])
            g.finish('VECTOR V2 '+side+region+' fabric',verts,faces,cloth,side+region,weights=weights)
    # Fuse the authored soft volumes into one manifold garment. This replaces
    # disconnected tube intersections at the groin/knees/ankles with real cloth.
    pieces=[o for o in bpy.context.scene.objects if o.type=='MESH' and
            ('trousers' in o.name or ('VECTOR V2' in o.name and 'fabric' in o.name))]
    samples=[];weights=[]
    for o in pieces:
        names={v.index:v.name for v in o.vertex_groups}
        for v in o.data.vertices:
            samples.append(o.matrix_world@v.co);weights.append({names[w.group]:w.weight for w in v.groups})
    tree=KDTree(len(samples))
    for i,p in enumerate(samples):tree.insert(p,i)
    tree.balance()
    bpy.ops.object.select_all(action='DESELECT')
    for o in pieces:o.select_set(True)
    bpy.context.view_layer.objects.active=pieces[0];bpy.ops.object.join();o=bpy.context.object
    o.name='VECTOR V2 continuous lower garment'
    for mod in list(o.modifiers):o.modifiers.remove(mod)
    remesh=o.modifiers.new('Continuous anatomical garment','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.008;remesh.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth=o.modifiers.new('Tailored joint continuity','SMOOTH');smooth.factor=.7;smooth.iterations=3;bpy.ops.object.modifier_apply(modifier=smooth.name)
    reduce=o.modifiers.new('Authored garment topology','DECIMATE');reduce.ratio=.30;bpy.ops.object.modifier_apply(modifier=reduce.name)
    o.vertex_groups.clear();groups={n:o.vertex_groups.new(name=n) for n in sorted({n for w in weights for n in w})}
    for v in o.data.vertices:
        nearby=tree.find_n(o.matrix_world@v.co,4);acc={};total=0
        for point,index,distance in nearby:
            influence=1/max(.001,distance)**2;total+=influence
            for name,weight in weights[index].items():acc[name]=acc.get(name,0)+weight*influence
        for name,weight in acc.items():groups[name].add([v.index],weight/total,'REPLACE')
    uv=o.data.uv_layers.new(name='UVMap')
    for polygon in o.data.polygons:
        for li in polygon.loop_indices:
            p=o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(p.x*8,p.z*8)
    skin=o.modifiers.new('Skin','ARMATURE');skin.object=g.rig


def patch(name, outline, mapping, bone, material, crown=.008):
    """Closed crowned shell with an actual bevel, dark gasket and metal edge land."""
    pts = [Vector(p) for p in outline]
    center = sum(pts, Vector((0, 0)))/len(pts)
    # Edge subdivisions keep silhouettes intentional rather than inflated.
    perimeter = [a.lerp(pts[(i+1)%len(pts)], j/2) for i,a in enumerate(pts) for j in range(2)]
    rings = [(.10, crown), (.45, crown*.95), (.86, crown*.6), (.94, .002),
             (.975, 0), (1, -.003), (1, -.010), (.975, -.013)]
    verts, faces, coordinates = [], [], []
    n = len(perimeter)
    for radius, depth in rings:
        for p in perimeter:
            q = center+(p-center)*radius
            coordinates.append(q)
            base=Vector(mapping(q.x,q.y,0)); point=Vector(mapping(q.x,q.y,depth))
            axis=g.rig.data.bones[bone];delta=axis.tail_local-axis.head_local
            t=max(0,min(1,(base-axis.head_local).dot(delta)/delta.length_squared))
            anchor=axis.head_local+delta*t;ray=base-anchor
            hit,normal,index,distance=clearance.ray_cast(anchor,ray.normalized(),.6)
            # A ray may meet the opposite leg after leaving the owning limb.
            # Only local clearance is relevant; never pull a plate across the crotch.
            if hit is not None and ray.length-.03<distance<ray.length+.025 and distance+.014>ray.length:
                point+=ray.normalized()*(distance+.014-ray.length)
            verts.append(point)
    faces.append(tuple(reversed(range(n))))
    for row in range(len(rings)-1):
        for i in range(n):
            a=row*n+i; b=row*n+(i+1)%n
            faces.append((a,b,b+n,a+n))
    faces.append(tuple(range((len(rings)-1)*n,len(rings)*n)))
    obj = g.finish('VECTOR V2 '+name, verts, faces, material, bone)
    low=Vector((min(p.x for p in pts),min(p.y for p in pts)));high=Vector((max(p.x for p in pts),max(p.y for p in pts)))
    for polygon in obj.data.polygons:
        for li in polygon.loop_indices:
            q=coordinates[obj.data.loops[li].vertex_index]
            obj.data.uv_layers.active.data[li].uv=((q.x-low.x)/(high.x-low.x),(q.y-low.y)/(high.y-low.y))
    obj.data.materials.append(g.metal); obj.data.materials.append(g.trim)
    for face in obj.data.polygons:
        row=(face.index-1)//n
        if row == 4: face.material_index=1
        elif row >= 5: face.material_index=2
    # Broad primary faces carry their own normals; a manufacturing bevel must
    # not pull the entire panel's normals towards its rear wall.
    for face in obj.data.polygons:face.use_smooth=True
    bpy.context.view_layer.objects.active=obj
    normal=obj.modifiers.new('Area weighted shell normals','WEIGHTED_NORMAL')
    normal.keep_sharp=True;normal.weight=50
    bpy.ops.object.modifier_apply(modifier=normal.name)
    # Normal generation is intrinsic to the curved primary surface; no extra plates.
    return obj


def torso():
    front=lambda x,z,d:(x,-.184+1.4*x*x-d,z)
    outline=[(-.080,1.455),(.080,1.455),(.176,1.425),(.243,1.374),
             (.222,1.300),(.125,1.264),(.082,1.239),(-.082,1.239),
             (-.125,1.264),(-.222,1.300),(-.243,1.374),(-.176,1.425)]
    patch('pectoral shell',outline,front,'Chest',g.paint,.014)
    # Overlapping abdominal lames follow distinct torso joints and leave flexion room.
    for i,(top,bottom,width) in enumerate([(1.255,1.185,.171),(1.188,1.116,.158),(1.119,1.058,.151)]):
        patch('abdominal lame '+str(i),[(-width,top),(-.07,top-.010),(.07,top-.010),(width,top),
              (width*.87,bottom+.012),(.055,bottom),(-.055,bottom),(-width*.87,bottom+.012)],
              lambda x,z,d:(x,-.147+1.3*x*x-d,z),'Spine',g.armor,.006)
    for sign in [-1,1]:
        patch('scapular shell',[(sign*x,z) for x,z in [(.012,1.453),(.119,1.445),(.225,1.388),
              (.211,1.287),(.122,1.243),(.022,1.283)]],
              lambda x,z,d:(x,.192-.75*x*x+d,z),'Chest',g.armor,.009)
        patch('rear identity',[(sign*x,z) for x,z in [(.030,1.424),(.111,1.424),(.171,1.390),(.163,1.371),(.103,1.399),(.030,1.399)]],
              lambda x,z,d:(x,.204-.75*x*x+d,z),'Chest',g.paint,.002)
        patch('clavicle bridge',[(sign*x,z) for x,z in [(.072,1.488),(.125,1.484),(.222,1.443),
              (.204,1.421),(.112,1.454),(.071,1.453)]],
              lambda x,z,d:(x,-.094+1.2*x*x-d,z),'UpperChest',g.armor,.003)
        patch('pelvic wing',[(sign*x,z) for x,z in [(.070,1.055),(.158,1.053),(.202,1.001),
              (.186,.918),(.122,.909),(.087,.971)]],
              lambda x,z,d:(x,-.124+.8*x*x-d,z),'Hips',g.armor,.007)
        patch('rear pelvic wing',[(sign*x,z) for x,z in [(.015,1.044),(.138,1.035),(.177,.989),
              (.155,.940),(.026,.958)]],
              lambda x,z,d:(x,.153-.6*x*x+d,z),'Hips',g.armor,.004)
    patch('pelvic center',[(-.061,1.046),(.061,1.046),(.074,.997),(.045,.913),
          (-.045,.913),(-.074,.997)],lambda x,z,d:(x,-.130-d,z),'Hips',g.armor,.006)
    for top,bottom in [(1.246,1.177),(1.174,1.105)]:
        patch('lumbar lame',[(-.145,top),(.145,top),(.140,bottom+.012),(.06,bottom),(-.06,bottom),(-.140,bottom+.012)],
              lambda x,z,d:(x,.173-.65*x*x+d,z),'Spine',g.armor,.004)


def legs():
    from sculpt_forms import curve
    for side,sign in [('Left',1),('Right',-1)]:
        for region,profile,start,end in [
            ('UpperLeg',[(0,.101,.118),(.25,.120,.128),(.60,.105,.116),(1,.068,.078)],.04,.90),
            ('LowerLeg',[(0,.068,.073),(.25,.081,.102),(.55,.076,.087),(1,.049,.054)],.18,.94)]:
            bone=g.rig.data.bones[side+region];a=bone.head_local;b=bone.tail_local
            axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized()
            def mapping(angle,t,d):
                rx=curve([(t,x) for t,x,y in profile],t);ry=curve([(t,y) for t,x,y in profile],t)
                return a.lerp(b,t)+lateral*(rx*math.sin(angle))+front*(ry*math.cos(angle)+d)
            outline=[(-1.18,start+.07),(-.55,start),(.40,start+.015),(1.18,start+.12),
                     (1.13,end-.11),(.45,end),(-.34,end-.015),(-1.10,end-.12)]
            patch(side+region+' main shell',outline,mapping,side+region,g.armor,.007)
            # Rear calf protection supplies a readable continuous silhouette from behind.
            if region=='LowerLeg':
                patch(side+' calf shell',[(-.8,.21),(.8,.21),(.9,.47),(.54,.86),(-.54,.86),(-.9,.47)],
                      lambda angle,t,d:a.lerp(b,t)+lateral*(curve([(t,x) for t,x,y in profile],t)*math.sin(angle))-front*(curve([(t,y) for t,x,y in profile],t)*math.cos(angle)+d),
                      side+region,g.armor,.004)
                patch(side+' knee shield',[(-.70,-.055),(.70,-.055),(.92,.06),(.55,.20),(0,.25),(-.65,.18)],mapping,side+region,g.armor,.009)
            # Assigned-color vertical inset, seated on the shell rather than around the leg.
            patch(side+region+' identity inset',[(-.21,.27),(.19,.29),(.22,.59),(-.14,.68)],
                  lambda angle,t,d:mapping(angle,t,d+.010),side+region,g.paint,.002)
        x=g.rig.data.bones[side+'Foot'].head_local.x
        patch(side+' boot shell',[(-.046,-.156),(.046,-.156),(.059,-.127),(.057,.025),(.037,.052),(-.037,.052),(-.057,.025),(-.059,-.127)],
              lambda u,v,d:(x+u,v,.043+.055*math.exp(-((v+.012)/.11)**2)+d),side+'Foot',g.armor,.003)


def surfaces():
    """Portable packed PBR maps, with small coating grain and textile weave."""
    size=256;rng=random.Random(181028)
    def image(name,pixels):
        result=bpy.data.images.new(name,width=size,height=size);result.pixels=pixels;result.pack();return result
    armor=[];normal=[];fabric=[];weave=[]
    # One shared UV panel atlas describes actual inset seams and captive fasteners.
    # Relief is packed into tangent normals, avoiding extra per-bot draw calls.
    lines=[((.5,1),(.5,.68)),((.5,.68),(.57,.54)),((.57,.54),(.57,.08)),
           ((.08,.72),(.23,.72)),((.23,.72),(.34,.57)),
           ((.92,.72),(.77,.72)),((.77,.72),(.66,.57))]
    bolts=[(.18,.25),(.82,.25),(.22,.82),(.78,.82)]
    def relief(u,v):
        distance=1
        for a,b in lines:
            dx=b[0]-a[0];dy=b[1]-a[1];t=max(0,min(1,((u-a[0])*dx+(v-a[1])*dy)/(dx*dx+dy*dy)))
            distance=min(distance,math.hypot(u-a[0]-t*dx,v-a[1]-t*dy))
        depth=-.0020*math.exp(-(distance/.003)**2)
        for x,y in bolts:
            r=math.hypot(u-x,v-y)
            depth-=.0025*math.exp(-((r-.011)/.003)**2)
            if r<.008:depth-=.001
            if abs(u-x)<.006 and abs(v-y)<.0015:depth-=.0015
        return depth
    for y in range(size):
        for x in range(size):
            grain=rng.uniform(-.025,.025)
            scratch=-.12 if (x+3*y)%251==0 and rng.random()<.35 else 0
            u=x/size;v=y/size;h=relief(u,v)
            color=.64+grain+scratch+max(-.22,h*80);armor.extend((color,color,color,1))
            dx=(relief(u+1/size,v)-relief(u-1/size,v))*size
            dy=(relief(u,v+1/size)-relief(u,v-1/size))*size
            n=Vector((-dx,-dy,1)).normalized();normal.extend((n.x*.5+.5,n.y*.5+.5,n.z*.5+.5,1))
            warp=math.sin(x*math.tau/8);weft=math.sin(y*math.tau/8)
            v=.022+grain*.10+.004*(warp+weft);fabric.extend((v,v*1.06,v*1.1,1))
            n=Vector((warp*.12,weft*.12,1)).normalized();weave.extend((n.x*.5+.5,n.y*.5+.5,n.z*.5+.5,1))
    coat=image('vector-v2-coated-metal-albedo',armor);grain=image('vector-v2-coating-normal',normal)
    textile=image('vector-v2-textile-albedo',fabric);textile_normal=image('vector-v2-textile-normal',weave)
    grain.colorspace_settings.name='Non-Color';textile_normal.colorspace_settings.name='Non-Color'
    for material,color,norm,metal,rough in [
      (g.paint,coat,grain,.45,.43),(g.armor,coat,grain,.72,.46),
      (bpy.data.materials['vector-fighter-sewn-pants'],textile,textile_normal,0,.86),
      (bpy.data.materials['vector-fighter-fabric'],textile,textile_normal,0,.86)]:
        material.use_nodes=True;nodes=material.node_tree.nodes;nodes.clear()
        bs=nodes.new('ShaderNodeBsdfPrincipled');out=nodes.new('ShaderNodeOutputMaterial')
        material.node_tree.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
        bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
        tex=nodes.new('ShaderNodeTexImage');tex.image=color;material.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
        tex=nodes.new('ShaderNodeTexImage');tex.image=norm;n=nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.35
        material.node_tree.links.new(tex.outputs['Color'],n.inputs['Color']);material.node_tree.links.new(n.outputs['Normal'],bs.inputs['Normal'])


def shoulder_caps():
    """Closed fitted shoulder shells, with longitudinal topology around the limb."""
    for obj in list(bpy.context.scene.objects):
        if obj.type=='MESH' and 'UpperArm contoured' in obj.name:bpy.data.objects.remove(obj,do_unlink=True)
    for side in ['Left','Right']:
        bone=g.rig.data.bones[side+'UpperArm'];a=bone.head_local;b=bone.tail_local
        axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized()
        rings=[(-.40,.030),(-.31,.075),(-.10,.119),(.18,.128),(.43,.117),(.59,.097),(.62,.094)]
        verts=[];faces=[];n=32
        for t,radius in rings:
            for i in range(n):
                angle=math.tau*i/n
                sn=math.copysign(abs(math.sin(angle))**.85,math.sin(angle));cs=math.copysign(abs(math.cos(angle))**.85,math.cos(angle))
                verts.append(a.lerp(b,t)+lateral*(radius*sn)+front*(radius*.95*cs))
        for row in range(len(rings)-1):
            for i in range(n):
                j=row*n+i;k=row*n+(i+1)%n;faces.append((j,k,k+n,j+n))
        faces.extend([tuple(reversed(range(n))),tuple(range((len(rings)-1)*n,len(rings)*n))])
        obj=g.finish('VECTOR V2 '+side+' fitted shoulder',verts,faces,g.armor,side+'UpperArm')
        obj.data.materials.append(g.paint);obj.data.materials.append(g.trim)
        for polygon in obj.data.polygons:
            row=polygon.index//n;angle=(polygon.index%n+.5)*math.tau/n
            if row in (1,2) and math.cos(angle)>.25:polygon.material_index=1
            elif row>=5:polygon.material_index=2
            for li in polygon.loop_indices:
                vi=obj.data.loops[li].vertex_index
                obj.data.uv_layers.active.data[li].uv=((vi%n)/n,(vi//n)/(len(rings)-1))


def main():
    global clearance
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f17-assault.blend'))
    g.rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');g.parts=[]
    g.paint=bpy.data.materials['vector-fighter-identity'].copy();g.paint.name='vector-fighter-identity-v2'
    g.armor=bpy.data.materials['vector-assault-armor'].copy();g.armor.name='vector-assault-armor-v2'
    g.metal=bpy.data.materials['vector-fighter-titanium']
    g.trim=bpy.data.materials.get('vector-fighter-trim') or next(m for m in bpy.data.materials if 'trim' in m.name)
    foundation();tailored_legs()
    vertices=[];faces=[]
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or not any(k in obj.name for k in ('fabric','trousers')):continue
        offset=len(vertices);vertices.extend(obj.matrix_world@v.co for v in obj.data.vertices)
        faces.extend(tuple(offset+i for i in p.vertices) for p in obj.data.polygons)
    clearance=BVHTree.FromPolygons(vertices,faces)
    torso();legs();shoulder_caps();surfaces()
    bpy.context.preferences.filepaths.save_version=0
    output=ROOT/'scripts/vector_fighter/f18-heavy-agile.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    print('ASSAULT_V2_SOURCE',output)


if __name__=='__main__':main()
