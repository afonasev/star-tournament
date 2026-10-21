"""VECTOR costume remodeling: extracted sewn cloth and new shaped shell topology.
All dimensions are offline mesh authoring; rig, clips, weapon geometry remain intact.
"""
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

def build(g,style):
    pi=math.pi
    # Extract only the sewn sleeve/trouser/boot components from the pinned source.
    # Component counts refer to the welded, pinned BASE mesh, not a live heuristic.
    allowed={'material':{141,131,167},'pants':{349,310,252}}
    for o in g.source_garment:
        material=o.data.materials[0].name
        if material not in allowed:continue
        bpy.context.collection.objects.link(o)
        bm=bmesh.new();bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        remaining=set(bm.verts);remove=[]
        while remaining:
            todo=[remaining.pop()];component=[]
            while todo:
                v=todo.pop();component.append(v)
                for edge in v.link_edges:
                    other=edge.other_vert(v)
                    if other in remaining:remaining.remove(other);todo.append(other)
            if len(component) not in allowed[material]:remove.extend(component)
        bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(o.data);bm.free()
        # The old source contains outer armor and straps as independent components;
        # the retained components carry only sewn folds and their original UVs.
        world=o.matrix_world.copy();o.data.transform(world);o.matrix_world=Matrix.Identity(4)
        for v in o.data.vertices:
            p=v.co
            if material=='pants':
                # Shaped close-fitting trouser, with calf taper and retained knees.
                factor=.90 if p.z>.65 else .82 if p.z>.19 else 1
                side=1 if p.x>0 else -1;bone=g.rig.data.bones[('Left' if side>0 else 'Right')+('UpperLeg' if p.z>.62 else 'LowerLeg')]
                axis=bone.tail_local-bone.head_local;t=max(0,min(1,(p-bone.head_local).dot(axis)/axis.length_squared));c=bone.head_local+axis*t
                if p.z>.19:p.x=c.x+(p.x-c.x)*factor;p.y=c.y+(p.y-c.y)*factor
            else:
                side='Left' if p.x>0 else 'Right';bone=g.rig.data.bones[side+('LowerArm' if abs(p.x)>.39 else 'UpperArm')]
                a=bone.head_local;b=g.rig.data.bones[side+'Hand'].head_local if 'Lower' in bone.name else bone.tail_local
                axis=b-a;t=max(0,min(1,(p-a).dot(axis)/axis.length_squared));c=a+axis*t
                # Preserve thin FPS forearms while retaining the sewn elbow volume.
                factor=.75 if 'Lower' in bone.name else .93
                v.co=c+(p-c)*factor
        for m in o.data.materials:
            m.name='vector-fighter-sewn-'+material;m.use_nodes=True
            bs=m.node_tree.nodes.get('Principled BSDF')
            for key in ['Base Color','Metallic','Roughness']:
                for link in list(bs.inputs[key].links):m.node_tree.links.remove(link)
            bs.inputs['Base Color'].default_value=(.018,.022,.026,1);bs.inputs['Metallic'].default_value=0;bs.inputs['Roughness'].default_value=.96
            for n in m.node_tree.nodes:
                if n.type=='NORMAL_MAP':n.inputs['Strength'].default_value=.25
        for mod in o.modifiers:
            if mod.type=='ARMATURE':mod.object=g.rig
        o.parent=g.rig;o.name='VECTOR tailored UpperArm fabric' if material=='material' else 'VECTOR tailored trousers and boots';g.parts.append(o)
    # New sewn torso replaces the source vest completely. Sections carry rib cage,
    # waist, pelvis overlap and trapezius instead of one cylinder under floating armor.
    sections=[(.95,.145,.095),(1.00,.152,.102),(1.055,.144,.094),(1.11,.137,.083),(1.17,.150,.095),(1.24,.181,.108),(1.32,.207,.110),(1.39,.211,.099),(1.435,.173,.077),(1.47,.073,.052),(1.54,.057,.047)]
    from sculpt_forms import curve
    n=96;rows=100;vs=[];fs=[];weights=[]
    for j in range(rows):
        z=.95+.59*j/(rows-1);rx=curve([(z,x) for z,x,y in sections],z);ry=curve([(z,y) for z,x,y in sections],z)
        for i in range(n):
            a=2*pi*i/n;s=math.sin(a);c=math.cos(a)
            # Irregular compression folds at the waist/armpit, quiet taut chest.
            fold=.0028*math.sin(z*139+3*a+math.sin(a*3))*math.exp(-((z-1.09)/.095)**2)
            fold+=.002*math.sin(z*183+4*a)*math.exp(-((z-1.47)/.05)**2)
            seam=.0016*math.exp(-((abs(s)-.65)/.025)**2)
            vs.append(((rx+fold-seam)*s,.026-(ry+fold-seam)*c,z))
            if z<1.08:w={'Hips':max(0,(1.08-z)/.13),'Spine':min(1,(z-.95)/.13)}
            elif z<1.25:t=(z-1.08)/.17;w={'Spine':1-t,'Chest':t}
            elif z<1.41:w={'Chest':1}
            elif z<1.47:t=(z-1.41)/.06;w={'UpperChest':1-t,'Neck':t}
            else:w={'Neck':1}
            weights.append(w)
    for j in range(rows-1):
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    o=g.finish('VECTOR sewn torso fabric',vs,fs,g.cloth,'Spine',weights=weights)
    o.data.materials.append(g.trim)
    for f in o.data.polygons:
        x,y,z=f.center
        if abs(abs(x)-.085)<.0015 and z<1.2:f.material_index=0

    # The actual retained garment is the clearance surface, including sewn folds.
    cloth_vertices=[];cloth_faces=[]
    for garment in g.parts:
        offset=len(cloth_vertices);cloth_vertices.extend(v.co.copy() for v in garment.data.vertices)
        cloth_faces.extend(tuple(offset+i for i in p.vertices) for p in garment.data.polygons)
    clearance=BVHTree.FromPolygons(cloth_vertices,cloth_faces)
    def shell(name,outline,mapper,bone,mat=None):
        # Concentric quad topology follows each explicitly drawn silhouette.
        # Distinct edge land, chamfer and crowned face replace inflated cylinders.
        corners=[Vector(p) for p in outline];center=sum(corners,Vector((0,0)))/len(corners)
        points=[a.lerp(corners[(i+1)%len(corners)],j/6) for i,a in enumerate(corners) for j in range(6)]
        v=[];f=[];count=len(points)
        rings=[(0.01,0),(.20,0),(.40,0),(.62,0),(.82,0),(.94,0),(.975,-.001),(1,-.004)]
        for scale,inset in rings:
            for p in points:
                q=center+(p-center)*scale;v.append(mapper(q.x,q.y,inset))
        f.append(tuple(reversed(range(count))))
        for j in range(len(rings)-1):
            for i in range(count):a=j*count+i;b=j*count+(i+1)%count;f.append((a,b,b+count,a+count))
        rest=g.rig.data.bones[bone];axis=rest.tail_local-rest.head_local
        cleared=[]
        for point in v:
            point=Vector(point);t=max(0,min(1,(point-rest.head_local).dot(axis)/axis.length_squared));anchor=rest.head_local+axis*t
            ray=point-anchor
            hit,normal,index,distance=clearance.ray_cast(anchor,ray.normalized(),.30)
            if '--pose-base' not in sys.argv and hit is not None and distance+.006>ray.length:point=anchor+ray.normalized()*(distance+.006)
            cleared.append(point)
        v=cleared
        o=g.finish('VECTOR '+name,v,f,mat or g.paint,bone)
        # Orient an open patch outward before giving it inward shell thickness.
        center3=sum((Vector(p) for p in v),Vector())/len(v)
        rest=g.rig.data.bones[bone];axis=rest.tail_local-rest.head_local
        t=max(0,min(1,(center3-rest.head_local).dot(axis)/axis.length_squared))
        outward=center3-(rest.head_local+axis*t)
        normal=sum((p.normal*p.area for p in o.data.polygons),Vector())
        if normal.dot(outward)<0:o.data.flip_normals()
        o.modifiers.remove(o.modifiers['Skin']);bpy.context.view_layer.objects.active=o
        mod=o.modifiers.new('Shell inward thickness','SOLIDIFY');mod.thickness=.004;mod.offset=-1;bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Skin','ARMATURE');mod.object=g.rig
        o.data.materials.append(g.trim)
        # Manufactured dark edge belongs to the same surface and is flush.
        for p in o.data.polygons:
            if 1+(len(rings)-2)*count<=p.index<1+(len(rings)-1)*count:p.material_index=1
        return o
    front=lambda x,z,d:(x,-.127+2.4*x*x-.011*math.exp(-((z-1.34)/.08)**2)+d,z)
    # Drawn neckline, swept axilla edges, sternum V, floating lower costal plate.
    shell('pectoral shell',[(-.065,1.441),(.065,1.441),(.147,1.416),(.190,1.375),(.171,1.327),(.110,1.306),(.069,1.272),(-.069,1.272),(-.110,1.306),(-.171,1.327),(-.190,1.375),(-.147,1.416)],front,'Chest')
    shell('costal shell',[(-.174,1.317),(-.107,1.294),(-.071,1.260),(.071,1.260),(.107,1.294),(.174,1.317),(.145,1.252),(.091,1.182),(.062,1.142),(-.062,1.142),(-.091,1.182),(-.145,1.252)],lambda x,z,d:(x,-.114+3.0*x*x+d,z),'Spine')
    # Rear shell is designed around shoulder blades and a recessed spinal channel.
    for sign in [-1,1]:
        shell('scapular shell',[(sign*x,z) for x,z in [(.015,1.442),(.117,1.438),(.188,1.382),(.171,1.298),(.083,1.239),(.027,1.19),(.015,1.26)]],lambda x,z,d:(x,.147-.60*x*x-d,z),'Chest')
        shell('waist yoke',[(sign*x,z) for x,z in [(.07,1.065),(.15,1.073),(.172,1.042),(.159,1.009),(.103,1.016),(.086,1.037)]],lambda x,z,d:(x,-.067+.23*abs(x)+d,z),'Hips',g.metal)
        shell('clavicle yoke',[(sign*x,z) for x,z in [(.067,1.465),(.119,1.459),(.177,1.427),(.163,1.407),(.12,1.431),(.069,1.440)]],lambda x,z,d:(x,-.066+.10*abs(x)+d,z),'UpperChest',g.metal)
    # Wrapped armor uses explicit longitudinal/azimuthal silhouettes, rather than
    # one repeated primitive for all four anatomically different limb segments.
    outlines={
      'UpperArm':[(-1.6,-.05),(-.9,-.27),(.1,-.34),(1.1,-.25),(1.65,.05),(1.15,.36),(.65,.48),(.25,.70),(-.50,.74),(-1.02,.52)],
      'LowerArm':[(-1.05,.08),(-.40,.16),(.20,.08),(1.12,.17),(1.04,.72),(.42,.94),(-.30,.89),(-.92,.72)],
      'UpperLeg':[(-1.05,.04),(-.27,.12),(.35,.20),(1.08,.32),(1.10,.73),(.54,.94),(-.10,.85),(-.90,.63)],
      'LowerLeg':[(-1.0,.18),(-.45,.07),(.25,.11),(1.04,.22),(.93,.63),(.45,.94),(-.14,.97),(-.77,.82)]}
    if '--pose-base' in sys.argv:outlines['LowerLeg']=[(a,max(.30,t)) for a,t in outlines['LowerLeg']]
    profiles={
      'UpperArm':[(0,.080,.077),(.25,.076,.082),(.52,.067,.070),(.8,.049,.056),(1,.043,.046)],
      'LowerArm':[(0,.043,.047),(.24,.049,.054),(.55,.041,.047),(1,.030,.034)],
      'UpperLeg':[(0,.087,.093),(.2,.097,.101),(.5,.087,.090),(.8,.065,.068),(1,.052,.054)],
      'LowerLeg':[(0,.054,.060),(.23,.064,.077),(.45,.060,.073),(.7,.044,.055),(1,.035,.036)]}
    for side,sign in [('Left',1),('Right',-1)]:
        for region,outline in outlines.items():
            bone=side+region;a=g.rig.data.bones[bone].head_local.copy();b=g.rig.data.bones[side+'Hand'].head_local.copy() if region=='LowerArm' else g.rig.data.bones[bone].tail_local.copy()
            axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized();profile=profiles[region]
            def mapping(angle,t,d):
                rt=max(0,min(1,t));rx=curve([(t,x) for t,x,y in profile],rt);ry=curve([(t,y) for t,x,y in profile],rt)
                if region=='UpperArm' and t<.12:
                    dome=math.sqrt(max(.01,1-((t-.12)/.51)**2));rx*=dome;ry*=dome
                # Broad planar crest, flattened flank, tapered tendon side.
                theta=angle+(-.30*sign if region=='UpperLeg' else -.12*sign)
                ridge=.006*(1-min(1,abs(angle)/1.3))*math.sin(pi*rt)
                return a.lerp(b,t)+lateral*((rx+.007)*math.sin(theta))+front*((ry+.010)*math.cos(theta)+ridge+d)
            shell(side+region+' contoured shell',outline,mapping,bone)
            if region=='LowerLeg':
                shell(side+' knee shield',[(-.65,-.06),(.65,-.06),(.88,.07),(.55,.23),(0,.28),(-.65,.20)],mapping,bone,g.metal)
        foot=g.rig.data.bones[side+'Foot'];x=foot.head_local.x
        shell(side+' articulated instep',[(-.045,.17),(.045,.17),(.058,.09),(.047,.065),(-.047,.065),(-.058,.09)],lambda u,z,d:(x+u,-.02-(.17-z)*1.4-.008+d,z),side+'Foot')
    g.helmet()
    print('VECTOR_REMESHED_COSTUME',style)
