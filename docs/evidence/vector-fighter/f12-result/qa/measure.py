from pathlib import Path
import math,json,sys
weapons=['Rifle','Shotgun','RocketLauncher','Cutter']
def sub(a,b):return [x-y for x,y in zip(a,b)]
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def angle(a,b):return math.degrees(math.acos(max(-1,min(1,dot(a,b)/(dot(a,a)*dot(b,b))**.5))))
def read(root,w):
 out={}
 for l in (Path(root)/(w+'-grip.csv')).read_text().splitlines():
  a=l.split(',')
  if a[0].startswith('Left') and len(a)==4:out[a[0]]=list(map(float,a[1:]))
 return out
results={}
for key,root in [('F11','docs/evidence/vector-fighter/f11-result/frames'),('F12',sys.argv[1])]:
 rows={};directions=[]
 for w in weapons:
  b=read(root,w);arm=sub(b['LeftHand'],b['LeftLowerArm']);hand=sub(b['LeftMiddleProximal'],b['LeftHand']);directions.append(arm)
  rows[w]={'wrist_degrees':round(angle(arm,hand),3),'elbow':b['LeftLowerArm'],'wrist':b['LeftHand'],'palm':b['LeftMiddleProximal']}
 results[key]={'weapons':rows,'max_pairwise_forearm_degrees':round(max(angle(a,b) for a in directions for b in directions),3)}
print(json.dumps(results,indent=2))
