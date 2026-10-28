"""Bounded chest/shoulder construction study. No runtime promotion or reduction."""
import sys,math,subprocess,json
from pathlib import Path
import bpy,bmesh
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import author_reference as r
import build as g
OUT=ROOT/'.local/vector-upper-study';OUT.mkdir(parents=True,exist_ok=True)
BASE='b4490f620472acf26c961523c8d88cf19c4d6e38'


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
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH':continue
        if any(tag in obj.name for tag in remove):bpy.data.objects.remove(obj,do_unlink=True);continue
        if 'torso fabric' in obj.name:
            bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if (obj.matrix_world@f.calc_center_median()).z>1.25],context='FACES');bm.to_mesh(obj.data);bm.free()
        if 'UpperArm fabric' in obj.name:
            bm=bmesh.new();bm.from_mesh(obj.data);deleted=[]
            for f in bm.faces:
                p=obj.matrix_world@f.calc_center_median();side='Left' if p.x>0 else 'Right';bone=g.rig.data.bones[side+'UpperArm'];d=bone.tail_local-bone.head_local;t=(p-bone.head_local).dot(d)/d.length_squared
                if t<.67:deleted.append(f)
            bmesh.ops.delete(bm,geom=deleted,context='FACES');bm.to_mesh(obj.data);bm.free()
            # Blend the retained elbow-side sleeve into the new fitted gusset.
            # Preserve its original rig weights and the wrist-side geometry.
            for vertex in obj.data.vertices:
                p=obj.matrix_world@vertex.co;side='Left' if p.x>0 else 'Right'
                bone=g.rig.data.bones[side+'UpperArm'];d=bone.tail_local-bone.head_local
                t=(p-bone.head_local).dot(d)/d.length_squared
                if .60<t<1.06:
                    anchor=bone.head_local+d*t;radial=p-anchor
                    limit=.061+.004*max(0,(t-.90)/.16)
                    if radial.length>limit:vertex.co=obj.matrix_world.inverted()@(anchor+radial.normalized()*limit)
    def body(a,z,clearance=0):
        t=max(0,min(1,(z-1.25)/.20));width=.177+.027*math.sin(math.pi*t*.88);depth=.143+.015*math.sin(math.pi*t)
        depth-=.047*max(0,(z-1.385)/.075)**1.25
        # Squared ribcage rather than a round mannequin under flat shields.
        sa,ca=math.sin(a),math.cos(a)
        return Vector(((width+clearance)*math.copysign(abs(sa)**.66,sa),.025-(depth+clearance)*math.copysign(abs(ca)**.66,ca),z))
    def top(a):return 1.427+.041*math.sin(a*1.7)**2-.098*math.exp(-((abs(a)-math.pi/2)/.32)**2)
    def chest(q,offset=0):
        angle,v=q;z=1.267+(top(angle)-1.267)*v
        p=body(angle,z)
        # A shallow molded ridge belongs to the chest surface itself.
        ridge=.009*max(0,1-abs(v-.62)/.28)*max(0,1-abs(abs(angle)-.64)/.55)
        p.y-=ridge+offset
        return p
    # A tailored support vest shares the SAME contour as the armor, 8 mm inboard.
    for sign in [-1,1]:
        shell('textile ribcage '+str(sign),lambda u,v,sign=sign:body(sign*math.pi*u,1.245+(top(sign*math.pi*u)-1.245)*v,-.009),'Chest',r.cloth,64,36,.003)
    # Angular boundaries cut a continuous shaped volume, rather than forming
    # separate flat shields. The dark backing is only 5–7 mm below the face.
    def plate(name,outline):
        o=r.mapped_plate('study '+name,outline,'Chest',r.steel,chest,lambda q:Vector((0,-1,0)),(.6,.6))
        o['study_shell']=True
        return o
    plate('sternum',[(-.28,.42),(-.28,.88),(-.19,.98),(.19,.98),(.28,.88),(.28,.42),(.16,.27),(-.16,.27)])
    for sign in [-1,1]:
        plate('pectoral '+str(sign),[(sign*a,v) for a,v in [(.31,.43),(.31,.88),(.43,.99),(.85,.99),(1.10,.82),(1.10,.42),(.83,.24),(.52,.25)]])
        plate('costal front '+str(sign),[(sign*a,v) for a,v in [(.20,.22),(.51,.21),(.82,.20),(1.10,.37),(1.11,.07),(.77,.02),(.43,.04),(.18,.10)]])
        # Narrow molded recesses follow the same surface; bright ID is restricted
        # to shoulder harness hardware rather than broad painted rectangles.
        r.line('study pectoral groove '+str(sign),[chest(Vector((sign*a,v)),.0003) for a,v in [(.38,.73),(.55,.79),(.82,.81),(1.01,.67)]],'Chest',r.seal,.0014)
    for v in [.72,.84]:
        r.line('study sternum recess',[chest(Vector((a,v)),.0003) for a in [-.14,-.07,0,.07,.14]],'Chest',r.seal,.0016)
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
        # A sewn gusset and suspension web form the upper arm transition.
        side='Left' if sign>0 else 'Right';a,b,f,l=r.limb_frame(side,'UpperArm')
        axis=(b-a).normalized()
        def arm(u,v,inner=False,back=False):
            angle=-1.56+3.12*u;front=-f if back else f
            t=-.30+1.25*v
            radius=.075*math.sqrt(max(.001,1-((t-.13)/.50)**2)) if t<.13 else .075-.015*(t-.13)/.82
            if inner:radius-=.004
            fold=(.0018*math.sin(v*math.pi*22)*math.sin(math.pi*v)**2) if inner else 0
            return a.lerp(b,t)+l*((radius+fold)*math.sin(angle))+front*((radius+fold)*math.cos(angle))
        for back in [False,True]:
            shell('woven axillary gusset '+side+str(back),lambda u,v,back=back:arm(u,v,True,back),side+'UpperArm',r.cloth,48,54,.003)
        def cap(u,v,back=False):
            angle=-1.56+3.12*u;front=-f if back else f
            start=-.37;end=.48-.07*abs(angle)/1.56;t=start+(end-start)*v
            radius=.090*math.sqrt(max(.0004,1-((t-.13)/.50)**2))
            return a.lerp(b,t)+l*(radius*math.sin(angle))+front*((radius+.006)*math.cos(angle))
        for back in [False,True]:
            for sector,(lo,hi) in enumerate([(0,.278),(.286,.714),(.722,1)]):
                shell('deltoid main '+side+str(back)+str(sector),lambda u,v,lo=lo,hi=hi,back=back:cap(lo+(hi-lo)*u,v*.77,back),side+'UpperArm',r.steel,24,40,.006)
            shell('deltoid lower articulation '+side+str(back),lambda u,v,back=back:cap(u,.795+v*.205,back),side+'UpperArm',r.steel,56,14,.006)
        # Suspension runs across the CLOSED yoke, with integral clasp pockets.
        def strap(u,v,sign=sign):
            angle=sign*(.52+u*.25);p=yoke(abs(angle)/math.pi,.10+.76*v);p.z+=.006
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
    target=ROOT/'scripts/vector_fighter/f20-upper-study.blend';bpy.ops.wm.save_as_mainfile(filepath=str(target))
    print('UPPER_STUDY_SOLIDS',count,'SOURCE',target)

if __name__=='__main__':main()
