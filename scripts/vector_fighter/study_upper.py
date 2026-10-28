"""Bounded chest/shoulder construction study. No runtime promotion or reduction."""
import sys,math,subprocess,json
from pathlib import Path
import bpy,bmesh
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import author_reference as r
import build as g
OUT=ROOT/'.local/vector-upper-study-f21';OUT.mkdir(parents=True,exist_ok=True)
BASE='b4490f620472acf26c961523c8d88cf19c4d6e38'


def profile(points,x,rounding=.035):
    """Linear cage faces with short tangent-continuous corner transitions."""
    if x<=points[0][0]:return points[0][1]
    if x>=points[-1][0]:return points[-1][1]
    for previous,corner,following in zip(points,points[1:],points[2:]):
        dx=x-corner[0];h=min(rounding,(corner[0]-previous[0])*.3,(following[0]-corner[0])*.3)
        if abs(dx)<h:
            left=(corner[1]-previous[1])/(corner[0]-previous[0]);right=(following[1]-corner[1])/(following[0]-corner[0])
            return corner[1]+left*dx+(right-left)*(dx+h)**2/(4*h)
    for first,last in zip(points,points[1:]):
        if x<=last[0]:return first[1]+(last[1]-first[1])*(x-first[0])/(last[0]-first[0])


def shell(name,surface,bone,mat,nu=48,nv=30,thickness=.006,identity=None):
    """Regular quad shell: its actual depth wraps the fitted textile volume."""
    vertices=[];faces=[];stride=nu+1
    for j in range(nv+1):
        for i in range(nu+1):vertices.append(surface(i/nu,j/nv))
    for j in range(nv):
        for i in range(nu):k=j*stride+i;faces.append((k,k+1,k+1+stride,k+stride))
    obj=g.finish('VECTOR STUDY '+name,vertices,faces,mat,bone)
    obj.modifiers.remove(obj.modifiers.get('Skin'))
    obj.data.materials.append(r.edge);obj.data.materials.append(r.paint)
    for j in range(nv):
        for i in range(nu):
            face=obj.data.polygons[j*nu+i]
            if identity and identity((i+.5)/nu,(j+.5)/nv):face.material_index=2
    for face in obj.data.polygons:
        for li in face.loop_indices:
            vi=obj.data.loops[li].vertex_index;j,i=divmod(vi,stride)
            obj.data.uv_layers.active.data[li].uv=(i/nu*1.3,j/nv*.7)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    # Solidify inward keeps outer dimensions shared by neighboring shell parts.
    mod=obj.modifiers.new('Continuous inward wall','SOLIDIFY');mod.thickness=thickness;mod.offset=-1;mod.use_even_offset=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=obj.modifiers.new('Machined perimeter','BEVEL');mod.width=.0012;mod.segments=3;mod.limit_method='ANGLE';mod.angle_limit=.65;mod.harden_normals=True;mod.material=1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=obj.modifiers.new('Surface normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=40
    bpy.ops.object.modifier_apply(modifier=mod.name);obj.select_set(False)
    mod=obj.modifiers.new('Skin','ARMATURE');mod.object=g.rig
    obj['study_shell']=True
    return obj


def main():
    pinned=OUT/'previous-source.blend'
    pinned.write_bytes(subprocess.check_output(['git','show',BASE+':scripts/vector_fighter/f19-reference.blend'],cwd=ROOT))
    bpy.ops.wm.open_mainfile(filepath=str(pinned));g.rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');g.parts=[]
    g.rig.data.pose_position='REST'
    for key,name in [('steel','vector-reference-coated-steel'),('paint','vector-fighter-identity-reference'),('edge','vector-reference-edge-metal'),('cloth','vector-reference-woven-fabric'),('seal','vector-reference-flexible-seals'),('webbing','vector-reference-webbing'),('glass','vector-reference-opaque-visor')]:setattr(r,key,bpy.data.materials[name])
    # Remove the whole old thoracic construction, not just its visible decals.
    remove=['fitted ballistic vest','pectoral','chest identity','costal plate','thoracic wrap','clavicle','rear scapula','rear ID','sternum','collar rail','chest service','shoulder','bicep guard']
    sleeve_boundaries={}
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH':continue
        if any(tag in obj.name for tag in remove):bpy.data.objects.remove(obj,do_unlink=True);continue
        if 'torso fabric' in obj.name:
            bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if (obj.matrix_world@f.calc_center_median()).z>1.25],context='FACES');bm.to_mesh(obj.data);bm.free()
        if 'UpperArm fabric' in obj.name:
            # Cut at one exact cross-section, rather than deleting entire faces
            # by their centers (that left a visibly serrated overlapping cuff).
            bm=bmesh.new();bm.from_mesh(obj.data)
            # The baseline stores BOTH sleeves in one mesh. Isolate each side
            # before cutting so the opposite sleeve cannot enter this plane.
            for side,sign in [('Left',1),('Right',-1)]:
                bone=g.rig.data.bones[side+'UpperArm'];d=bone.tail_local-bone.head_local
                vertices={v for v in bm.verts if sign*(obj.matrix_world@v.co).x>0}
                geom=list(vertices)+[e for e in bm.edges if all(v in vertices for v in e.verts)]+[f for f in bm.faces if all(v in vertices for v in f.verts)]
                bmesh.ops.bisect_plane(bm,geom=geom,dist=.000001,
                    plane_co=obj.matrix_world.inverted()@(bone.head_local+d*.98),
                    plane_no=obj.matrix_world.to_3x3().transposed()@d.normalized(),clear_inner=True)
            bm.to_mesh(obj.data);bm.free()
            # Keep wrist geometry/weights; taper only the short elbow transition.
            for vertex in obj.data.vertices:
                p=obj.matrix_world@vertex.co;side='Left' if p.x>0 else 'Right'
                bone=g.rig.data.bones[side+'UpperArm'];d=bone.tail_local-bone.head_local
                t=(p-bone.head_local).dot(d)/d.length_squared
                if .95<t<1.08:
                    anchor=bone.head_local+d*t;radial=p-anchor
                    limit=.057+.008*max(0,(t-.98)/.10)
                    if radial.length>limit:vertex.co=obj.matrix_world.inverted()@(anchor+radial.normalized()*limit)
            for side,sign in [('Left',1),('Right',-1)]:
                a,b,front,lateral=r.limb_frame(side,'UpperArm');d=b-a;boundary=[]
                for vertex in obj.data.vertices:
                    p=obj.matrix_world@vertex.co;t=(p-a).dot(d)/d.length_squared
                    if sign*p.x>0 and abs(t-.98)<.0001:
                        radial=p-(a+d*t);boundary.append((math.atan2(radial.dot(lateral),radial.dot(front)),radial.length))
                assert len(boundary)>=8,side+' retained sleeve must provide an exact join ring'
                boundary=sorted({round(angle,6):radius for angle,radius in boundary}.items())
                sleeve_boundaries[side]=[(angle+shift,radius) for shift in [-math.tau,0,math.tau] for angle,radius in boundary]
    def body(a,z,clearance=0):
        t=max(0,min(1,(z-1.25)/.20));width=.177+.027*math.sin(math.pi*t*.88);depth=.143+.015*math.sin(math.pi*t)
        depth-=.047*max(0,(z-1.385)/.075)**1.25
        # Explicit ribcage cage: broad front planes and a turned costal side.
        # Keeping these angular stations gives the armor actual major facets.
        stations=[(0,0,-1),(.26,.28,-1),(.62,.65,-.93),(1.05,.91,-.67),(math.pi/2,1,0),(2.10,.89,.64),(2.55,.60,.93),(math.pi,0,1)]
        angle=abs(a)
        x=profile([(q,x) for q,x,y in stations],angle,.07);y=profile([(q,y) for q,x,y in stations],angle,.07)
        return Vector((math.copysign((width+clearance)*x,a),.025+(depth+clearance)*y,z))
    def top(a):return 1.427+.041*math.sin(a*1.7)**2-.098*math.exp(-((abs(a)-math.pi/2)/.32)**2)
    def chest(q,offset=0):
        angle,v=q;z=1.267+(top(angle)-1.267)*v
        p=body(angle,z)
        # A shallow molded ridge belongs to the chest surface itself.
        ridge=.009*profile([(0,0),(.34,0),(.62,1),(.90,0),(1,0)],v,.07)*profile([(0,0),(.09,0),(.64,1),(1.19,0),(math.pi,0)],abs(angle),.07)
        p.y-=ridge+offset
        return p
    # A tailored support vest shares the SAME contour as the armor, 8 mm inboard.
    for sign in [-1,1]:
        shell('textile ribcage '+str(sign),lambda u,v,sign=sign:body(sign*math.pi*u,1.245+(top(sign*math.pi*u)-1.245)*v,-.009),'Chest',r.cloth,64,36,.003)
    # Angular boundaries cut a continuous shaped volume, rather than forming
    # separate flat shields. The dark backing is only 5–7 mm below the face.
    def plate(name,outline):
        o=r.mapped_plate('study '+name,outline,'Chest',r.steel,chest,lambda q:Vector((0,-1,0)),(.6,.6),refinement=4)
        o['study_shell']=True
        return o
    plate('sternum',[(-.26,.55),(-.27,.87),(-.20,.98),(.20,.98),(.27,.87),(.26,.55),(.13,.48),(-.13,.48)])
    for sign in [-1,1]:
        plate('pectoral '+str(sign),[(sign*a,v) for a,v in [(.30,.55),(.30,.87),(.43,.99),(.83,.99),(1.10,.79),(1.10,.54),(.87,.36),(.52,.37)]])
        plate('costal front '+str(sign),[(sign*a,v) for a,v in [(.21,.43),(.51,.33),(.85,.32),(1.10,.48),(1.11,.17),(.76,-.03),(.44,.04),(.16,.10)]])
        # Narrow molded recesses follow the same surface; bright ID is restricted
        # to shoulder harness hardware rather than broad painted rectangles.
        r.line('study pectoral groove '+str(sign),[chest(Vector((sign*a,v)),.001) for a,v in [(.36,.64),(.52,.55),(.82,.55),(1.02,.70)]],'Chest',r.seal,.0015)
    for v in [.65,.84]:
        r.line('study sternum recess',[chest(Vector((a,v)),.0003) for a in [-.14,-.07,0,.07,.14]],'Chest',r.seal,.0016)
    plate('lower sternum',[(-.13,.43),(.13,.43),(.19,.32),(.12,-.02),(-.12,-.02),(-.19,.32)])
    # Side shells continue around the ribcage, ending at a shaped armhole.
    for sign in [-1,1]:
        shell('costal return '+str(sign),lambda u,v,sign=sign:body(sign*(1.14+.68*u),1.27+(top(sign*(1.14+.68*u))-1.27)*v),'Chest',r.steel,40,28,.006)
        shell('scapular shell '+str(sign),lambda u,v,sign=sign:body(sign*(1.84+1.28*u),1.28+(top(sign*(1.84+1.28*u))-1.28)*v),'Chest',r.steel,48,28,.006)
        # Close the upper vest around a fitted neck aperture. It is a sloping
        # textile yoke, not an open bucket surrounding a separate neck cylinder.
        def yoke(u,v,sign=sign):
            angle=sign*math.pi*u;outer=body(angle,top(angle)-.006,-.009)
            inner=Vector((.068*math.sin(angle),.028-.062*math.cos(angle),1.462))
            p=outer.lerp(inner,v);p.z+=.010*math.sin(math.pi*v)
            return p
        shell('closed shoulder yoke '+str(sign),yoke,'UpperChest',r.cloth,64,24,.004)
        # A low frontal collar rises along the sides and back of the neck.
        # It is seated on the closed textile yoke, with an inward-sloping lip.
        for sector,(lo,hi) in enumerate([(.05,.73),(.76,1.48),(1.51,2.28),(2.31,3.10)]):
            def collar(u,v,lo=lo,hi=hi,sign=sign):
                angle=sign*(lo+(hi-lo)*u);rise=.055*(1-math.cos(angle))*.5
                rx=.093-.014*v;ry=.082-.012*v
                return Vector((rx*math.sin(angle),.028-ry*math.cos(angle),1.446+(.022+rise)*v))
            shell('armored collar '+str(sign)+str(sector),collar,'UpperChest',r.steel,22,10,.005)
        # A sewn gusset and suspension web form the upper arm transition.
        side='Left' if sign>0 else 'Right';a,b,f,l=r.limb_frame(side,'UpperArm')
        axis=(b-a).normalized()
        def arm(u,v,inner=False,back=False):
            angle=-1.56+3.12*u;front=-f if back else f
            t=-.42+1.404*v
            radius=.071*math.sqrt(max(.001,1-((t-.13)/.50)**2)) if t<.13 else .071-.010*(t-.13)/.87
            if inner:radius=max(.002,radius-.004)
            # Seat the fabric below the shoulder's faceted lower return.
            limit=profile([(-.42,.003),(-.40,.009),(-.32,.043),(-.16,.070),(.09,.080),(.29,.075),(.47,.064),(.60,.050)],t)*.86
            blend=max(0,min(1,(.66-t)/.14))
            radius=radius*(1-blend)+min(radius,limit)*blend
            # Match the actual retained sleeve cross-section at the elbow;
            # a circular guess leaves a visible open cuff on the rear side.
            phi=math.atan2(math.sin(angle),(-1 if back else 1)*math.cos(angle))
            target=profile(sleeve_boundaries[side],phi,.025)
            join=max(0,min(1,(t-.86)/.12));join=join*join*(3-2*join)
            radius=radius*(1-join)+target*join
            fold=(.0018*math.sin(v*math.pi*22)*math.sin(math.pi*v)**2) if inner else 0
            fold*=1-join
            return a.lerp(b,t)+l*((radius+fold)*math.sin(angle))+front*((radius+fold)*math.cos(angle))
        for back in [False,True]:
            shell('woven axillary gusset '+side+str(back),lambda u,v,back=back:arm(u,v,True,back),side+'UpperArm',r.cloth,48,54,.003)
        outward=-sign*l
        def shoulder(q,offset=0):
            angle,t=q
            radius=profile([(-.42,.003),(-.40,.009),(-.32,.043),(-.16,.070),(.09,.080),(.29,.075),(.47,.064),(.60,.050)],t)
            # Octagonal cross-section provides flat manufactured faces.
            step=math.pi/4
            stations=[(j*step,math.cos(j*step),math.sin(j*step)) for j in range(-5,6)]
            xx=profile([(q,x) for q,x,y in stations],angle,.08);yy=profile([(q,y) for q,x,y in stations],angle,.08)
            normal=outward*math.cos(angle)+f*math.sin(angle)
            return a.lerp(b,t)+outward*(radius*xx)+f*(radius*.98*yy)+normal*offset
        for half in [-1,1]:
            shell('shoulder fitted cradle '+side+str(half),lambda u,v,half=half:shoulder(Vector((half*math.pi*u,-.405+.695*v)),-.004),side+'UpperArm',r.seal,64,48,.003)
        shoulder_parts=[
            ('outer shield',[(-.73,-.24),(-.35,-.33),(.35,-.33),(.73,-.24),(.78,.17),(.51,.32),(-.51,.32),(-.78,.17)]),
            ('lower return',[(-.78,.205),(-.49,.35),(.49,.35),(.78,.205),(.85,.40),(.44,.50),(-.44,.50),(-.85,.40)]),
            ('front bevel',[(.77,-.24),(1.30,-.29),(1.97,-.15),(2.13,.18),(1.76,.35),(.83,.17)]),
            ('rear bevel',[(-.77,-.24),(-1.30,-.29),(-1.97,-.15),(-2.13,.18),(-1.76,.35),(-.83,.17)]),
            ('crown',[(-1.35,-.31),(-1.15,-.39),(1.15,-.39),(1.35,-.31),(.70,-.27),(.32,-.36),(-.32,-.36),(-.70,-.27)])]
        for name,outline in shoulder_parts:
            obj=r.mapped_plate('study shoulder '+name+' '+side,outline,side+'UpperArm',r.steel,shoulder,
                lambda q:outward*math.cos(q.x)+f*math.sin(q.x),(.35,.8),refinement=4);obj['study_shell']=True
        for angle in [-.45,.45]:
            r.line('study shoulder machining '+side+str(angle),[shoulder(Vector((angle,t)),.001) for t in [-.18,-.04,.10]],side+'UpperArm',r.seal,.0013)
        # Short upper-arm protection follows the sleeve radius and meets the
        # shoulder's front return; it does not float on a cylindrical backing.
        def bicep(q,offset=0):
            angle,t=q;radius=.067-.012*(t-.4)
            radial=outward*math.cos(angle)+f*math.sin(angle)
            return a.lerp(b,t)+radial*(radius+offset)
        obj=r.mapped_plate('study upper-arm transition '+side,[(.60,.43),(1.20,.37),(1.80,.46),(1.85,.70),(1.45,.84),(.80,.80),(.55,.63)],
            side+'UpperArm',r.steel,bicep,lambda q:outward*math.cos(q.x)+f*math.sin(q.x),(.3,.8),refinement=4)
        obj['study_shell']=True
        for u in [.20,.40,.60,.80]:
            r.line('study sleeve seam '+side+str(u),[arm(u,.57+i*.025,True,False) for i in range(15)],side+'UpperArm',r.webbing,.0007)
        # Suspension runs across the CLOSED yoke, with integral clasp pockets.
        def strap(u,v,sign=sign):
            angle=sign*(.45+u*.34);p=yoke(abs(angle)/math.pi,.02+.84*v);p.z+=.006
            return p
        shell('collar suspension '+side,strap,'UpperChest',r.webbing,10,32,.005)
        for k in range(2):
            def clasp(u,v,k=k):
                p=strap(.05+.9*u,.20+k*.34+v*.15);p.z+=.004
                return p
            shell('harness clasp '+side+str(k),clasp,'UpperChest',r.edge,12,8,.004,lambda u,v:.2<u<.8 and .22<v<.78)
        r.line('armhole stitched edge '+side,[body(sign*(1.0+i*.045),top(sign*(1.0+i*.045))-.004,-.008) for i in range(20)],'Chest',r.webbing,.0017)
    r.textile_uvs()
    # Audit this bounded study's actual closed shell geometry.
    count=0
    for obj in bpy.context.scene.objects:
        if not obj.get('study_shell'):continue
        bm=bmesh.new();bm.from_mesh(obj.data)
        assert all(e.is_manifold for e in bm.edges),obj.name+' closed shell'
        bm.free();count+=1
    bpy.context.preferences.filepaths.save_version=0
    g.rig.data.pose_position='POSE'
    target=ROOT/'scripts/vector_fighter/f21-upper-study.blend';bpy.ops.wm.save_as_mainfile(filepath=str(target))
    print('UPPER_STUDY_SOLIDS',count,'SOURCE',target)

if __name__=='__main__':main()
