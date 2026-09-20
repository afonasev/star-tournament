"""Offline measured anatomy and topology-aware skinning for ART_LOLL source SHA.
Coordinates/tolerances describe this source mesh, not gameplay tunables.
"""
import math
import numpy as np
from mathutils import Vector,Matrix,Quaternion

# u follows extended digits, v spans the palm, w points out of the glove dorsum.
# Values measured from the unmodified left glove; right glove is mirrored.
DIGITS={
 'Thumb':[(.390,.025,.469),(.424,-.002,.466),(.454,-.020,.468),(.487,-.025,.469)],
 'Index':[(.469,.032,.477),(.512,.028,.478),(.539,.025,.479),(.563,.023,.480)],
 'Middle':[(.473,.059,.477),(.519,.059,.477),(.549,.060,.477),(.569,.059,.477)],
 'Ring':[(.468,.093,.473),(.510,.093,.475),(.540,.094,.476),(.563,.094,.476)],
 'Little':[(.460,.119,.466),(.496,.122,.467),(.521,.125,.469),(.539,.125,.469)],
}
SEGMENTS=('Proximal','Intermediate','Distal')
THUMB_OPPOSITION_DEGREES=30 # Offline authored grasp pose, not a gameplay parameter.
def xyz(uvw,sign=1):
    u,v,w=uvw
    return (sign*(u+w)/math.sqrt(2),v,(w-u)/math.sqrt(2))
def uvw(p):return Vector(((abs(p.x)-p.z)/math.sqrt(2),p.y,(abs(p.x)+p.z)/math.sqrt(2)))
def finger_specs():
    result=[]
    for side,sign in [('Left',1),('Right',-1)]:
        for digit,points in DIGITS.items():
            for i,segment in enumerate(SEGMENTS):
                name=side+digit+segment
                result.append((name,xyz(points[i],sign),xyz(points[i+1],sign),side+'Hand' if i==0 else side+digit+SEGMENTS[i-1]))
    return result

def smooth(x,a,b):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def mix(a,b,t):
    out={k:v*(1-t) for k,v in a.items()}
    for k,v in b.items():out[k]=out.get(k,0)+v*t
    return {k:v for k,v in out.items() if v>1e-7}


def harmonic_pelvis(o,island):
    """Dirichlet solve for a connected waist/thigh cloth component, no cuts.
    Waist is Hips; distal ends are L/R thigh. Interior weights use edge-length
    weighted graph Laplacian, keeping both sides of the crotch continuous.
    """
    ids=set(island);adj={i:{} for i in island};v=o.data.vertices
    for e in o.data.edges:
        a,b=e.vertices
        if a in ids and b in ids:
            conductance=1/max((v[a].co-v[b].co).length,1e-6)
            adj[a][b]=conductance;adj[b][a]=conductance
    boundary={}
    for i in island:
        p=v[i].co
        if p.z>-.015:boundary[i]=np.array([1.,0.,0.])
        elif p.z<-.28 and abs(p.x)>.065:boundary[i]=np.array([0.,float(p.x>0),float(p.x<0)])
    assert any(w[1] for w in boundary.values()) and any(w[2] for w in boundary.values())
    interior=[i for i in island if i not in boundary];rows={i:k for k,i in enumerate(interior)}
    A=np.zeros((len(interior),len(interior)));B=np.zeros((len(interior),3))
    for i,k in rows.items():
        for j,c in adj[i].items():
            A[k,k]+=c
            if j in rows:A[k,rows[j]]-=c
            else:B[k]+=c*boundary[j]
    result=np.linalg.solve(A,B)
    assert np.max(np.abs(A@result-B))<1e-7
    names=['Hips','LeftUpperLeg','RightUpperLeg'];out={}
    for i in island:
        w=boundary[i] if i in boundary else result[rows[i]]
        w=np.clip(w,0,1);w/=w.sum();out[i]={n:float(x) for n,x in zip(names,w) if x>1e-7}
    return out

def orient_combat_hand(rig,side):
    sign=1 if side=='Left' else -1
    u=Vector((sign,0,-1)).normalized();v=Vector((0,1,0));w=Vector((sign,0,1)).normalized()
    source=Matrix((u,v,w)).transposed()
    # Support palm faces up, trigger-hand palm faces inward. Finger rows stay
    # perpendicular to the matching local QA cylinder axis.
    if side=='Left':tu,tv,tw=Vector((0,-1,0)),Vector((-1,0,0)),Vector((0,0,-1))
    else:tu,tv,tw=Vector((0,-1,0)),Vector((0,0,-1)),Vector((-1,0,0))
    target=Matrix((tu,tv,tw)).transposed();rotation=target@source.inverted()
    pb=rig.pose.bones[side+'Hand'];q=rotation.to_quaternion()@rig.data.bones[pb.name].matrix_local.to_quaternion()
    pb.matrix=Matrix.LocRotScale(pb.head.copy(),q,Vector((1,1,1)))

def pose_digits(rig,kind,t):
    for side,sign in [('Left',1),('Right',-1)]:
        for digit in DIGITS:
            angles=(8,12,8)
            if kind=='run':angles=(18,25,18)
            if kind in ('aim','fire'):
                angles=GRIP_ANGLES[digit]
                if digit=='Index' and side=='Right':
                    pulse=math.exp(-((t-.22)/.10)**2) if kind=='fire' else 0
                    angles=(8+10*pulse,12+12*pulse,8+6*pulse)
            for segment,angle in zip(SEGMENTS,angles):
                name=side+digit+segment;bone=rig.data.bones[name]
                axis=bone.matrix_local.to_3x3().inverted()@Vector((0,sign,0))
                q=Quaternion(axis,math.radians(angle))
                if digit=='Thumb' and segment=='Proximal' and kind in ('aim','fire'):
                    opposition=bone.matrix_local.to_3x3().inverted()@Vector((1,0,sign)).normalized()
                    q=Quaternion(opposition,math.radians(THUMB_OPPOSITION_DEGREES))@q
                rig.pose.bones[name].rotation_quaternion=q

def harmonic_glove(o,island,base_weights):
    """Use disconnected fingertip branches as Dirichlet boundaries, then solve
    across palm/web topology. Avoid nearest-finger discontinuities on tiny seams.
    """
    vertices=o.data.vertices;coords={i:uvw(vertices[i].co) for i in island}
    ids=set(island);adj={i:{} for i in island}
    for e in o.data.edges:
        a,b=e.vertices
        if a in ids and b in ids:
            c=1/max((vertices[a].co-vertices[b].co).length,1e-6);adj[a][b]=c;adj[b][a]=c
    active={i for i in island if coords[i].x>.485};labels={}
    while active:
        i=active.pop();part=[i];stack=[i]
        while stack:
            a=stack.pop()
            for b in adj[a]:
                if b in active:active.remove(b);part.append(b);stack.append(b)
        mean_v=sum(coords[i].y for i in part)/len(part)
        digit=min(DIGITS,key=lambda d:abs(DIGITS[d][-1][1]-mean_v))
        for i in part:labels[i]=digit
    side='Left' if vertices[island[0]].co.x>0 else 'Right'
    names=[side+'Hand',side+'LowerArm']+[side+d+s for d in DIGITS for s in SEGMENTS]
    def dense(w):return np.array([w.get(n,0) for n in names],dtype=float)
    boundary={}
    for i in island:
        q=coords[i]
        digit=labels.get(i)
        if q.x>.475 and q.y<-.008:digit='Thumb'
        if digit:
            pts=DIGITS[digit];prefix=side+digit;w={prefix+'Proximal':1}
            w=mix(w,{prefix+'Intermediate':1},smooth(q.x,pts[1][0]-.014,pts[1][0]+.014))
            w=mix(w,{prefix+'Distal':1},smooth(q.x,pts[2][0]-.010,pts[2][0]+.010))
            boundary[i]=dense(w)
        elif q.x<.39 or (q.x<.425 and q.y>.020) or (q.z>.497 and q.x<.44):boundary[i]=dense(base_weights[i])
    interior=[i for i in island if i not in boundary];rows={i:k for k,i in enumerate(interior)}
    A=np.zeros((len(interior),len(interior)));B=np.zeros((len(interior),len(names)))
    for i,k in rows.items():
        for j,c in adj[i].items():
            A[k,k]+=c
            if j in rows:A[k,rows[j]]-=c
            else:B[k]+=c*boundary[j]
    # Soft thumb controls retain all three weighted phalanges without a hard
    # transition across the thumb web. Screened Laplacian remains normalized.
    for i,k in rows.items():
        q=coords[i]
        for segment,center in [('Proximal',(.408,.013,.468)),('Intermediate',(.441,-.012,.467))]:
            distance=((q.x-center[0])/.020)**2+((q.y-center[1])/.018)**2+((q.z-center[2])/.025)**2
            strength=20*math.exp(-distance/2)
            A[k,k]+=strength;B[k,names.index(side+'Thumb'+segment)]+=strength
    result=np.linalg.solve(A,B);assert np.max(np.abs(A@result-B))<1e-7
    out={}
    for i in island:
        w=boundary[i] if i in boundary else result[rows[i]]
        w=np.clip(w,0,1)
        # Four-influence GLB contract. Tiny harmonic tails are discarded; both
        # exported and DCC QA use the exact same truncated/normalized weights.
        keep=np.argsort(w)[-4:];w=np.array([x if k in keep else 0 for k,x in enumerate(w)])
        w/=w.sum();out[i]={n:float(x) for n,x in zip(names,w) if x>1e-7}
    return out

# One cylindrical QA grip, in source metres. This is an offline hand-fit fixture,
# not an adopted weapon dimension. Both mirrored hands use the same fixture.
GRIP_CENTER=(.485,.445)
GRIP_RADIUS=.013
GRIP_V_RANGE=(.015,.125)
GRIP_ANGLES={d:(15.,70.,75.) for d in ['Index','Middle','Ring','Little']}
GRIP_ANGLES['Thumb']=(15.,25.,25.)

def fit_grip(rig,mesh,scale,floor):
    """Deterministic bounded coordinate search using the actual skinned glove.
    The fixed gauge never shrinks to hide penetration. Fit the load-bearing
    digits, preserving a bent grasp and limiting angular departure from the seed.
    """
    global GRIP_ANGLES
    groups={g.index:g.name for g in mesh.vertex_groups}
    selected=[v for v in mesh.data.vertices if v.co.x>0.50*scale and any(groups[g.group].startswith('Left') and 'Arm' not in groups[g.group] for g in v.groups)]
    points=np.array([tuple(v.co) for v in selected]);n=len(points)
    names=['LeftHand','LeftLowerArm']+['Left'+d+s for d in DIGITS for s in SEGMENTS]
    W=np.zeros((n,len(names)))
    for row,v in enumerate(selected):
        for g in v.groups:
            if groups[g.group] in names:W[row,names.index(groups[g.group])]=g.weight
            elif groups[g.group].startswith('LeftLowerArmTwist'):W[row,0]+=g.weight
    masks={d:np.max(W[:,[names.index('Left'+d+s) for s in SEGMENTS]],axis=1)>.35 for d in DIGITS}
    def rotation(axis,angle):return np.array(Quaternion(Vector(axis),math.radians(angle)).to_matrix())
    def about(head,R):
        M=np.eye(4);M[:3,:3]=R;M[:3,3]=head-R@head;return M
    def evaluate(angles):
        matrices={'LeftHand':np.eye(4),'LeftLowerArm':np.eye(4)}
        for digit in DIGITS:
            parent=np.eye(4)
            a=angles[digit]
            for k,segment in enumerate(SEGMENTS):
                name='Left'+digit+segment;head=np.array(rig.data.bones[name].head_local)
                R=rotation((0,1,0),a[k])
                if digit=='Thumb' and k==0:R=rotation(Vector((1,0,1)).normalized(),THUMB_OPPOSITION_DEGREES)@R
                parent=parent@about(head,R);matrices[name]=parent
        deformed=np.zeros_like(points)
        for k,name in enumerate(names):
            M=matrices[name];deformed+=W[:,k,None]*(points@M[:3,:3].T+M[:3,3])
        x=deformed[:,0]/scale;y=deformed[:,1]/scale;z=deformed[:,2]/scale+floor
        u=(x-z)/math.sqrt(2);ww=(x+z)/math.sqrt(2)
        clearance=(np.hypot(u-GRIP_CENTER[0],ww-GRIP_CENTER[1])-GRIP_RADIUS)*scale
        valid=(y>GRIP_V_RANGE[0])&(y<GRIP_V_RANGE[1])
        return {d:float(clearance[valid&masks[d]].min()) for d in masks}
    angles=dict(GRIP_ANGLES);seed=dict(angles)
    def score(d,values):
        clear=evaluate(values)[d]
        prior=sum((x-y)**2 for x,y in zip(values[d],seed[d]))
        return 1e7*min(0,clear-.0003)**2+1e5*(clear-.001)**2+prior*.00005
    before=evaluate(angles)
    for d in masks:
        bounds=[(-10,45),(0,55),(0,55)] if d=='Thumb' else [(-10,50),(20,100),(20,90)]
        for step in [15,7.5,3,1]:
            for iteration in range(8):
                best=score(d,angles);choice=None
                for k in range(3):
                    for delta in [-step,step]:
                        trial=dict(angles);a=list(angles[d]);a[k]=max(bounds[k][0],min(bounds[k][1],a[k]+delta));trial[d]=tuple(a)
                        value=score(d,trial)
                        if value<best:best=value;choice=trial
                if choice is None:break
                angles=choice
    GRIP_ANGLES=angles
    return {'fixedGauge':{'centerUW':GRIP_CENTER,'radiusSourceMeters':GRIP_RADIUS,'axisVRange':GRIP_V_RANGE},'anglesDegrees':angles,'beforeClearanceMeters':before,'afterClearanceMeters':evaluate(angles)}

def twist_specs():
    out=[]
    for side,sign in [('Left',1),('Right',-1)]:
        a=Vector((sign*.409,.078,.275));b=Vector((sign*.566,.080,.113))
        for name,t,next_t in [('Twist1',.30,.60),('Twist2',.60,1.)]:
            out.append((side+'LowerArm'+name,tuple(a.lerp(b,t)),tuple(a.lerp(b,next_t)),side+'LowerArm'))
    return out

def distribute_forearm_twist(p,weights):
    side='Left' if p.x>=0 else 'Right';name=side+'LowerArm';amount=weights.get(name,0)
    if not amount:return weights
    f=mix({name:1},{name+'Twist1':1},smooth(uvw(p).x,.10,.20))
    f=mix(f,{name+'Twist2':1},smooth(uvw(p).x,.20,.285))
    out={k:v for k,v in weights.items() if k!=name}
    for k,v in f.items():out[k]=out.get(k,0)+v*amount
    # This split can create tiny tails; keep the actual export/DCC contract <=4.
    out=dict(sorted(out.items(),key=lambda kv:-kv[1])[:4]);total=sum(out.values())
    return {k:v/total for k,v in out.items() if v>1e-7}

def pose_twists(rig):
    for side in ['Left','Right']:
        fa=rig.pose.bones[side+'LowerArm'];hand=rig.pose.bones[side+'Hand']
        delta=fa.matrix.to_quaternion().inverted()@hand.matrix.to_quaternion()@hand.bone.matrix_local.to_quaternion().inverted()@fa.bone.matrix_local.to_quaternion()
        twist=Quaternion((delta.w,0,delta.y,0));twist.normalize()
        if twist.w<0:twist.negate()
        for suffix,fraction in [('Twist1',.5),('Twist2',1.)]:
            rig.pose.bones[side+'LowerArm'+suffix].rotation_quaternion=Quaternion((1,0,0,0)).slerp(twist,fraction)
