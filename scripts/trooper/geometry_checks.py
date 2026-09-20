"""Pure triangle/finite-cylinder distance used by exported grip QA."""
from mathutils import Vector

def radial_distance_to_triangle(points, center, axial_range):
    # Clip the actual exported triangle to the finite cylinder's V interval.
    poly=list(points)
    for bound,lower in [(axial_range[0],True),(axial_range[1],False)]:
        output=[]
        for a,b in zip(poly,poly[1:]+poly[:1]):
            inside_a=a.y>=bound if lower else a.y<=bound
            inside_b=b.y>=bound if lower else b.y<=bound
            if inside_a:output.append(a)
            if inside_a!=inside_b:output.append(a.lerp(b,(bound-a.y)/(b.y-a.y)))
        poly=output
        if not poly:return None
    p=[Vector((q.x-center[0],q.z-center[1])) for q in poly]
    signs=[a.x*b.y-a.y*b.x for a,b in zip(p,p[1:]+p[:1])]
    if len(p)>=3 and abs(sum(signs))>1e-14 and (all(x>=0 for x in signs) or all(x<=0 for x in signs)):return 0.
    distances=[]
    for a,b in zip(p,p[1:]+p[:1]):
        d=b-a;t=max(0,min(1,-a.dot(d)/d.length_squared)) if d.length_squared>1e-16 else 0
        distances.append((a+d*t).length)
    return min(distances)

if __name__=='__main__':
    def check(points,expected):
        result=radial_distance_to_triangle([Vector(p) for p in points],(0,0),(0,1))
        assert result is None if expected is None else result is not None and abs(result-expected)<1e-6,(result,expected)
    check([(-1,.5,-1),(1,.5,-1),(0,.5,1)],0) # Outside vertices enclose cylinder axis.
    check([(2,.5,0),(3,.5,0),(2,.5,1)],2) # Entire triangle outside.
    check([(.1,-1,0),(.1,2,0),(.2,2,.1)],.1) # Crosses both finite end planes.
    check([(-1,2,-1),(1,2,-1),(0,3,1)],None) # Outside axial interval.
    check([(2,0,0),(3,.5,0),(4,.5,0)],2) # Collinear projection is not containment.
    print('PASS: 5 exact triangle/cylinder distance cases')
