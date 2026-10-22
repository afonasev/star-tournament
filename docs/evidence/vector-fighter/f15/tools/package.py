from pathlib import Path
import shutil
from PIL import Image,ImageDraw,ImageFont
src=Path('.local/vector-f15/review');dest=Path('docs/evidence/vector-fighter/f15');native=dest/'native';native.mkdir(parents=True,exist_ok=True)
for p in src.rglob('*'):
 if p.suffix in ['.png','.csv']:
  q=native/p.relative_to(src);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
neutral=dest/'neutral';neutral.mkdir(exist_ok=True)
for p in Path('.local/vector-f15/preview-final').glob('*png'):shutil.copyfile(p,neutral/p.name)
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',27)
weapons=['Rifle','Shotgun','RocketLauncher','Cutter']
# Contact sheets only resize/place original captures; no retouching.
for weapon in weapons:
 sheet=Image.new('RGB',(1500,1060),(25,29,33));draw=ImageDraw.Draw(sheet)
 for row,side in enumerate(['Left','Right']):
  for col,angle in enumerate([-1,1,2]):
   im=Image.open(native/f'{weapon}-{side}-grip-{angle}.png');im.thumbnail((500,500));sheet.paste(im,(col*500,row*530+30));draw.text((col*500+8,row*530+2),f'{weapon} / {side} / {angle}',font=font,fill='white')
 sheet.save(dest/f'{weapon}-all-angles.png')
sheet=Image.new('RGB',(1200,1410),(25,29,33));draw=ImageDraw.Draw(sheet)
for i,w in enumerate(weapons):
 im=Image.open(native/f'{w}-fps.png');im.thumbnail((600,675));x=i%2*600;y=i//2*705;sheet.paste(im,(x,y+30));draw.text((x+8,y+2),w,font=font,fill='white')
sheet.save(dest/'fps-overview.png')
html=['<!doctype html><meta charset="utf-8"><title>F15 — Unity hand review</title><style>body{background:#181c20;color:#eee;font:18px system-ui;margin:28px}img{max-width:100%;height:auto}section{margin:32px 0}a{color:#8cf}</style><h1>F15 — руки в перчатках</h1><p>Реальные Unity FPS и крупные планы. Нейтральная геометрическая диагностика отдельно. Художественная приёмка пользователя остаётся открытой.</p><img src="fps-overview.png">']
for w in weapons:
 html.append(f'<section><h2>{w}</h2><a href="native/{w}-fps.png">FPS в полном размере</a><img src="{w}-all-angles.png">')
 for side in ['Left','Right']:
  html.append('<p>'+side+': '+ ' · '.join(f'<a href="native/{w}-{side}-grip-{a}.png">ракурс {a}</a>' for a in [-1,1,2])+'</p>')
 html.append('<h3>Движение и стрельба</h3>')
 for phase in ['fire','rising','falling','landing']:
  html.append(f'<details><summary>{phase}</summary><img src="native/poses/{w}-{phase}/{w}-fps.png">')
  for side in ['Left','Right']:
   html.append(f'<img src="native/poses/{w}-{phase}/{w}-{side}-grip-1.png">')
  html.append('</details>')
 html.append('<details><summary>Нейтральные диагностические ракурсы</summary>')
 for side in ['Left','Right']:
  for angle in [-1,1]:html.append(f'<img src="neutral/{w}-{side}-clay-{angle}.png">')
 html.append('</details></section>')
(dest/'review.html').write_text('\n'.join(html)+'\n')
