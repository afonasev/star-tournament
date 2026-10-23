from pathlib import Path
import shutil
from PIL import Image,ImageDraw,ImageFont
src=Path('.local/vector-f16/review');dest=Path('docs/evidence/vector-fighter/f16');native=dest/'native'
for p in src.rglob('*'):
 if p.suffix in ['.png','.csv']:
  q=native/p.relative_to(src);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',24)
weapons=['Rifle','Shotgun','RocketLauncher','Cutter']
def sheet(name,items,cols,width,height):
 rows=(len(items)+cols-1)//cols;s=Image.new('RGB',(cols*width,rows*(height+30)),(25,29,33));d=ImageDraw.Draw(s)
 for i,(label,path) in enumerate(items):
  im=Image.open(path);im.thumbnail((width,height));x=i%cols*width;y=i//cols*(height+30);s.paste(im,(x,y+30));d.text((x+6,y+2),label,font=font,fill='white')
 s.save(dest/name)
for w in weapons:
 sheet(w+'-hands.png',[(w+' / '+side+' / '+str(a),native/f'{w}-{side}-grip-{a}.png') for side in ['Left','Right'] for a in [-1,1,2]],3,500,500)
 sheet(w+'-world.png',[(w+' / '+str(a),native/'world'/f'{w}-{a}-fps.png') for a in [0,90,180,270]],4,400,450)
sheet('fps.png',[(w,native/f'{w}-fps.png') for w in weapons],2,600,675)
html=['<!doctype html><meta charset="utf-8"><title>F16 — Orbital gloves on VECTOR</title><style>body{background:#181c20;color:#eee;font:18px system-ui;margin:28px}img{max-width:100%;height:auto}section{margin:32px 0}a{color:#8cf}</style><h1>F16 — прежние кисти на VECTOR</h1><p>Реальные Unity-кадры. На модели игрока и в FPS используются одинаковые перчатки. Диагностические виды полной модели имеют отдельную камеру и заполняющий свет.</p><img src="fps.png">']
for w in weapons:
 html.append(f'<section><h2>{w}</h2><img src="{w}-hands.png"><img src="{w}-world.png">')
 for phase in ['fire','rising','falling','landing']:
  html.append(f'<details><summary>{phase}</summary><img src="native/poses/{w}-{phase}/{w}-fps.png">')
  for side in ['Left','Right']:html.append(f'<img src="native/poses/{w}-{phase}/{w}-{side}-grip-1.png">')
  html.append('</details>')
 html.append('</section>')
(dest/'review.html').write_text('\n'.join(html))
