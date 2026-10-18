"""Compose raw Editor evidence; no colour/exposure edits."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parent
FONT='/System/Library/Fonts/Supplemental/Arial.ttf'
title=ImageFont.truetype(FONT,28);label=ImageFont.truetype(FONT,20)
views=[('room-0','01 · Биология'),('room-1','02 · Физика'),('room-2','03 · Электроника'),('atrium','Атриум')]
for variant,text in [('before','До · Well v4'),('after','После · выбранный дневной свет · Well v5')]:
    canvas=Image.new('RGB',(1600,1054),'#171d24');draw=ImageDraw.Draw(canvas)
    draw.text((18,12),text,font=title,fill='white')
    draw.text((18,49),'Штатный runtime · реальный Editor render · без review-only поправок',font=label,fill='#c3cbd5')
    for i,(name,caption) in enumerate(views):
        x,y=i%2*800,90+i//2*482;draw.text((x+16,y+5),caption,font=label,fill='white')
        with Image.open(ROOT/variant/(name+'.png')) as source:canvas.paste(source.resize((800,450),Image.Resampling.LANCZOS),(x,y+32))
    canvas.save(ROOT/(variant+'-board.jpg'),quality=94,subsampling=0)
canvas=Image.new('RGB',(1920,580),'#171d24');draw=ImageDraw.Draw(canvas)
for i,(variant,text) in enumerate([('before','До'),('after','После')]):
    draw.text((i*960+16,7),text,font=title,fill='white')
    with Image.open(ROOT/variant/'atrium.png') as source:canvas.paste(source.resize((960,540),Image.Resampling.LANCZOS),(i*960,40))
canvas.save(ROOT/'atrium-before-after.jpg',quality=94,subsampling=0)
files=sorted((ROOT/'full-visual').glob('*.png'))
for page,start in enumerate(range(0,len(files),12)):
    canvas=Image.new('RGB',(1800,1496),'#171d24');draw=ImageDraw.Draw(canvas)
    for i,file in enumerate(files[start:start+12]):
        x,y=i%3*600,i//3*374;draw.text((x+8,y+4),file.stem,font=label,fill='white')
        with Image.open(file) as source:canvas.paste(source.resize((600,338),Image.Resampling.LANCZOS),(x,y+36))
    canvas.save(ROOT/f'full-review-{page+1}.jpg',quality=92,subsampling=0)
