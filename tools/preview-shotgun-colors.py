from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
im=Image.new('RGB',(1200,540),(18,20,23));d=ImageDraw.Draw(im)
font='/System/Library/Fonts/Supplemental/Arial.ttf'
f=lambda n:ImageFont.truetype(font,n)
d.text((28,24),'Дробь: сравнение цвета',font=f(28),fill='white')
colors=[(255,199,102),(117,77,36),(61,66,71)]
for i,title in enumerate(['Сейчас · светлая','1 · тёмная бронза','2 · графит']):
 x=25+i*390;d.text((x,85),title,font=f(23),fill='white')
 for j,bg in enumerate([(163,163,163),(41,41,41)]):
  y=130+j*165;d.rectangle((x,y,x+370,y+145),fill=bg)
  for k in range(8):
   xx=x+30+(k*41)%230; yy=y+25+(k*17)%100
   d.line((xx,yy,xx+55,yy+(k-4)*2),fill=colors[i],width=4)
d.text((28,490),'Макет цвета на двух фонах — не кадр из игры',font=f(22),fill='white')
out=Path(__file__).resolve().parents[1]/'.local/shotgun-weight'; out.mkdir(parents=True,exist_ok=True); im.save(out/'colors.png')
