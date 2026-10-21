from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).parent
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',23)
small=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',18)
source=root/'grip-candidate2';out=Image.new('RGB',(1440,1640),'#171d22');d=ImageDraw.Draw(out)
d.text((12,12),'VECTOR — реальные Unity кадры; кандидат, НЕ defaults; хват не принят',font=font,fill='white')
for i,label in enumerate(['Левая: пальцы','Левая: большой палец','Правая: рукоять','Вид игрока']):d.text((i*360+10,48),label,font=small,fill='#bbc7d2')
for j,w in enumerate(['Rifle','Shotgun','RocketLauncher','Cutter']):
 y=82+j*385;d.text((10,y),w+(' — visual FAIL' if j>1 else ' — прежний хват, без изменений'),font=small,fill='#e7b758' if j>1 else 'white')
 for i,suffix in enumerate(['Left-grip-1','Left-grip--1','Right-grip-1','fps']):
  im=Image.open(source/f'{w}-{suffix}.png').convert('RGB');im.thumbnail((352,352));out.paste(im,(i*360+(360-im.width)//2,y+26+(352-im.height)//2))
out.save(root/'grip-review.png')
out=Image.new('RGB',(1600,930),'#171d22');d=ImageDraw.Draw(out);d.text((15,14),'VECTOR — реальный GLB-кандидат; visual FAIL; игровой GLB не заменён',font=font,fill='white')
for i,(f,label) in enumerate([('model-front.png','Модель целиком'),('torso.png','Торс и плечи')]):
 im=Image.open(root/'model-clearance'/f).convert('RGB');im.thumbnail((790,790));out.paste(im,(i*800+(800-im.width)//2,70));d.text((i*800+14,875),label,font=font,fill='white')
out.save(root/'model-review.png')
