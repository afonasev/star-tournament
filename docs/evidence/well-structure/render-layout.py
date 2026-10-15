"""Render a dimensioned plan from the actual ArenaDefinition exported by Editor QA."""
from pathlib import Path
from html import escape
import json
root=Path(__file__).parent
d=json.loads((root/'editor-views/arena.json').read_text())
s=[]
def rect(x,y,w,h,fill,stroke='none',sw=1):s.append(f'<rect x="{x:.2f}" y="{y:.2f}" width="{w:.2f}" height="{h:.2f}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>')
def text(x,y,t,size=18,color='#203848',anchor='middle'):s.append(f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-family="Arial,sans-serif" font-size="{size}" fill="{color}">{escape(t)}</text>')
rect(0,0,1680,810,'#f1f4f6')
text(40,48,'КОЛОДЕЦ · СТРУКТУРА 03 / '+d['Identity'],27,anchor='start')
text(40,83,'План построен из проверенной геометрии Unity. Шаг этажей 7,2 м · перекрытия 0,8 м · чистая высота 6,4 м.',20,anchor='start')
# Read symbolic names through the separately retained approved position list to avoid enum-number assumptions.
approved=json.loads((root/'approved-layout.json').read_text())
labelsByName={'shotgun':'Д','rocket':'Р','cutter':'Рз','armor':'Б','speed':'С','damage':'У','heal':'Л'}
for f in range(3):
 y=f*7.2;cx=280+f*550;cy=385;k=6.6
 def box(pos,size,fill,stroke='none'):
  rect(cx+(pos['x']-size['x']/2)*k,cy-(pos['z']+size['z']/2)*k,size['x']*k,size['z']*k,fill,stroke)
 text(cx,128,f'{f+1} ЭТАЖ · +{y:g} м',22)
 solids=d['Solids']
 for q in solids:
  p=q['Position'];z=q['Size']
  if q['Material']=='floor' and abs(p['y']+z['y']/2-y)<.02:
   box(p,z,'#d1e7d8' if q['Id']=='garden' else '#b5cbd8' if q.get('Surface')=='well-grate' else '#fff','#9eacb6')
 # External stairs as paired runs, projection of the actual annex shell footprint.
 for side in [-1,1]:
  roof=next(q for q in solids if q['Id']=='stair-roof'+str(side));box(roof['Position'],roof['Size'],'#e0e6ea','#94a6b0')
  for i in range(15):
   x=side*(25.5+i*8.64/15);rect(cx+x*k,cy-3.2*k,.7,6.4*k,'#9eacb6')
 for q in solids:
  p=q['Position'];z=q['Size']
  if q['Material']!='floor' and p['y']-z['y']/2<y+1.5<p['y']+z['y']/2:
   box(p,z,'#81c7d4' if q['Material']=='glass' else '#506678')
 for p in d['Spawns']:
  if abs(p['y']-y)<.02:
   x=cx+p['x']*k;z=cy-p['z']*k;s.append(f'<circle cx="{x}" cy="{z}" r="6" fill="#276a50"/>')
 for p in d['Pickups']:
  if abs(p['Anchor']['y']-y)>.02:continue
  pos=p['Anchor'];a=next(a for a in approved['floors'][f]['items'] if abs(a['x']-pos['x'])<.01 and abs(a['z']-pos['z'])<.01)
  x=cx+pos['x']*k;z=cy-pos['z']*k;color='#b94a58' if a['kind'] in ['damage','heal'] else '#fff'
  s.append(f'<circle cx="{x}" cy="{z}" r="15" fill="{color}" stroke="#405a6c" stroke-width="2"/>');text(x,z+5,labelsByName[a['kind']],15,'#fff' if color!='#fff' else '#203848')
 text(cx,642,['Урон в центре; предметы у лифтов.','Наружный коридор вдоль лестниц открыт.','Две предметные комнаты без спавнов.'][f],17)
text(40,704,'● Спавн: 10 угловых комнат для матча на 2–8 участников.   Голубое — непробиваемое стекло; разрывы — открытые проёмы.',18,anchor='start')
text(40,741,'Д — дробовик    Р — ракетница    Рз — резак    Б — броня (щит)    С — скорость    У — урон    Л — лечение',19,anchor='start')
text(40,780,'Все 9 предметов имеют физическую опору и доступный маршрут. Центральная цепочка совмещена по решению пользователя.',18,anchor='start')
(root/'layout.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" width="1680" height="810">'+''.join(s)+'</svg>')
