import bpy,json,sys,struct
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from array import array
root=Path(sys.argv[sys.argv.index('--')+1]);out=Path('.local/vector-f14/surface');out.mkdir(exist_ok=True)
axes=[(-.40,.40,81),(-.52,.30,83),(-.52,.85,138)]
for weapon in ['Rifle','Shotgun','RocketLauncher','Cutter']:
 vertices=[];faces=[];name=''
 for line in (root/(weapon+'-actual-mesh.obj')).read_text().splitlines():
  a=line.split()
  if not a:continue
  if a[0]=='o':name=a[1]
  elif a[0]=='v':vertices.append(Vector(tuple(map(float,a[1:]))))
  elif a[0]=='f' and 'vector-armored-hands' not in name:faces.append(tuple(int(i)-1 for i in a[1:]))
 tree=BVHTree.FromPolygons(vertices,faces,all_triangles=True);probe=Vector((5,0,0));hit,normal,_,_=tree.find_nearest(probe);sign=1 if (probe-hit).dot(normal)>0 else -1
 field=array('f')
 for xi in range(axes[0][2]):
  x=axes[0][0]+xi*.01
  for yi in range(axes[1][2]):
   y=axes[1][0]+yi*.01
   for zi in range(axes[2][2]):
    p=Vector((x,y,axes[2][0]+zi*.01));hit,normal,_,dist=tree.find_nearest(p);field.append(dist*(1 if sign*(p-hit).dot(normal)>0 else -1))
 with (out/(weapon+'.bin')).open('wb') as f:field.tofile(f)
 print(weapon,'done',flush=True)
(out/'grid.json').write_text(json.dumps(axes))
