#!/usr/bin/env python3
"""Render a review plan from Unity's exported ArenaDefinition (no hand-copied anchors)."""
import argparse
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def render(source, target):
    arena = json.loads(Path(source).read_text())
    assert arena['MapId'] == 'quarry-prototype-v1'
    assert len(arena['Spawns']) == 8 and len(arena['Pickups']) == 10
    scale = 2
    canvas = Image.new('RGB', (1680*scale, 1190*scale), '#111c28')
    d = ImageDraw.Draw(canvas)
    font_path = '/System/Library/Fonts/Supplemental/Arial.ttf'
    if not Path(font_path).exists():
        font_path = '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
    def font(size): return ImageFont.truetype(font_path, size*scale)
    def xy(x,z): return ((80+(x+50)*10.3)*scale, (170+(42-z)*10.3)*scale)
    def text(x,y,t,size=20,color='#e8eef3'):
        d.text((x*scale,y*scale),t,font=font(size),fill=color)
    def line(points,fill,width=2): d.line([(x*scale,y*scale) for x,y in points],fill=fill,width=width*scale)
    def tag(x,y,label,color,radius=17):
        x*=scale;y*=scale;r=radius*scale
        d.ellipse((x-r,y-r,x+r,y+r),fill=color,outline='#101924',width=2*scale)
        box=d.textbbox((0,0),label,font=font(16));d.text((x-(box[2]-box[0])/2,y-(box[3]-box[1])/2-3*scale),label,font=font(16),fill='white')
    def mark(p,label,color,radius=17):
        x,y=xy(p['x'],p['z']);tag(x/scale,y/scale,label,color,radius)
    def outline_dash(points,color,width=2):
        for a,b in zip(points,points[1:]+points[:1]):
            length=math.dist(a,b);n=max(1,round(length/(7*scale)))
            for j in range(0,n,2):
                t=j/n;u=min(1,(j+1)/n)
                d.line([(a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t),(a[0]+(b[0]-a[0])*u,a[1]+(b[1]-a[1])*u)],fill=color,width=width*scale)
    def polygon(s):
        p,sz,q=s['Position'],s['Size'],s['Rotation'];shape=s.get('Surface')
        if shape in ('quarry-boulder','quarry-slab'):
            points=[(-.5,-.27),(-.23,-.5),(.29,-.42),(.5,-.16),(.38,.36),(.07,.5),(-.39,.33)]
        elif shape=='quarry-faceted':points=[(-.36,-.5),(.36,-.5),(.5,-.36),(.5,.36),(.36,.5),(-.36,.5),(-.5,.36),(-.5,-.36)]
        else:points=[(-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5)]
        # Project the full 3D extent, including pitched ramps and loader wheels.
        # The convex hull removes duplicate/hidden corners in a top-down view.
        if shape=='quarry-cylinder':
            points=[(.5*math.cos(i*math.pi/8),.5*math.sin(i*math.pi/8)) for i in range(16)]
        result=[]
        for a,b in points:
            for h in (-.5,.5):
                x,y,z=a*sz['x'],h*sz['y'],b*sz['z']
                rx=(1-2*(q['y']**2+q['z']**2))*x+2*(q['x']*q['y']-q['w']*q['z'])*y+2*(q['x']*q['z']+q['w']*q['y'])*z
                rz=2*(q['x']*q['z']-q['w']*q['y'])*x+2*(q['y']*q['z']+q['w']*q['x'])*y+(1-2*(q['x']**2+q['y']**2))*z
                result.append(xy(p['x']+rx,p['z']+rz))
        points=sorted(set(result))
        def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
        def half(seq):
            out=[]
            for pt in seq:
                while len(out)>1 and cross(out[-2],out[-1],pt)<=0:out.pop()
                out.append(pt)
            return out
        return half(points)[:-1]+half(reversed(points))[:-1]
    text(65,40,'КАРЬЕР / УКРЫТИЯ И ТОЧКИ ПОДБОРА',34)
    text(65,91,'Вид сверху • север ↑ • координаты экспортированы из игровой сцены',20,'#9aafbf')
    text(65,124,arena['Identity']+'   |   8 спавнов · 5 точек оружия · 5 бонусов',18,'#9aafbf')
    solids=arena['Solids']
    for s in sorted(solids,key=lambda s:s['Position']['y']+s['Size']['y']/2):
        ident=s['Id'];mat=s['Material'];support=s.get('Support','')
        if '-fill-' in ident or ident in ('rear-cliff','workshop-awning'):continue
        if 'lamp' in ident:continue
        points=polygon(s)
        if mat=='floor':
            fill='#8f7859' if support=='support:quarry-ground' else '#b2a48b' if support=='support:quarry-terrace' else '#7d919d'
            if ident.startswith('transition:'):fill='#cdb487'
        else:
            fill={'quarry-rock':'#766851','quarry-crate':'#ac7844','quarry-metal':'#586c79','quarry-machine':'#394b59','quarry-portal':'#e5a148','quarry-rubber':'#253038','quarry-glass':'#48788c'}.get(mat,'#766851')
        d.polygon(points,fill=fill)
        # Floor cap borders and actual object footprints remain legible.
        if '-rock' not in ident:d.line(points+[points[0]],fill='#524b3f',width=scale)
    canopy=next(s for s in solids if s['Id']=='workshop-awning');outline_dash(polygon(canopy),'#d3dce1',2)
    # Roofed passage is below the walkable center; show its actual roof-cell footprint dashed.
    for s in solids:
        if s['Id'].startswith('tunnel-roof-'):outline_dash(polygon(s),'#eee1b8',2)
    def arrow(a,b,color='#4fd2d1'):
        pa,pb=xy(*a),xy(*b);d.line([pa,pb],fill=color,width=3*scale)
        ang=math.atan2(pb[1]-pa[1],pb[0]-pa[0]);length=11*scale
        d.polygon([pb,(pb[0]-length*math.cos(ang-.5),pb[1]-length*math.sin(ang-.5)),(pb[0]-length*math.cos(ang+.5),pb[1]-length*math.sin(ang+.5))],fill=color)
    for tr in arena['Transitions']:
        a,b=tr['OrderedFeet'][0],tr['OrderedFeet'][-1];arrow((a['x'],a['z']),(b['x'],b['z']),'#ffe0a1')
    arrow((0,17),(0,9));arrow((23,9.5),(12,9.5))
    for i,p in enumerate(arena['Spawns'],1):mark(p,'S'+str(i),'#2464c4')
    weapons=[p for p in arena['Pickups'] if p['Kind']>=4];bonus=[p for p in arena['Pickups'] if p['Kind']<4]
    for i,p in enumerate(weapons,1):mark(p['Anchor'],'W'+str(i),'#a34232')
    for i,p in enumerate(bonus,1):mark(p['Anchor'],'B'+str(i),'#197a61')
    for x,z,label in [(2,35.5,'МАСТЕРСКАЯ +8 м'),(3,17.7,'СРЕДНИЙ +4 м'),(1,-5,'ЦЕНТР +4 м'),(9,-36,'ПОГРУЗКА 0 м')]:
        px,py=xy(x,z);box=d.textbbox((0,0),label,font=font(17));d.text((px-(box[2]-box[0])/2,py),label,font=font(17),fill='#fff7e7',stroke_width=scale,stroke_fill='#524b3f')
    text(1160,170,'ОРУЖИЕ',23)
    names={0:'Броня',1:'Скорость',2:'Усиление урона',3:'Полное лечение',4:'Дробовик',5:'Ракетница',6:'Лазер',7:'Плазмомёт'}
    def item_rows(items,prefix,y,color):
        for i,p in enumerate(items,1):
            tag(1181,y+12,prefix+str(i),color);a=p['Anchor'];text(1211,y-1,names[p['Kind']],21)
            suffix=' • внутри тоннеля' if p['Kind']==3 else ''
            text(1211,y+25,f"{a['y']:+g} м{suffix}",17,'#9aafbf');y+=66
        return y
    item_rows(weapons,'W',213,'#a34232');text(1160,568,'БОНУСЫ',23);item_rows(bonus,'B',610,'#197a61')
    text(1160,973,'СПАВНЫ И МАРШРУТЫ',21)
    text(1160,1010,'S1–S2: 0 м   S3–S5: +4 м',18,'#a4c6ff');text(1160,1038,'S6–S8: +8 м',18,'#a4c6ff')
    text(65,1050,'Пунктир в центре — тоннель под площадкой; B3 находится внизу, на отметке 0 м.',19)
    text(65,1085,'Жёлтые стрелки — подъёмы. Бирюзовые — выходы на крышу тоннеля.',19,'#9aafbf')
    text(65,1120,'Контуры препятствий — проекция геометрии. Навес показан пунктиром, чтобы видеть точки под ним.',17,'#9aafbf')
    # Ten-metre scale taken from the same projection as the map.
    line([(870,1018),(973,1018)],'#eef2f4',3);text(902,1024,'10 м',15)
    canvas.resize((1680,1190),Image.Resampling.LANCZOS).save(target)
    return {'identity':arena['Identity'],'spawns':len(arena['Spawns']),'weapons':len(weapons),'bonuses':len(bonus),'source':str(source),'output':str(target)}

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source');p.add_argument('target');a=p.parse_args();print(json.dumps(render(a.source,a.target),ensure_ascii=False,indent=2))
