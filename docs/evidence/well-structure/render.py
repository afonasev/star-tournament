from pathlib import Path
from html import escape
P=Path(__file__).parent
s=[]
def rect(x,y,w,h,fill,stroke='#72818a',sw=2): s.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>')
def line(x,y,xx,yy,color='#52616b',w=3,dash=''): s.append(f'<path d="M{x},{y} L{xx},{yy}" fill="none" stroke="{color}" stroke-width="{w}" stroke-dasharray="{dash}"/>')
def text(x,y,t,size=18,color='#223746',anchor='middle'): s.append(f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-family="Arial,sans-serif" font-size="{size}" fill="{color}">{escape(t)}</text>')
rect(0,0,1680,1280,'#f3f5f6','none')
text(40,48,'КОЛОДЕЦ / СТРУКТУРА 03 — предложение для согласования',30,anchor='start')
text(40,82,'Тот же корпус 49,6 × 49,6 м и колодец 19,2 × 19,2 м. Общие оси стен, встроенные комнаты и ясные маршруты.',20,anchor='start')
items=[ [('Д',-20,20),('Р',-20,-20),('Б',20,20),('С',20,-20),('У',6,-6)], [('Р',0,0),('Рз',20,6),('Б',-20,-6)], [('Л',0,0),('Рз',-20,20),('Д',20,-20),('С',-20,-20)] ]
for f in range(3):
    ox=280+f*550; oy=400; k=7
    def xy(x,z): return ox+x*k,oy-z*k
    def box(x,z,w,d,c='#e1e7eb'):
        a,b=xy(x-w/2,z+d/2);rect(a,b,w*k,d*k,c)
    def seg(x,z,xx,zz,c='#536875',w=3,dash=''): a,b=xy(x,z);aa,bb=xy(xx,zz);line(a,b,aa,bb,c,w,dash)
    def room(x,z,w,d,axis):
        box(x,z,w,d,'#dce5eb')
        # Two openings connect each wing room with ring/corner circulation.
        if axis=='x':
            for dz in [-d/2,d/2]: seg(x-1.6,z+dz,x+1.6,z+dz,'#fdfefe',5)
            ix=x-w/2 if x>0 else x+w/2;seg(ix,z-1.6,ix,z+1.6,'#fdfefe',5)
        else:
            iz=z-d/2 if z>0 else z+d/2;seg(x-1.6,iz,x+1.6,iz,'#fdfefe',5)
            ix=x-w/2 if x>0 else x+w/2;seg(ix,z-1.6,ix,z+1.6,'#fdfefe',5)
    text(ox,133,f'{f+1} ЭТАЖ / '+['БИО','ФИЗИКА','ЭЛЕКТРОНИКА'][f],23)
    text(ox,163,['сад и риск за урон','центральная ракетница','лечение на верхней перемычке'][f],17)
    box(0,0,49.6,49.6,'#fdfefe');box(0,0,19.2,19.2,'#d9ebdf' if f==0 else '#f3f5f6')
    for x in [-20.4,20.4]:
        for z in [-6.4,6.4]:room(x,z,8.8,6.4,'x')
    for x in [-17.2,17.2]:
        for z in [-20.4,20.4]:room(x,z,15.2,8.8,'z')
    # Four cardinal lobbies and continuous inner ring.
    for a,b,c,d in [(-13.2,-13.2,13.2,-13.2),(13.2,-13.2,13.2,13.2),(13.2,13.2,-13.2,13.2),(-13.2,13.2,-13.2,-13.2)]:seg(a,b,c,d,'#28a5a1',2,'6 4')
    for z in [-28.7,28.7]:
        box(0,z,9.6,7.8,'#d8e5f0');a,b=xy(0,z);text(a,b+5,'ЛФ',14);seg(-3.5,24.8*(1 if z>0 else -1),3.5,24.8*(1 if z>0 else -1),'#fdfefe',6)
    for x in [-30.6,30.6]:
        box(x,0,11.6,7.4,'#e0e5e9')
        for xx in range(7):seg(x-4.5+xx*1.35,-2.8,x-4.5+xx*1.35,2.8,'#95a4ad',1)
        a,b=xy(x,0);text(a,b+5,'СТ',14);seg(24.8*(1 if x>0 else -1),-3.5,24.8*(1 if x>0 else -1),3.5,'#fdfefe',6)
    if f==1:box(0,0,3.6,19.2,'#c6d4de')
    if f==2:
        for x in [-4.8,4.8]:box(x,0,3,19.2,'#c6d4de')
        box(0,0,19.2,3,'#c6d4de')
    # Atrium entrances aligned to existing bridge directions.
    for z in [-9.6,9.6]:
        for x in ([0] if f==1 else [-4.8,4.8] if f==2 else []):seg(x-1.5,z,x+1.5,z,'#fdfefe',5)
    if f in [0,2]:
        for x in [-9.6,9.6]:seg(x,-1.6,x,1.6,'#fdfefe',5)
    for label,x,z in items[f]:
        a,b=xy(x,z);s.append(f'<circle cx="{a}" cy="{b}" r="14" fill="{"#bd4855" if label in ["У","Л"] else "#fff"}" stroke="#40576b" stroke-width="2"/>');text(a,b+5,label,14,'#fff' if label in ['У','Л'] else '#223746')
    text(ox,655,['Урон смещён от оси падения на 8,5 м.','Броня и резак — в противоположных крыльях.','Верхние предметы требуют обхода кольца.'][f],16)
    text(ox,682,'Белые разрывы — проходы; бирюзовый пунктир — кольцевой путь.',14)
text(40,733,'Д — дробовик   Р — ракетница   Рз — резак   Б — броня   С — скорость   У — урон   Л — лечение',19,anchor='start')
line(40,759,1640,759,'#bbc6cd',1)
text(40,799,'ВЫСОТА / два варианта одного плана',24,anchor='start')
for idx,(step,slab,title) in enumerate([(7.2,.8,'1 · Рекомендуемый'),(6.4,.6,'2 · Сдержанный')]):
    x=65+idx*590; base=1160; scale=12
    text(x,841,title,22,anchor='start')
    text(x,870,f'Шаг {step:g} м · перекрытие {slab:g} м · чистая высота {step-slab:g} м',17,anchor='start')
    for f in range(4):
        y=base-f*step*scale;rect(x,y,350,slab*scale,'#586f80','none');text(x+368,y+7,f'+{f*step:g} м',16,anchor='start')
        if f<3:
            line(x,base-f*step*scale,x,base-(f+1)*step*scale,'#586f80',4);line(x+350,base-f*step*scale,x+350,base-(f+1)*step*scale,'#586f80',4)
    text(x,1202,'Сейчас: шаг 5 м / перекрытие 0,3 м / чистая высота 4,7 м.',15,anchor='start')
text(1245,850,'Архитектурные правила',21,anchor='start')
for i,t in enumerate(['• Стены по общим осям этажей.','• Кабинеты примыкают к корпусу.','• Проходные комнаты + обход.','• Спавны в угловых нишах.','• Без отдельных Г-образных ширм.','• Лестницы длиннее, уклон прежний.','• Лифты сохраняют семантику.']):text(1245,890+i*33,t,17,anchor='start')
text(40,1240,'Это структурный эскиз, не результат Unity QA. Проёмы, укрытие спавнов и маршруты ботов подлежат проверке в движке.',18,anchor='start')
(P/'proposal.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" width="1680" height="1280" viewBox="0 0 1680 1280">'+''.join(s)+'</svg>')
