"""Primary-form study for VECTOR. New designed surfaces, not detail attachments.
The continuous garment is welded in object space, then receives the preserved rig.
Armor uses anatomical cross-sections, shaped cut lines and recessed surface relief.
Preview only: build.py --candidate --forms sculpted (or planar).
"""
import bpy, math
from mathutils import Vector
from mathutils.kdtree import KDTree


def curve(knots,t):
    # Shape-preserving cubic interpolation through authored section coordinates.
    for i in range(len(knots)-1):
        if t <= knots[i+1][0]: break
    x0,y0=knots[i];x1,y1=knots[i+1];h=x1-x0;u=max(0,min(1,(t-x0)/h))
    a=(y1-y0)/h
    left=(y0-knots[i-1][1])/(x0-knots[i-1][0]) if i else a
    right=(knots[i+2][1]-y1)/(knots[i+2][0]-x1) if i+2<len(knots) else a
    m0=0 if left*a<=0 else 2*left*a/(left+a)
    m1=0 if right*a<=0 else 2*right*a/(right+a)
    return (2*u**3-3*u**2+1)*y0+(u**3-2*u**2+u)*h*m0+(-2*u**3+3*u**2)*y1+(u**3-u**2)*h*m1


def build(g,style):
    rig=g.rig; finish=g.finish; parts=g.parts; pi=math.pi; tau=math.tau
    planar=style=='planar'
    # Reconstructed garment cross-sections: pelvis, obliques, rib cage, traps.
    profiles=[(.80,.075,.065),(.85,.145,.094),(.90,.178,.105),(.97,.161,.105),(1.04,.148,.092),(1.12,.137,.082),(1.20,.160,.102),(1.30,.196,.111),(1.38,.213,.104),(1.43,.184,.083),(1.48,.058,.052),(1.54,.053,.048)]
    verts=[];faces=[];weights=[];N=48;M=55
    for j in range(M):
        z=.80+(.74*j/(M-1));rx=curve([(a,b) for a,b,c in profiles],z);ry=curve([(a,c) for a,b,c in profiles],z)
        if z<1.04: ws={'Hips':1}
        elif z<1.22:
            w=(z-1.04)/.18;ws={'Spine':1-w,'Chest':w}
        elif z<1.40:ws={'Chest':1}
        elif z<1.46: w=(z-1.40)/.06;ws={'Chest':1-w,'UpperChest':w}
        else: ws={'Neck':1}
        for i in range(N):
            a=tau*i/N;x=rx*math.sin(a);y=.032-ry*math.cos(a)
            # Garment sits over gluteal and scapular volumes; centerline is restrained.
            y+=.018*max(0,-math.cos(a))*math.exp(-((z-.94)/.08)**2)*math.sin(a)**2
            y-=.008*max(0,math.cos(a))*math.exp(-((z-1.30)/.08)**2)*math.sin(2*a)**2
            verts.append((x,y,z));weights.append(ws)
    for j in range(M-1):
        for i in range(N):a=j*N+i;b=j*N+(i+1)%N;faces.append((a,b,b+N,a+N))
    faces += [tuple(reversed(range(N))),tuple(range((M-1)*N,M*N))]
    clothparts=[finish('VECTOR anatomical torso fabric',verts,faces,g.cloth,'Spine',weights=weights)]
    # Continuous shoulder-to-wrist / hip-to-ankle garment sections. Different
    # radial profiles for biceps, triceps, brachioradialis, quad and calf masses.
    dimensions={
        'UpperArm':[(0,.073,.071),(.15,.079,.074),(.38,.066,.075),(.65,.057,.061),(.87,.043,.045),(1,.040,.044)],
        'LowerArm':[(0,.040,.044),(.18,.045,.050),(.40,.043,.048),(.67,.035,.039),(1,.029,.031)],
        'UpperLeg':[(0,.085,.089),(.20,.096,.103),(.40,.087,.098),(.65,.074,.086),(.86,.059,.064),(1,.051,.055)],
        'LowerLeg':[(0,.051,.055),(.20,.062,.070),(.38,.061,.077),(.61,.048,.060),(.83,.036,.043),(1,.033,.036)]}
    frames={}
    for side,sign in [('Left',1),('Right',-1)]:
        for region in dimensions:
            name=side+region;b=rig.data.bones[name];a=b.head_local.copy();end=rig.data.bones[side+'Hand'].head_local.copy() if region=='LowerArm' else b.tail_local.copy()
            axis=(end-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized();frames[name]=(a,end,front,lateral)
        for kind in ['Arm','Leg']:
            v=[];f=[];ws=[];rings=[]
            for region in ['Upper'+kind,'Lower'+kind]:
                name=side+region;a,b,front,lat=frames[name];p=dimensions[region]
                for j in range(25):
                    if region.startswith('Lower') and j==0:continue
                    t=(-.28+1.28*j/24) if region=='UpperLeg' else j/24;center=a.lerp(b,t);wx=curve([(q,x) for q,x,y in p],t);dy=curve([(q,y) for q,x,y in p],t)
                    if region=='UpperLeg' and t<0:
                        taper=.30+.70*(t+.28)/.28;wx*=taper;dy*=taper
                    if region=='UpperLeg' and t<.18:w=max(0,min(1,(t+.28)/.46));weight={'Hips':1-w,name:w}
                    elif region.startswith('Upper') and t>.84:w=(t-.84)/.16*.5;weight={name:1-w,side+'Lower'+kind:w}
                    elif region.startswith('Lower') and t<.16:w=.5+.5*t/.16;weight={side+'Upper'+kind:1-w,name:w}
                    else:weight={name:1}
                    for i in range(N):
                        ang=tau*i/N;c=math.cos(ang);s=math.sin(ang)
                        # Shifting muscle belly and tendon plane avoid circular tubes.
                        belly=math.sin(pi*t)
                        d=dy*(1+.055*math.cos(2*ang+.5)*belly)
                        center_offset=front*(.004*belly if kind=='Arm' else -.005*belly)
                        v.append(center+center_offset+lat*(wx*s)+front*(d*c));ws.append(weight)
                    rings.append(center)
            for j in range(len(rings)-1):
                for i in range(N):a=j*N+i;b=j*N+(i+1)%N;f.append((a,b,b+N,a+N))
            f += [tuple(reversed(range(N))),tuple(range((len(rings)-1)*N,len(rings)*N))]
            clothparts.append(finish('VECTOR continuous '+side+kind+' fabric',v,f,g.cloth,side+'Upper'+kind,weights=ws))
    # Fuse cloth intersections only. Nearest authored source weights are restored
    # after remeshing; no disconnected shoulder/hip balls survive in the mesh.
    samples=[]
    for o in clothparts:
        groups={vg.index:vg.name for vg in o.vertex_groups}
        for v in o.data.vertices:samples.append((v.co.copy(),{groups[w.group]:w.weight for w in v.groups if w.weight>0}))
        for m in list(o.modifiers):o.modifiers.remove(m)
    kd=KDTree(len(samples))
    for i,(co,ws) in enumerate(samples):kd.insert(co,i)
    kd.balance()
    bpy.ops.object.select_all(action='DESELECT')
    for o in clothparts:o.select_set(True)
    garment=clothparts[0];bpy.context.view_layer.objects.active=garment;bpy.ops.object.join()
    for o in clothparts:
        if o in parts:parts.remove(o)
    garment.name='VECTOR continuous garment'
    mod=garment.modifiers.new('Weld anatomical garment','REMESH');mod.mode='VOXEL';mod.voxel_size=.004;mod.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=garment.modifiers.new('Relax garment transitions','SMOOTH');mod.factor=.65;mod.iterations=5;bpy.ops.object.modifier_apply(modifier=mod.name)
    garment.vertex_groups.clear();names=sorted({k for co,ws in samples for k in ws});groups={k:garment.vertex_groups.new(name=k) for k in names}
    finalws=[]
    for v in garment.data.vertices:
        w={};near=kd.find_n(v.co,4)
        for co,idx,d in near:
            for k,a in samples[idx][1].items():w[k]=w.get(k,0)+a/(d+.001)**2
        total=sum(w.values());w={k:a/total for k,a in w.items()};finalws.append(w)
        for k,a in w.items():groups[k].add([v.index],a,'REPLACE')
    # Split only the render batches along existing face edges. Shared boundary
    # positions and skin weights are identical in world/FPS outputs.
    armfaces=[];bodyfaces=[]
    for p in garment.data.polygons:
        aw=sum(sum(w for k,w in finalws[i].items() if 'Arm' in k or 'Hand' in k) for i in p.vertices)/len(p.vertices)
        (armfaces if aw>.5 else bodyfaces).append(tuple(p.vertices))
    for fs,name in [(armfaces,'VECTOR continuous UpperArm fabric'),(bodyfaces,'VECTOR continuous body fabric')]:
        ids=sorted({i for f in fs for i in f});remap={v:i for i,v in enumerate(ids)}
        finish(name,[garment.data.vertices[i].co.copy() for i in ids],[tuple(remap[i] for i in f) for f in fs],g.cloth,'Spine',weights=[finalws[i] for i in ids])
    bpy.data.objects.remove(garment,do_unlink=True)

    if style=='authored-cloth':
        import bmesh
        for o in list(parts):
            if 'fabric' in o.name:
                parts.remove(o);bpy.data.objects.remove(o,do_unlink=True)
        for o in g.source_garment:
            bpy.context.collection.objects.link(o)
            # Keep the original sewn garment, not pouches or head equipment.
            groups={v.index:v.name for v in o.vertex_groups}
            keep={v.index for v in o.data.vertices if sum(w.weight for w in v.groups if any(groups[w.group].startswith(side+part) for side in ['Left','Right'] for part in ['Hand','Thumb','Index','Middle','Ring','Little']))<=.10}
            bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index not in keep],context='VERTS');bm.to_mesh(o.data);bm.free()
            if not o.data.polygons:
                bpy.data.objects.remove(o,do_unlink=True);continue
            # Original garment detail retained; no arbitrary smoothing/subdivision.
            for m in o.modifiers:
                if m.type=='ARMATURE':m.object=rig
            o.name='VECTOR authored garment '+o.name
            # Separate renderer batches by existing skin ownership, with the same
            # mesh vertices on the boundary (the body itself is not reshaped here).
            for mat in o.data.materials:
                if not mat or not mat.use_nodes:continue
                shader=mat.node_tree.nodes.get('Principled BSDF')
                if shader:
                    shader.inputs['Roughness'].default_value=1;shader.inputs['Metallic'].default_value=0
            parts.append(o)

    def surface(name,v,f,bone,ws=None,metal_edge=False):
        o=finish(name,v,f,g.paint,bone,weights=ws)
        if metal_edge:
            o.data.materials.append(g.trim)
        bpy.context.view_layer.objects.active=o
        o.modifiers.remove(o.modifiers['Skin'])
        m=o.modifiers.new('Authored shell thickness','SOLIDIFY');m.thickness=.004;m.offset=-1;bpy.ops.object.modifier_apply(modifier=m.name)
        m=o.modifiers.new('Manufactured edge radius','BEVEL');m.width=.0012;m.segments=2;bpy.ops.object.modifier_apply(modifier=m.name)
        m=o.modifiers.new('Skin','ARMATURE');m.object=rig
        return o

    # One wraparound cuirass front, carrying chest-to-sternum-to-abdomen flow.
    # Material cut-lines are incised into this surface instead of floating tubes.
    profile=[(1.145,.059,.081),(1.19,.084,.095),(1.25,.127,.115),(1.31,.177,.128),(1.36,.193,.123),(1.40,.183,.103),(1.44,.122,.079)]
    for back in [False,True]:
        v=[];f=[];ws=[];N=57;M=53
        for j in range(M):
            t=j/(M-1);z=1.145+.305*t;rx=curve([(a,b) for a,b,c in profiles],z)+.009;depth=curve([(a,c) for a,b,c in profiles],z)+.017
            for i in range(N):
                u=2*i/(N-1)-1;span=curve([(0,.54),(.35,1.12),(.70,1.34),(1,1.10)],t);ang=u*span;x=rx*math.sin(ang)
                # Side termination follows the ribs; sculpted pectoral relief
                # merges into the sternum. No separate chest/abdominal slabs.
                zz=z-.006*(1-abs(u))*t**10+.032*abs(u)**2*(1-t)**6
                section=math.cos(ang)**(.38 if planar else .68)
                bulge=.001*math.exp(-((z-1.347)/.058)**2)*math.sin(pi*abs(u))**2
                keel=.008*math.exp(-(u/.20)**2)*math.exp(-((z-1.26)/.10)**2)
                seamz=1.291+.040*abs(u)**.7
                groove=.003*math.exp(-((z-seamz)/.0045)**2)
                groove+=.0013*math.exp(-((abs(u)-(.71+.05*math.sin(t*pi)))/.018)**2)*math.sin(pi*t)**2
                d=depth*section+bulge+keel-groove
                if back:d=depth*section+.008*math.exp(-((z-1.34)/.08)**2)*math.sin(pi*abs(u))**2
                y=.032+(d if back else -d)
                v.append((x,y,zz));w=max(0,min(1,(z-1.16)/.12));ws.append({'Spine':1-w,'Chest':w})
        for j in range(M-1):
            for i in range(N-1):q=j*N+i;f.append((q,q+1,q+N+1,q+N))
        o=surface('VECTOR sculpted '+('dorsal' if back else 'cuirass'),v,f,'Chest',ws)
        o.data.materials.append(g.trim)
        if not back:
            for poly in o.data.polygons:
                x,y,z=poly.center;radius=curve([(a,b) for a,b,c in profiles],z)+.009;u=math.asin(max(-1,min(1,x/radius)))/1.2;seamz=1.291+.040*abs(u)**.7
                if abs(z-seamz)<.0017:poly.material_index=1

    # Four independently designed limb shell silhouettes, following local anatomy.
    ranges={'UpperArm':(-.26,.72),'LowerArm':(.07,.92),'UpperLeg':(.12,.91),'LowerLeg':(-.06,.94)}
    for side,sign in [('Left',1),('Right',-1)]:
        for region in dimensions:
            name=side+region;a,b,front,lat=frames[name];pr=dimensions[region];start,end=ranges[region]
            v=[];f=[];N=39;M=41
            for j in range(M):
                t=j/(M-1)
                for i in range(N):
                    u=2*i/(N-1)-1
                    # Shoulder shell wraps deltoid and runs into upper-arm shape;
                    # knee is part of shin silhouette, with asymmetric V cut lines.
                    if region=='UpperArm':
                        lo=-.18+.15*abs(u)**1.6;hi=end-.15*abs(u)**1.3;span=1.63-.26*t;offset=-.18*sign
                    elif region=='LowerArm':
                        lo=start+.12*(1-abs(u));hi=end-.12*abs(u)**1.4;span=1.32-.22*t;offset=.12*sign
                    elif region=='UpperLeg':
                        lo=start+.15*(1-u*u);hi=end-.22*abs(u)**1.4;span=1.32-.16*t;offset=-.32*sign
                    else:
                        lo=start+.14*abs(u)**1.3;hi=end-.10*abs(u);span=1.31-.26*t;offset=.05*sign
                    z=lo+(hi-lo)*t;ang=u*span+offset;rz=max(0,min(1,z));wx=curve([(q,x) for q,x,y in pr],rz);dy=curve([(q,y) for q,x,y in pr],rz)
                    if region=='UpperArm' and z<.07:
                        dome=math.sqrt(max(.06,1-((z-.08)/.27)**2));wx*=dome;dy*=dome
                    # Long anatomical ridges change normals over a broad surface,
                    # with a shallow seam carved directly along the contour.
                    ridge=(.006 if planar else .004)*math.exp(-((u+.15*sign)/.26)**2)*math.sin(pi*t)**.6
                    groove=.0018*math.exp(-((u-(.62-.15*t))/.025)**2)*math.sin(pi*t)**.4
                    y=(dy+.013)*math.cos(ang)+ridge-groove+.004*math.sin(pi*rz)
                    if region=='LowerLeg':y+=.013*math.exp(-((z-.03)/.12)**2)*(1-u*u)
                    if planar:y=.65*y+.35*(dy+.013)*(1-.43*abs(u))
                    p=a.lerp(b,z)+lat*((wx+.012)*math.sin(ang))+front*y
                    v.append(p)
            for j in range(M-1):
                for i in range(N-1):q=j*N+i;f.append((q,q+1,q+N+1,q+N))
            surface('VECTOR '+name+' anatomical ceramic',v,f,name)
        # Shaped boot sections, blended instep and toe roll. No cuboid sole/heel.
        foot=rig.data.bones[side+'Foot'];x=foot.head_local.x
        v=[];f=[];N=48
        boot=[(.023,-.069,.065,.137),(.035,-.071,.069,.142),(.055,-.072,.066,.140),(.076,-.067,.061,.128),(.103,-.047,.054,.104),(.133,-.014,.046,.069),(.170,.012,.037,.043)]
        for z,y,rx,ry in boot:
            for i in range(N):
                ang=tau*i/N;v.append((x+rx*math.sin(ang),y-ry*math.cos(ang),z+.009*max(0,math.cos(ang))**6 if z<.07 else z))
        for j in range(len(boot)-1):
            for i in range(N):q=j*N+i;r=j*N+(i+1)%N;f.append((q,r,r+N,q+N))
        f += [tuple(reversed(range(N))),tuple(range((len(boot)-1)*N,len(boot)*N))]
        o=finish('VECTOR '+side+' sculpted boot',v,f,g.rubber,side+'Foot')
        # Color follows upper mesh faces, not added armor blocks.
        o.data.materials.append(g.paint)
        for p in o.data.polygons:
            if p.center.z>.075 and p.center.y<.02:p.material_index=1
    # Preserve the compatible helmet for this body-form study. Its overhaul is
    # assessed separately so the comparison isolates the rejected body language.
    g.helmet()
    for o in parts:
        if len(o.vertex_groups)==1 and o.vertex_groups[0].name=='Head':
            for v in o.data.vertices:v.co.x*=1.04;v.co.z-=.012
    print('VECTOR_PRIMARY_FORM_STUDY',style)
