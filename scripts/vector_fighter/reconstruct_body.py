"""VECTOR B01: independently authored suit surfaces over the unchanged rig.

Dimensions are offline asset coordinates (metres, Z up, -Y forward). No old
body cages are reused. Head, glove mesh, equipment and their materials survive.
"""
import bpy,bmesh,math,sys,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import build as g
from military_f23_helmet import fingerprint
OUT=ROOT/'.local/vector-body-reconstruction';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'scripts/vector_fighter/f23-reconstructed.blend'
TARGET=ROOT/'scripts/vector_fighter/vector-body-b01.blend'
parts=[]


def material(name,color,metal,rough):
    m=bpy.data.materials.new(name);m.use_nodes=True
    nodes=m.node_tree.nodes;nodes.clear();bs=nodes.new('ShaderNodeBsdfPrincipled');bs.name='Principled BSDF'
    out=nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(bs.outputs[0],out.inputs['Surface'])
    bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
    return m


def activate(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o


def mesh(name,verts,faces,mat,bone,solid=0,bevel=0,smooth=True,weights=None):
    o=g.finish('VECTOR B01 '+name,verts,faces,mat,bone,weights=weights)
    o.modifiers.remove(o.modifiers['Skin']);activate(o)
    for p in o.data.polygons:p.use_smooth=smooth
    if solid:
        m=o.modifiers.new('Inward formed wall','SOLIDIFY');m.thickness=solid;m.offset=-1
        bpy.ops.object.modifier_apply(modifier=m.name)
    if bevel:
        m=o.modifiers.new('Machined radius','BEVEL');m.width=bevel;m.segments=3;m.limit_method='ANGLE';m.angle_limit=.5
        bpy.ops.object.modifier_apply(modifier=m.name)
        m=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');m.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=m.name)
    m=o.modifiers.new('Skin','ARMATURE');m.object=g.rig
    o['body_reconstruction']='B01';parts.append(o);return o


def grid(name,rows,mat,bone,closed=False,solid=0,bevel=0,smooth=True,weights=None):
    n=len(rows[0]);v=[p for row in rows for p in row];f=[]
    for j in range(len(rows)-1):
        for i in range(n if closed else n-1):
            k=(i+1)%n;f.append((j*n+i,j*n+k,(j+1)*n+k,(j+1)*n+i))
    if closed:f += [tuple(reversed(range(n))),tuple(range((len(rows)-1)*n,len(rows)*n))]
    ws=[w for w in weights for _ in range(n)] if weights else None
    return mesh(name,v,f,mat,bone,solid,bevel,smooth,ws)


def strip(name,points,width,mat,bone):
    # Narrow bands follow the actual surface instead of floating bolt clusters.
    return g.tube('VECTOR B01 '+name,points,width,mat,bone,radial=8)


def plate(name,points,normal,mat,bone,thick=.007):
    # Raised perimeter chamfer and recessed back create a closed manufactured solid.
    p=[Vector(x) for x in points];n=len(p);c=sum(p,Vector())/n;d=Vector(normal).normalized()
    v=p+[x.lerp(c,.07)+d*.002 for x in p]+[x-d*thick for x in p]
    f=[tuple(range(n,2*n)),tuple(reversed(range(2*n,3*n)))]
    for i in range(n):
        j=(i+1)%n;f.extend([(i,j,n+j,n+i),(i,2*n+i,2*n+j,j)])
    return mesh(name,v,f,mat,bone,bevel=.0007,smooth=False)


# Continuous tailored torso basis: independent chest, waist and pelvic sections.
TORSO=[(.81,.082,.064,.052),(.86,.135,.087,.087),(.94,.159,.092,.108),(1.01,.155,.089,.105),(1.075,.140,.090,.098),(1.14,.148,.103,.104),(1.23,.182,.119,.113),(1.32,.203,.130,.112),(1.39,.199,.115,.104),(1.435,.162,.095,.087),(1.47,.071,.063,.058)]
def torso_point(z,a,offset=0):
    for lo,hi in zip(TORSO,TORSO[1:]):
        if lo[0]<=z<=hi[0]:
            t=(z-lo[0])/(hi[0]-lo[0]);rx,rf,rb=[lo[i]*(1-t)+hi[i]*t for i in (1,2,3)];break
    else:rx,rf,rb=TORSO[-1][1:]
    sn,cs=math.sin(a),math.cos(a)
    x=(rx+offset)*math.copysign(abs(sn)**.87,sn)
    y=.032-(rf+offset if cs>=0 else rb+offset)*math.copysign(abs(cs)**.85,cs)
    return Vector((x,y,z))


def torso():
    rows=[];ws=[]
    for i in range(49):
        z=.815+i*(1.47-.815)/48
        rows.append([torso_point(z,j*math.tau/64,.0012*math.sin(z*270+j*.8)) for j in range(64)])
        if z<1.015:w={'Hips':1}
        elif z<1.18:t=(z-1.015)/.165;w={'Spine':1-t,'Chest':t}
        elif z<1.38:w={'Chest':1}
        else:t=min(1,(z-1.38)/.08);w={'Chest':1-t,'UpperChest':t}
        ws.append(w)
    grid('tailored torso textile',rows,cloth,'Spine',closed=True,weights=ws)
    # Close fitting torso armor from shaped angular sectors; same fitted surface
    # carries the front panel, outer return and rear flange.
    def shell(name,z0,z1,a0,a1,mat,bone,offset=.014,topcut=0,bottomcut=0):
        rows=[]
        for j in range(8):
            t=j/7;row=[]
            for i in range(17):
                u=i/16;a=a0+(a1-a0)*u
                z=(z0+bottomcut*abs(2*u-1))*(1-t)+(z1-topcut*abs(2*u-1))*t
                row.append(torso_point(z,a,offset+.004*math.sin(math.pi*t)*math.sin(math.pi*u)))
            rows.append(row)
        return grid(name,rows,mat,bone,solid=.007,bevel=.0012)
    for sign in [-1,1]:
        shell('pectoral wrap '+str(sign),1.255,1.423,sign*.14,sign*1.26,paint,'Chest',topcut=.025,bottomcut=.045)
        shell('scapula wrap '+str(sign),1.255,1.423,sign*1.85,sign*3.01,paint,'Chest',topcut=.038,bottomcut=.015)
        # Nested oblique lames overlap a dark backing without broad open gaps.
        for k in range(3):
            z=1.105+k*.052
            shell('oblique articulated lame %s %s'%(sign,k),z,z+.060,sign*.48,sign*1.65,steel,'Spine' if k<2 else 'Chest',offset=.012+k*.001,topcut=.009)
        # Shoulder yoke runs front-to-back over trapezius.
        x=.105*sign
        rows=[]
        for y,z,w in [(-.094,1.405,.024),(-.070,1.44,.029),(-.018,1.465,.033),(.045,1.46,.033),(.106,1.428,.029),(.133,1.393,.025)]:
            rows.append([(x+sign*u*w,y,z-.012*abs(u)) for u in [-1,-.7,0,.7,1]])
        grid('load yoke '+str(sign),rows,steel,'UpperChest',solid=.009,bevel=.0014)
        for y,z in [(-.078,1.441),(-.037,1.463)]:
            plate('yoke indicator',[(x-.013,y-.007,z),(x+.013,y-.007,z),(x+.013,y+.007,z+.003),(x-.013,y+.007,z+.003)],(0,-.2,1),status,'UpperChest',.002)
    # Raised collar with front throat relief and stepped laminated edges.
    for sign in [-1,1]:
        rows=[]
        for j in range(4):
            t=j/3;row=[]
            for i in range(17):
                a=sign*(.45+i*2.62/16);rx=.078+t*.004;ry=.069+t*.004
                row.append((rx*math.sin(a),.026-ry*math.cos(a),1.434+t*(.046+.012*(1-math.cos(a)))))
            rows.append(row)
        grid('raised collar '+str(sign),rows,steel,'UpperChest',solid=.006,bevel=.001)
        # Following grooves and corner facets on the newly formed chest surface.
        path=[(1.405,.30),(1.400,.58),(1.375,.92),(1.316,1.08),(1.295,.74)]
        strip('pectoral recessed boundary',[torso_point(z,sign*a,.020) for z,a in path],.0012,seal,'Chest')
        shell('pectoral upper laminate '+str(sign),1.355,1.420,sign*.28,sign*1.08,steel,'Chest',offset=.020,topcut=.020,bottomcut=.020)
        shell('pectoral corner facet '+str(sign),1.278,1.337,sign*.78,sign*1.23,steel,'Chest',offset=.022,topcut=.020,bottomcut=.017)
    # Recessed sternum and three broad overlapping abdominal shells.
    shell('sternum carrier',1.26,1.405,-.16,.16,steel,'Chest',offset=.015,topcut=.006)
    for k in range(3):
        shell('abdominal laminate '+str(k),1.035+k*.065,1.11+k*.065,-.52,.52,paint,'Spine',offset=.014,topcut=.009,bottomcut=.012)
    for k in range(4):
        shell('lumbar laminate '+str(k),1.055+k*.065,1.13+k*.065,2.55,3.73,steel,'Spine' if k<2 else 'Chest',offset=.014,topcut=.012)
    # Chest vent integrates a framed negative recess.
    for z in (1.356,1.384):
        plate('sternum vent', [(-.023,-.115,z),(.023,-.115,z),(.021,-.117,z+.009),(-.021,-.117,z+.009)],(0,-1,0),seal,'Chest',.003)
    rows=[]
    for z,r in [(1.443,.063),(1.46,.061),(1.485,.055),(1.51,.054),(1.54,.053),(1.566,.054)]:
        rows.append([(r*math.sin(a),.024-r*.9*math.cos(a),z) for a in [i*math.tau/48 for i in range(48)]])
    grid('neck gaiter',rows,cloth,'Neck',closed=True)
    for z in [1.47+i*.009 for i in range(9)]:
        strip('neck compression seam',[(.055*math.sin(a),.024-.051*math.cos(a),z) for a in [i*math.tau/48 for i in range(49)]],.001,seal,'Neck')
    shell('belt buckle inlay',.988,1.023,-.115,.115,bpy.data.materials['vector-fighter-identity-reference'],'Hips',offset=.044)
    shell('belt carrier',.968,1.03,-math.pi,math.pi,web,'Hips',offset=.018)
    for a in [-1.25,-.83,-.42,0,.42,.83,1.25,2.2,2.65,3.15,3.65,4.08]:
        shell('belt hard case',.965,1.038,a-.14,a+.14,steel,'Hips',offset=.035,topcut=.006,bottomcut=.003)
    for sign in [-1,1]:
        shell('hip socket '+str(sign),.866,.966,sign*.74,sign*1.72,steel,'Hips',offset=.016,topcut=.008,bottomcut=.023)
    shell('pelvic guard',.842,.961,-.42,.42,steel,'Hips',offset=.019,bottomcut=.025)


def frame(bone):
    b=g.rig.data.bones[bone];a=b.head_local.copy();end=g.rig.data.bones[bone.replace('LowerArm','Hand')].head_local if 'LowerArm' in bone else b.tail_local
    axis=(end-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();side=axis.cross(front).normalized()
    return a,end,front,side


def limb(side,region):
    bone=side+region;a,b,f,s=frame(bone)
    profile={
        'UpperArm':[(0,.066,.069),(.15,.078,.078),(.38,.074,.078),(.65,.063,.067),(1,.048,.050)],
        'LowerArm':[(0,.049,.052),(.20,.057,.061),(.47,.053,.058),(.74,.042,.045),(1,.033,.036)],
        'UpperLeg':[(0,.090,.094),(.18,.101,.113),(.42,.094,.102),(.7,.080,.087),(1,.061,.066)],
        'LowerLeg':[(0,.061,.065),(.2,.074,.084),(.42,.073,.080),(.65,.057,.066),(1,.041,.046)]}[region]
    def dims(t):
        t=max(0,min(1,t))
        for lo,hi in zip(profile,profile[1:]):
            if lo[0]<=t<=hi[0]:
                u=(t-lo[0])/(hi[0]-lo[0]);return [lo[i]*(1-u)+hi[i]*u for i in (1,2)]
        return profile[-1][1:]
    def pos(t,angle,off=0):
        rx,ry=dims(t);cs,sn=math.cos(angle),math.sin(angle)
        # Quadriceps/calf/deltoid convexity and flattened inner articulation.
        return a.lerp(b,t)+s*((rx+off)*math.copysign(abs(sn)**.92,sn))+f*((ry+off)*math.copysign(abs(cs)**.85,cs))
    rows=[]
    for j in range(33):
        t=-.055+1.11*j/32
        rows.append([pos(t,i*math.tau/48,.0015*math.sin(t*80+i*.75)*(abs(t-.5)*1.6)) for i in range(48)])
    grid(bone+' anatomical textile',rows,cloth,bone,closed=True)
    def armor(name,t0,t1,a0,a1,material,offset=.009,tip=.045):
        rows=[]
        for j in range(10):
            v=j/9;row=[]
            for i in range(17):
                u=i/16;ang=a0+(a1-a0)*u
                start=t0+tip*abs(2*u-1);end=t1-tip*abs(2*u-1)
                row.append(pos(start*(1-v)+end*v,ang,offset+.003*math.sin(v*math.pi)))
            rows.append(row)
        o=grid(bone+' '+name,rows,material,bone,solid=.006,bevel=.001)
        return o
    if region=='UpperArm':
        # A true enveloping shoulder cup: dome sections widen over deltoid and
        # turn under it, with a separate articulated lower arm guard.
        armor('deltoid cup',-.16,.43,-1.55,1.55,paint,.017,.10)
        armor('posterior deltoid return',-.10,.37,1.60,4.68,steel,.013,.08)
        armor('biceps shroud',.43,.85,-1.22,1.22,steel,.010,.06)
        armor('triceps shroud',.42,.83,1.50,4.78,steel,.009,.06)
    elif region=='LowerArm':
        armor('vambrace outer',.16,.91,-1.45,.60,paint,.012,.065)
        armor('vambrace inner',.20,.89,.67,2.65,steel,.009,.045)
        armor('vambrace rear',.20,.88,2.72,4.76,steel,.009,.045)
        for t in [.31,.72]:
            strip('forearm retaining band',[pos(t,x,.019) for x in [i*math.tau/48 for i in range(49)]],.003,bpy.data.materials['vector-reference-edge-metal'],bone)
        strip('forearm light gasket',[pos(t,-.6,.019) for t in [.35,.4,.68,.73]],.005,seal,bone)
        strip('forearm light',[pos(t,-.6,.023) for t in [.39,.68]],.002,status,bone)
    elif region=='UpperLeg':
        armor('quadriceps shell',.11,.88,-1.15,1.10,paint,.013,.095)
        armor('outer thigh return',.16,.84,1.18,2.55,steel,.010,.07)
        armor('hamstring shell',.23,.80,2.63,4.68,steel,.009,.10)
        # Additional facing is a fitted inset, never a floating flat diamond.
        armor('quadriceps inner inset',.28,.74,-.65,.65,steel,.021,.06)
    else:
        armor('continuous greave',.22,.95,-1.30,1.30,paint,.010,.04)
        armor('calf protection',.23,.89,1.37,4.91,steel,.008,.07)
        armor('patella cup',-.08,.23,-1.18,1.18,steel,.016,.07)
        strip('shin light gasket',[pos(t,.85,.018) for t in [.43,.66]],.0045,seal,bone)
        strip('shin light',[pos(t,.85,.022) for t in [.45,.64]],.002,status,bone)
    # Small formed overlapping edge facets break the broad shell into purposeful
    # manufacturing surfaces; they follow the same new envelope.
    if region=='UpperArm':
        armor('shoulder crown facet',-.09,.24,-1.1,1.08,steel,.024,.075)
        armor('shoulder lower overlap',.25,.46,-.86,.9,bpy.data.materials['vector-reference-coated-steel'],.023,.035)
    elif region=='LowerLeg':
        armor('greave outer facet',.34,.87,.88,1.32,steel,.016,.04)
    # Thin collars and textile articulation channels read as a connected suit.
    for t in [.02,.055,.09,.95,.98]:
        strip(bone+' articulation rib',[pos(t,x,.002) for x in [i*math.tau/40 for i in range(41)]],.0015,bpy.data.materials['vector-reference-woven-fabric'],bone)
    # Curved panel edge seam: material line integrated with the surface.
    strip(bone+' longitudinal seam',[pos(t,.92,.018 if region=='UpperLeg' else .014) for t in [.30,.4,.55,.7,.8]],.001,seal,bone)


def boots(side):
    bone=side+'Foot';x=g.rig.data.bones[bone].head_local.x
    # Rounded asymmetric sole outline has a narrowed heel, full forefoot and toe.
    outline=[(-.052,.106),(.052,.106),(.064,.076),(.060,.005),(.073,-.105),(.064,-.185),(.045,-.217),(-.045,-.217),(-.069,-.184),(-.077,-.107),(-.061,.005),(-.065,.078)]
    def rings(name,levels,material):
        rows=[]
        for z,scale,cy in levels:rows.append([(x+u*scale,cy+(v+.04)*scale,z) for u,v in outline])
        return grid(side+' '+name,rows,material,bone,closed=True,bevel=.002,smooth=False)
    rings('traction sole',[(.009,1,-.04),(.022,1.015,-.04),(.044,1.0,-.04),(.052,.95,-.04)],seal)
    rings('moulded midsole',[(.048,.97,-.04),(.066,.97,-.04),(.079,.87,-.035)],steel)
    # Boot upper is height-field lofted around the foot, not stacked open plates.
    rows=[]
    for y,width,height in [(-.207,.041,.09),(-.186,.059,.099),(-.13,.064,.108),(-.064,.056,.126),(-.005,.047,.160),(.065,.047,.174),(.097,.044,.132)]:
        row=[]
        for i in range(24):
            a=math.tau*i/24;cs,sn=math.cos(a),math.sin(a)
            row.append((x+width*sn,y,.067+(height-.067)*max(0,cs)**.7))
        rows.append(row)
    grid(side+' stitched boot upper',rows,cloth,bone,closed=True,bevel=.0015)
    # Toe cap and instep bridge wrap down to the welt.
    for k,(y0,y1,z) in enumerate([(-.198,-.126,.10),(-.122,-.056,.121),(-.052,.015,.152)]):
        rows=[]
        for y in [y0,y0+.007,y1-.007,y1]:
            rows.append([(x+u*.061,y,z-.044*abs(u)**1.7) for u in [-1,-.85,-.5,0,.5,.85,1]])
        grid(side+' boot instep shell '+str(k),rows,steel,bone,solid=.008,bevel=.0015)
    for sign in [-1,1]:
        plate(side+' ankle counter',[(x+sign*.051,.075,.070),(x+sign*.048,.079,.163),(x+sign*.050,.035,.193),(x+sign*.057,-.012,.134),(x+sign*.061,-.023,.071)],(sign,0,0),steel,bone,.006)
    # Sparse physical lugs create readable tread and heel/toe notches.
    for sign in [-1,1]:
        for y in [-.165,-.113,-.061,.014,.071]:
            plate(side+' sole lug',[(x+sign*.063,y-.014,.012),(x+sign*.066,y-.014,.038),(x+sign*.066,y+.014,.038),(x+sign*.063,y+.014,.012)],(sign,0,0),seal,bone,.007)


def main():
    global paint,steel,cloth,seal,web,status
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE));g.rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    g.rig.data.pose_position='REST';g.parts=[]
    kept={};removed=[]
    for o in list(bpy.context.scene.objects):
        if o.type!='MESH':continue
        if o.name=='weapon:joined' or 'restored Orbital gloves' in o.name or (len(o.vertex_groups)==1 and o.vertex_groups[0].name=='Head'):
            kept[o.name]=fingerprint(o)
        else:removed.append(o.name);bpy.data.objects.remove(o,do_unlink=True)
    paint=material('vector-fighter-identity-body-b01',(.14,.14,.14),.18,.63)
    steel=material('vector-body-b01-graphite',(.065,.073,.078),.3,.62)
    cloth=material('vector-body-b01-textile',(.022,.027,.029),0,.89)
    seal=bpy.data.materials['vector-reference-flexible-seals'];web=bpy.data.materials['vector-reference-webbing'];status=bpy.data.materials['identity-vector-status']
    torso()
    for side in ['Left','Right']:
        for region in ['UpperArm','LowerArm','UpperLeg','LowerLeg']:limb(side,region)
        boots(side)
    assert all(fingerprint(bpy.data.objects[n])==v for n,v in kept.items()),'Preserved source objects'
    for o in g.parts:o['body_reconstruction']='B01'
    bpy.context.preferences.filepaths.save_version=0;g.rig.data.pose_position='POSE'
    bpy.ops.wm.save_as_mainfile(filepath=str(TARGET),compress=True)
    (OUT/'authoring.json').write_text(json.dumps({'source':str(SOURCE),'target':str(TARGET),'removed_old_body_objects':removed,'preserved_objects':kept,'new_objects':len(g.parts),'method':'new tailored anatomical cross sections, wraparound armor returns, domed shoulder cups, integrated boot uppers; no old body mesh reused'},indent=2))
    print('BODY B01',len(g.parts),'new objects;',len(kept),'exact preserved')
if __name__=='__main__':main()
