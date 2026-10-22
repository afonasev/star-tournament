import bpy,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path(sys.argv[sys.argv.index('--')+1]);report={}
for weapon in ['Rifle','Shotgun','RocketLauncher','Cutter']:
 vertices=[];faces=[];name=''
 for line in (root/(weapon+'-actual-mesh.obj')).read_text().splitlines():
  a=line.split()
  if not a:continue
  if a[0]=='o':name=a[1]
  elif a[0]=='v':vertices.append(Vector(tuple(map(float,a[1:]))))
  elif a[0]=='f' and 'vector-armored-hands' not in name:faces.append(tuple(int(i)-1 for i in a[1:]))
 tree=BVHTree.FromPolygons(vertices,faces,all_triangles=True);probe=Vector((5,0,0));hit,normal,_,_=tree.find_nearest(probe);sign=1 if (probe-hit).dot(normal)>0 else -1;records={}
 for line in (root/(weapon+'-vector-armored-hands-skin.txt')).read_text().splitlines():
  a=line.split()
  if not a or a[0]!='v':continue
  p=Vector(tuple(map(float,a[1:4])));ws={k:float(v) for k,v in (item.split(':') for item in a[4:])}
  for side in ['Left','Right']:
   for digit in ['Thumb','Index','Middle','Ring','Little']:
    prefix=side+digit
    if sum(w for k,w in ws.items() if k.startswith(prefix))<.8:continue
    hit,normal,_,dist=tree.find_nearest(p);signed=dist*(1 if sign*(p-hit).dot(normal)>0 else -1);records.setdefault(prefix,[]).append(signed)
 report[weapon]={}
 for name,ds in records.items():
  ds.sort();report[weapon][name]={'minimum_signed_rig_units':ds[0],'contact_abs_p01_rig_units':sorted(abs(x) for x in ds)[int(len(ds)*.01)],'penetration_fraction_below_003':sum(x<-.003 for x in ds)/len(ds),'vertices':len(ds)}
(root/'contact-review.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
