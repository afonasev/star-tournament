"""Contact sheets from unaltered local DCC QA renders. Requires Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[2]
P=ROOT/'docs/evidence/trooper-animation-pipeline'
try:
    font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',20)
    small=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',16)
except OSError:
    font=ImageFont.load_default(size=20)
    small=ImageFont.load_default(size=16)
def sheet(name,items,columns,width=350):
    height=round(width*840/760);rows=(len(items)+columns-1)//columns
    out=Image.new('RGB',(columns*width,rows*(height+34)+70),(19,26,38));draw=ImageDraw.Draw(out)
    draw.text((18,12),'ART_LOLL Trooper | humanoid-v2 | OFFLINE CANDIDATE',font=font,fill='white')
    draw.text((18,40),'Original CC BY 4.0 | Rig and clips adapted locally | Quality / Unity acceptance pending',font=small,fill='#aebed1')
    for i,(file,label) in enumerate(items):
        image=Image.open(P/file).convert('RGB');image.thumbnail((width,height),Image.Resampling.LANCZOS)
        x=(i%columns)*width;y=70+(i//columns)*(height+34)
        out.paste(image,(x+(width-image.width)//2,y));draw.text((x+12,y+height+5),label,font=small,fill='white')
    out.save(P/name)
sheet('clip-overview.png',[(f,'REST / source A-pose') if f=='rig-rest-three-quarter.png' else (f,label) for f,label in [
    ('rig-rest-three-quarter.png',''),('idle-025-three-quarter.png','IDLE 25%'),('walk-025-three-quarter.png','WALK 25%'),('run-025-three-quarter.png','RUN 25%'),('aim-025-three-quarter.png','AIM 25% / articulated grip'),('fire-025-three-quarter.png','FIRE 25% / recoil'),('hit-050-three-quarter.png','HIT 50%'),('death-100-three-quarter.png','DEATH 100%')]],4)
sheet('motion-phases.png',[(f'{clip}-{t:03d}-three-quarter.png',f'{clip.upper()} {t}%') for clip in ['walk','run','death'] for t in [0,25,50,75,100]],5,300)
sheet('source-and-combat.png',[(f,label) for f,label in [('source-front.png','ORIGINAL front'),('source-side.png','ORIGINAL side'),('source-back.png','ORIGINAL back'),('aim-025-side.png','AIM side / articulated grip'),('fire-025-side.png','FIRE side'),('hit-025-side.png','HIT side')]],3)
print('Wrote clip-overview.png, motion-phases.png, source-and-combat.png')

sheet('hands-and-grip.png',[(f'hand-{side}-{clip}-{view}.png',label) for side,clip,view,label in [
 ('left','idle','palm','LEFT / relaxed'),('left','aim','palm','LEFT / support grip'),('left','aim','dorsal','LEFT / glove and knuckles'),
 ('right','idle','palm','RIGHT / relaxed'),('right','aim','palm','RIGHT / trigger index extended'),('right','fire','palm','RIGHT / trigger action')]],3,400)
