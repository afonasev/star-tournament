from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import sys
root=Path(sys.argv[1]);out=Path(sys.argv[2]);im=Image.new('RGB',(1600,1040),'#181d24');draw=ImageDraw.Draw(im);font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',24)
for i,w in enumerate(['Rifle','Shotgun','RocketLauncher','Cutter']):
 src=Image.open(root/(w+'-fps.png'));print(w,src.size)
 src=src.crop((0,618,1200,1350)).resize((800,488))
 x=(i%2)*800;y=(i//2)*520;im.paste(src,(x,y+32));draw.text((x+12,y+3),w+' | F13 actual FPS',font=font,fill='white')
im.save(out)
im=Image.new('RGB',(1600,800),'#181d24')
for i,w in enumerate(['RocketLauncher','Cutter']):
 for j,sign in enumerate([-1,1]):im.paste(Image.open(root/(w+'-Left-grip-'+str(sign)+'.png')).resize((400,400)),(i*800+j*400,0))
for i,w in enumerate(['Rifle','Shotgun']):
 for j,sign in enumerate([-1,1]):im.paste(Image.open(root/(w+'-Left-grip-'+str(sign)+'.png')).resize((400,400)),(i*800+j*400,400))
im.save(out.with_name(out.stem+'-grips.png'))
im=Image.new('RGB',(1600,1040),'#181d24');draw=ImageDraw.Draw(im)
for i,w in enumerate(['RocketLauncher','Cutter']):
 for j,(folder,label) in enumerate([(Path('docs/evidence/vector-fighter/f12-result/frames'),'F12 before'),(root,'F13 after')]):
  src=Image.open(folder/(w+'-fps.png')).crop((0,618,1200,1350)).resize((800,488));x=j*800;y=i*520;im.paste(src,(x,y+32));draw.text((x+12,y+3),w+' | '+label,font=font,fill='white')
im.save(out.with_name('before-after.png'))
