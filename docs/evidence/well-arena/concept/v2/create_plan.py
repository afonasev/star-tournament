from pathlib import Path
from html import escape
import json

ROOT=Path(__file__).parent
BG='#f4f7f6'; INK='#293e48'; WALL='#617781'
COLORS=['#328358','#287eaf','#ab8318']
WASH=['#e5f1e8','#e5eff7','#f8f1d9']
NAMES=['БИОРАЗРАБОТКИ','ФИЗИКА','ЭЛЕКТРОНИКА']
DESCRIPTIONS=[
('Залы и две петли', 'На севере галерея образцов разветвляется вокруг демонстрационного блока. Западный коридор идёт вдоль окон, кабинеты стоят у наружной стены. На востоке всё наоборот: кабинеты у колодца, широкий коридор снаружи. Южная препараторская имеет два входа и два обхода, поэтому даёт третью связь между соседними холлами.'),
('Большой зал и испытательный блок', 'Север — широкий проходной презентационный зал. Запад — защищённый наружный коридор, отделённый от окон кабинетами. Восточный испытательный блок можно обойти с обеих сторон или пересечь через две двери. Южная галерея проходит вдоль окон; кабинеты с закрытыми дверями занимают наружную половину крыла.'),
('Смена внутреннего и внешнего маршрута', 'Северный коридор идёт вдоль окон, западный — у наружной стены, восточный снова у окон. Поперечные холлы связывают их с вынесенными лестницами и мостами. На юге — широкий демонстрационный зал. Северо-западная монтажная и юго-восточная ремонтная имеют проходные рабочие комнаты с двумя дверями и внешние обходы: можно пересечь помещение или обойти его.')
]

def rect(x,y,w,h,fill,stroke=WALL,sw=3,r=0):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>'
def line(x1,y1,x2,y2,color=WALL,w=4,dash=None):
    return f'<path d="M{x1} {y1}L{x2} {y2}" fill="none" stroke="{color}" stroke-width="{w}"'+(f' stroke-dasharray="{dash}"' if dash else '')+'/>'
def txt(x,y,label,size=15,color=INK,weight=400):
    rows=label.split('|')
    return f'<text x="{x}" y="{y}" text-anchor="middle" font-family="Arial,sans-serif" font-size="{size}" fill="{color}" font-weight="{weight}" stroke="{BG}" stroke-width="2.5" paint-order="stroke fill">'+''.join(f'<tspan x="{x}" dy="{0 if i==0 else size+4}">{escape(s)}</tspan>' for i,s in enumerate(rows))+'</text>'
def path(points,color,w=3,dash=None):
    return f'<polyline points="'+ ' '.join(f'{x},{y}' for x,y in points)+f'" fill="none" stroke="{color}" stroke-width="{w}" stroke-linecap="round" stroke-linejoin="round"'+(f' stroke-dasharray="{dash}"' if dash else '')+'/>'
def closed(x,y,w,h,door='S',label='Каб.'):
    s=rect(x,y,w,h,'url(#closed)')
    if door in ['N','S']:
        yy=y if door=='N' else y+h
        s+=line(x+w/2-12,yy,x+w/2+12,yy,'#c48755',6)
    else:
        xx=x if door=='W' else x+w
        s+=line(xx,y+h/2-12,xx,y+h/2+12,'#c48755',6)
    return s+txt(x+w/2,y+h/2-3,label,12,'#677781')
def openroom(x,y,w,h,doors,label):
    s=rect(x,y,w,h,'#fff')
    for side in doors:
        if side in ['N','S']:
            yy=y if side=='N' else y+h;s+=line(x+w/2-23,yy,x+w/2+23,yy,'#fff',7)
        else:
            xx=x if side=='W' else x+w;s+=line(xx,y+h/2-23,xx,y+h/2+23,'#fff',7)
    return s+txt(x+w/2,y+h/2-3,label,12)
def item(a,x,y,color):
    major=a in ['D','♥']
    return f'<circle cx="{x}" cy="{y}" r="{18 if major else 15}" fill="{"#cd4c58" if major else "white"}" stroke="{"#a43340" if major else color}" stroke-width="2"/>'+txt(x,y+5,a,16,'white' if major else color,700)

all_bodies=[]
for i in range(3):
    c=COLORS[i]; wash=WASH[i]
    s='<defs><pattern id="closed" width="10" height="10" patternUnits="userSpaceOnUse"><rect width="10" height="10" fill="#e4e9ed"/><path d="M0 10L10 0" stroke="#d4dce2"/></pattern><pattern id="mesh" width="7" height="7" patternUnits="userSpaceOnUse"><rect width="7" height="7" fill="#d0d9dd"/><path d="M0 0H7M0 0V7" stroke="#899aa3" stroke-width="1"/></pattern></defs>'
    s+=rect(140,140,620,620,wash,WALL,5)+rect(330,330,240,240,BG,WALL,4)
    # External annexes: all four lie fully outside the square floorplate.
    for y,lab in [(42,'СЕВЕРНЫЙ ЛИФТ'),(760,'ЮЖНЫЙ ЛИФТ')]:
        s+=rect(390,y,120,98,'#dce6eb',WALL,4,4)
        s+=rect(401,y+11,98, 70,'#f2f6f8','#8b9da6',2,3)
        s+=txt(450,y+43,'ШИРОКИЙ',12)+txt(450,y+63,'ЛИФТ',15,INK,700)
        s+=txt(450,y-10 if y==42 else y+121,lab,12)
    for x in [32,760]:
        s+=rect(x,380,108,140,'#e4e9e8',WALL,4,4)
        for y in range(395,482,12):
            s+=line(x+12,y,x+43,y,'#9baaa7',2)+line(x+65,y,x+96,y,'#9baaa7',2)
        s+=rect(x+10,487,88,22,'#f7f8f7','#a2afaa',1)+txt(x+54,455,'СТ',17,INK,700)
    # Wide external doors do not interrupt the perimeter circulation.
    s+=line(406,140,494,140,wash,9)+line(406,760,494,760,wash,9)
    s+=line(140,410,140,490,wash,9)+line(760,410,760,490,wash,9)
    s+=txt(86,548,'ЛЕСТНИЦА',12)+txt(814,548,'ЛЕСТНИЦА',12)
    s+=txt(450,12,'СЕВЕР ↑',12)
    # Alternating glass and empty openings. Bridge portals are cut later.
    for k,a in enumerate([345,395,445,495,545]):
        for side in range(4):
            color='#4bb7c5' if (k+side+i)%2==0 else '#d98649'
            b=min(a+28,560);args=[(a,330,b,330),(570,a,570,b),(a,570,b,570),(330,a,330,b)][side]
            s+=line(*args,color,6,None if color=='#4bb7c5' else '5 4')
    # Routes are deliberate branches, not a single generic square path.
    routes=[]
    if i==0:
        routes=[[(290,290),(290,450),(290,610)],[(240,450),(145,450)],[(295,190),(450,190),(605,190)],[(295,290),(450,290),(605,290)],[(310,190),(310,290)],[(595,190),(595,290)],[(710,290),(710,450),(710,610)],[(670,450),(755,450)],[(300,600),(450,600),(600,600)],[(300,730),(450,730),(600,730)],[(350,600),(350,730)],[(550,600),(550,730)],[(450,600),(450,730)]]
        for p in routes:s+=path(p,c,3,'7 7')
        # A north exhibit island produces two playable parallel lanes.
        s+=rect(350,223,200,38,'#cddbd2','#7f9787',2,3)+txt(450,247,'ВИТРИНЫ',12)
        s+=txt(450,311,'Галерея образцов',16,c,600)
        s+=closed(145,338,95,65,'E')+closed(145,498,95,65,'E')
        s+=closed(574,342,92, 90,'E')+closed(574,475,92,90,'E')
        s+=txt(282,382,'Вдоль|окон',12,c)+txt(710,393,'Внешний|коридор',12,c)
        s+=openroom(382,635,136,60,['N','S'],'Препараторская')
        s+=txt(450,620,'Внутренний обход',12,c)+txt(450,715,'Внешний обход',12,c)
        s+=txt(234,180,'Мокрая',17,c,600)+txt(234,201,'лаборатория',17,c,600)
        s+=rect(165,220,100,28,'#cddbd2','#7f9787',2,3)
        s+=txt(668,181,'Теплица',17,c,600)
        s+=line(585,235,648,235)+line(702,235,760,235)
        s+=txt(672,271,'Анализ образцов',13)
        s+=txt(233,682,'Приём|образцов',17,c,600)
        s+=line(190,620,280,620)+line(280,660,280,715)
        s+=rect(645,648,65,65,'#cddbd2','#7f9787',2,3)
        s+=txt(670,604,'Биобанк',17,c,600)
        # Garden is predominantly open, with clear west/east ground entrances.
        s+=rect(350,351,200,198,'#e4eddb','#b0c3a3',1,4)
        s+=line(329,450,571,450,'#f5f0e5',45)
        s+=rect(359,490,45,40,'#b4cea4','#9ab58a',1,4)+rect(493,365,40,44,'#b4cea4','#9ab58a',1,4)
        s+=txt(450,408,'САД',20,'#537c4a',700)
        s+=line(330,429,330,471,'#f5f0e5',8)+line(570,429,570,471,'#f5f0e5',8)
        items=[('Д',220,285),('Р',315,692),('Б',426,507),('У',710,583)]
    elif i==1:
        routes=[[(260,275),(450,275),(660,275)],[(450,145),(450,275),(450,327)],[(190,290),(190,450),(190,610)],[(190,450),(330,450)],[(600,310),(600,450),(600,610)],[(725,310),(725,450),(725,610)],[(600,350),(725,350)],[(600,550),(725,550)],[(600,450),(755,450)],[(290,610),(450,610),(610,610)],[(450,610),(450,755)]]
        for p in routes:s+=path(p,c,3,'7 7')
        s+=txt(450,216,'ПРЕЗЕНТАЦИОННЫЙ ЗАЛ',18,c,700)
        s+=rect(335,151,65,23,'#ced8df','#8a9ba6',1)+rect(500,151,65,23,'#ced8df','#8a9ba6',1)
        s+=txt(450,238,'широкий проход через зал',12)
        s+=closed(243,340,84,75,'W')+closed(243,485,84,75,'W')
        s+=txt(186,384,'Внешний|коридор',12,c)
        s+=openroom(634,385,61,130,['W','E'],'Опытная')
        s+=txt(660,579,'Обходы / через зал',11,c)
        s+=closed(333,675,67,82,'N')+closed(500,675,67,82,'N')
        s+=txt(450,645,'Галерея вдоль окон',16,c,600)
        s+=txt(234,184,'Подготовка|презентаций',16,c,600)
        s+=line(298,140,298,215)+line(298,280,298,330)
        s+=txt(674,183,'Оптическая',17,c,600)
        s+=rect(640,215,75,36,'#ced8df','#8a9ba6',2,3)
        s+=txt(235,676,'Приборная',17,c,600)
        s+=line(140,643,220,643)+line(280,643,328,643)
        s+=txt(674,676,'Измерительная',16,c,600)
        s+=line(613,646,613,708)+line(613,748,613,760)
        s+=rect(434,329,32,242,'url(#mesh)','#81959f',2)
        s+=line(436,330,464,330,'#d0d9dd',7)+line(436,570,464,570,'#d0d9dd',7)
        s+=txt(506,490,'ПУСТОТА',12,'#8a9ca3')
        items=[('D',450,389),('Р',723,325),('Э',254,709),('Б',350,293)]
    else:
        routes=[[(290,285),(450,285),(610,285)],[(450,145),(450,285)],[(190,290),(190,450),(190,610)],[(145,450),(330,450)],[(610,290),(610,450),(610,610)],[(570,450),(755,450)],[(270,630),(450,630),(630,630)],[(450,630),(450,755)],[(176,170),(288,170),(288,302),(176,302),(176,170)],[(610,603),(730,603),(730,730),(610,730),(610,603)],[(235,190),(235,302)],[(610,675),(730,675)]]
        for p in routes:s+=path(p,c,3,'7 7')
        s+=closed(333,143,67,90,'S')+closed(500,143,67,90,'S')
        s+=txt(450,310,'Коридор вдоль окон',15,c,600)
        s+=closed(242,338,85,67,'W')+closed(242,495,85,67,'W')
        s+=closed(672,338,85,67,'W')+closed(672,495,85,67,'W')
        s+=txt(185,375,'Внешний|коридор',12,c)+txt(615,375,'Вдоль|окон',12,c)
        s+=txt(450,678,'ДЕМОНСТРАЦИОННЫЙ ЗАЛ',17,c,700)
        s+=rect(335,720,60,25,'#e3d8ad','#b3a267',1)+rect(505,720,60,25,'#e3d8ad','#b3a267',1)
        s+=openroom(205,215,60,55,['N','S'],'Монтаж')+txt(234,198,'Монтажная',16,c,600)
        s+=txt(674,184,'Диагностика',16,c,600)
        s+=line(585,243,640,243)+line(700,243,760,243)
        s+=txt(674,280,'Испытания плат',13)
        s+=txt(234,681,'Серверная',17,c,600)
        s+=rect(160,604,26,85,'#e3d8ad','#b3a267',2)+rect(287,653,26,85,'#e3d8ad','#b3a267',2)
        s+=openroom(635,645,70,60,['W','E'],'Ремонт')+txt(670,583,'Ремонтная',16,c,600)
        s+=rect(377,329,28,242,'url(#mesh)','#81959f',2)+rect(495,329,28,242,'url(#mesh)','#81959f',2)+rect(329,436,242,28,'url(#mesh)','#81959f',2)
        for x in [378,496]:s+=line(x,330,x+26,330,'#d0d9dd',7)+line(x,570,x+26,570,'#d0d9dd',7)
        s+=line(330,437,330,463,'#d0d9dd',7)+line(570,437,570,463,'#d0d9dd',7)
        items=[('♥',450,450),('Э',285,336),('Д',731,710),('У',325,631)]
    for a,x,y in items:s+=item(a,x,y,c)
    s+=txt(450,902,'Внешние башни · широкие холлы · разные маршруты',14,c)
    all_bodies.append(s)
    svg=f'<svg xmlns="http://www.w3.org/2000/svg" width="920" height="925" viewBox="0 0 920 925"><rect width="920" height="925" fill="{BG}"/>{s}</svg>'
    (ROOT/f'floor-{i+1}.svg').write_text(svg)

composite=f'<svg xmlns="http://www.w3.org/2000/svg" width="2820" height="1170" viewBox="0 0 2820 1170"><rect width="2820" height="1170" fill="{BG}"/>'
composite+=txt(70,57,'',40)+ '<text x="60" y="58" font-family="Arial,sans-serif" font-size="38" font-weight="700" fill="#293e48">КОЛОДЕЦ / планировка 02</text>'
composite+='<text x="2760" y="55" text-anchor="end" font-family="Arial,sans-serif" font-size="19" fill="#72838a">ОФОРМЛЕНИЕ ПРИНЯТО · НОВАЯ ТОПОЛОГИЯ К ОБСУЖДЕНИЮ</text>'
for i,s in enumerate(all_bodies):
    composite+=txt(480+i*930,113,f'0{i+1} / {NAMES[i]}',24,COLORS[i],700)
    s=s.replace('closed',f'closed{i}').replace('mesh',f'mesh{i}')
    composite+=f'<g transform="translate({30+i*930} 150)">{s}</g>'
composite+='<g font-family="Arial,sans-serif" font-size="19" fill="#405762"><text x="60" y="1109">Штриховка — закрытые кабинеты · Белая комната с разрывами стен — проходная · Цветной пунктир — варианты маршрута</text><text x="60" y="1141">Голубой — стекло · Оранжевый пунктир — открытое окно · Коричневая черта — закрытая дверь · Д / Р / Э — дробовик / ракетница / резак · Б / У / D / ♥ — броня / скорость / урон / лечение</text></g></svg>'
(ROOT/'floor-plans.svg').write_text(composite)

content=''
for i,(title,desc) in enumerate(DESCRIPTIONS):
    content+=f'<article id="f{i+1}"><h2 style="color:{COLORS[i]}">0{i+1} · {NAMES[i]}</h2><div class="layout"><img src="floor-{i+1}.svg" alt="План этажа {i+1}"><div><h3>{title}</h3><p>{desc}</p><p>Штриховка — недоступные кабинеты с отдельными закрытыми дверями. Белый блок с проёмами — игровое помещение, которое можно пересечь. Пунктир показывает альтернативные пути, а не навигацию, проверенную в движке.</p></div></div></article>'
html='''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Колодец — планировка 02</title><style>body{margin:0;background:#f1f5f3;color:#293e48;font:17px/1.6 system-ui,sans-serif}main{max-width:1400px;margin:auto;padding:36px}h1{font-size:52px;letter-spacing:-2px;margin:10px 0}h2{font-size:28px}h3{font-size:21px}article,section{background:white;padding:28px;border:1px solid #d6e1dc;border-radius:18px;margin:24px 0}.layout{display:grid;grid-template-columns:2fr 1fr;gap:28px;align-items:start}img{width:100%;border-radius:10px}a{color:#287eaf}nav{display:flex;gap:18px;flex-wrap:wrap}.meta{color:#6b8087;font-size:14px}.notice{border-left:4px solid #328358;padding-left:16px}.row{display:grid;grid-template-columns:1fr 1fr;gap:24px}td,th{text-align:left;vertical-align:top;padding:12px;border-bottom:1px solid #dde5e1}table{border-collapse:collapse;width:100%;font-size:15px}@media(max-width:900px){main{padding:18px}.layout,.row{grid-template-columns:1fr}article,section{padding:16px}}</style><main><p class="meta">Star Tournament · 11.10.2026 · обсуждение архитектуры</p><h1>Колодец / планировка 02</h1><p class="notice">Пользователь принял визуальный концепт. В этой версии исправлены лифты, вертикальные связи вынесены за квадрат, расширены проходы и усложнена внутренняя топология. Это концепт, а не реализация или приёмка Player.</p><nav><a href="#f1">01 Биология</a><a href="#f2">02 Физика</a><a href="#f3">03 Электроника</a><a href="#lift">Лифты</a><a href="#rules">Принципы</a></nav>'''+content+'''
<section id="lift"><h2>Лифты: исправленное поведение</h2><p><b>Начав рейс, лифт доезжает до противоположного крайнего этажа, даже если все пассажиры спрыгнули.</b> На втором этаже можно выйти на ходу; это не отменяет рейс и не останавливает платформу.</p><p>Северная платформа изначально на третьем этаже, южная на первом. Посадка запускает задержку ровно 1 секунда. Внизу рейс направлен вверх, наверху — вниз. После прибытия лифт стоит; для следующего рейса предлагается новая посадка после освобождения платформы, чтобы стоящий пассажир не запускал бесконечное движение туда-сюда. Это уточнение повторного запуска ещё не согласовано отдельно.</p><p>Лифтовые башни стоят снаружи на севере и юге. Широкая площадка соединяется с отдельным холлом каждого этажа; шахта и направляющие не занимают коридор и не заслоняют колодец. Путь к мосту проходит через холл, а не через платформу. В первом приближении можно пробовать платформу около 6×6 м; окончательный размер ещё не выбран.</p><p>Боты учитывают положение, направление и время прибытия платформы, входят целиком, едут на движущейся опоре и могут выйти на втором этаже. Если лифт уехал на другой край и не возвращается, бот выбирает лестницу. Сход пассажиров не сбрасывает цель рейса. Snapshot/restore хранит цель, высоту, направление и таймер; Pause останавливает симуляцию, Repeat восстанавливает исходные положения.</p></section>
<section id="rules"><h2>Новая организация помещений</h2><div class="row"><div><h3>Пять типов пространства</h3><p>1. Проходной зал на всю глубину крыла.<br>2. Широкий коридор вдоль окон, закрытые кабинеты снаружи.<br>3. Широкий внешний коридор, кабинеты со стороны колодца.<br>4. Два обхода вокруг лабораторного блока и проход через его двери.<br>5. Угловой холл с несколькими выходами, смещёнными проёмами и крупным островом оборудования.</p><p>Каждый этаж сочетает типы по-разному. Узлы маршрута становятся залами с выбором выхода. Закрытые кабинеты сами не считаются маршрутами: дополнительные связи создают реальные проёмы проходных лабораторий и обходы.</p></div><div><h3>Размеры и читаемость</h3><p>Вместо прежних проходов около 3 м предлагается начинать с коридоров 5–6 м и залов шириной 10–14 м. Это диапазоны для следующего блокинга, не утверждённые параметры. Масштаб корпуса и колодца нужно подбирать вместе с временем обхода; схема не является размерным чертежом.</p><p>Сложность даёт выбор пути, а не тесноту: без частых узких дверей, длинных глухих тупиков и мебели на поворотах. Высокое оборудование стоит у стен, рабочие острова имеют широкие обходы. Точки возрождения будут переразмещены после проверки обзора в новой геометрии; прежние S-маркеры не переносились автоматически.</p></div></div></section>
<section><h2>Сохранённые игровые опоры</h2><table><tr><th>Этаж</th><th>Оружие и бонусы</th></tr><tr><td>01</td><td>Дробовик в мокрой лаборатории, ракетница в юго-западной приёмной, броня в саду, ускорение на восточном маршруте.</td></tr><tr><td>02</td><td>Урон на мосту север–юг, ракетница в восточной части, резак в юго-западной приборной, броня в презентационном зале.</td></tr><tr><td>03</td><td>Лечение в центре Н, резак на западе, дробовик в юго-восточной ремонтной, ускорение на юго-западе.</td></tr></table><p>Число и виды предметов сохранены из первого предложения, их точки подстроены под новые помещения. Винтовка стартовая. Мосты и сад сохраняют роль быстрых открытых переходов. Точные anchors, прострелы, безопасные спуски и спавны требуют проверки в игровом блокинге.</p></section>
<section><h2>Принятое оформление</h2><p>Белая яркая технологичная лаборатория; зелёные биоразработки, синяя физика, жёлтая электроника. Полосы и надписи соответствуют этажу. Окна чередуют непробиваемое стекло и открытые проёмы.</p><img src="../floor-interiors.png" alt="Принятое направление интерьеров"><p class="meta">Художественный эскиз иллюстрирует материалы и атмосферу. Актуальные помещения и внешние башни задаются планировкой 02; предыдущий разрез не является её чертежом.</p></section>
<section><h2>Следующий шаг</h2><p>Обсудить эту топологию и уточнить повторный запуск лифта после прибытия с пассажиром. Затем, по отдельной команде на реализацию, оформить согласованный scope и проверить игровой блокинг. Код, OpenSpec, Unity-процессы и сборки здесь не менялись.</p><p class="meta">Сохранена история: <a href="../index.html">первый концепт</a>. Авторизована переработка концепта; новая планировка ещё не принята пользователем.</p></section></main></html>'''
(ROOT/'index.html').write_text(html)
(ROOT/'decisions.json').write_text(json.dumps({'revision':2,'date':'2026-10-11','accepted_by_user':['visual direction of concept 01','elevator completes its trip even after passengers leave','stairs west/east and lifts north/south outside square','wide spacious elevators','wider corridors','mixed through halls and corridor/closed office wings','corridors both beside atrium windows and outside away from windows','topology more complex than one perimeter loop'],'proposed_not_accepted':['exact v2 room topology and pickup positions','5–6 m corridors, 10–14 m halls, ~6×6 m lift platform','rearm after terminal arrival requires empty platform and new boarding'],'scope':'concept artifacts only; no code or OpenSpec','verification':'SVG render and conceptual spatial inspection, no gameplay validation'},ensure_ascii=False,indent=2)+'\n')
print('Generated three floor SVGs, composite, HTML and decision record.')
