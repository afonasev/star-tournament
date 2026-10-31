"""Authored rounded monoline numerals; no external font or font licence required.
Run with Pillow. Atlas cells are immutable UV layout, not gameplay tuning.
"""
from pathlib import Path
from PIL import Image, ImageDraw
S=4; W=64; H=128
im=Image.new('L',(W*10*S,H*S)); d=ImageDraw.Draw(im)
def stroke(i, pts):
    p=[((i*W+x)*S,y*S) for x,y in pts]; width=7*S
    d.line(p,fill=255,width=width,joint='curve')
    r=width/2
    for x,y in (p[0],p[-1]):d.ellipse((x-r,y-r,x+r,y+r),fill=255)
def path(i,commands):
    pts=[]; cur=None
    for op,v in commands:
        if op=='M':cur=v;pts.append(v)
        elif op=='L':cur=v;pts.append(v)
        else:
            start=cur;control,end=v
            for j in range(1,17):
                t=j/16;pts.append(((1-t)**2*start[0]+2*(1-t)*t*control[0]+t*t*end[0],(1-t)**2*start[1]+2*(1-t)*t*control[1]+t*t*end[1]))
            cur=end
    stroke(i,pts)
def rounded(i,box,r=12):
    x,y,a,b=box
    d.rounded_rectangle(((i*W+x)*S,y*S,(i*W+a)*S,b*S),radius=r*S,outline=255,width=7*S)
rounded(0,(10,20,54,108),15)
stroke(1,[(20,34),(33,22),(33,106)]);stroke(1,[(19,106),(48,106)])
path(2,[('M',(12,36)),('Q',((12,20),(32,20))),('Q',((54,20),(54,38))),('Q',((54,49),(42,60))),('L',(15,87)),('Q',((10,93),(10,106))),('L',(54,106))])
path(3,[('M',(12,24)),('L',(37,24)),('Q',((54,24),(54,43))),('Q',((54,62),(35,62))),('L',(27,62)),('M',(35,62)),('Q',((54,62),(54,84))),('Q',((54,106),(35,106))),('L',(12,106))])
stroke(4,[(45,106),(45,22),(10,79),(55,79)])
path(5,[('M',(53,22)),('L',(13,22)),('L',(13,61)),('L',(34,61)),('Q',((54,61),(54,83))),('Q',((54,106),(34,106))),('L',(12,106))])
path(6,[('M',(50,22)),('L',(33,22)),('Q',((11,22),(11,48))),('L',(11,85)),('Q',((11,106),(32,106))),('Q',((54,106),(54,84))),('Q',((54,62),(32,62))),('L',(12,62))])
stroke(7,[(10,22),(54,22),(23,106)])
rounded(8,(10,20,54,66),13);rounded(8,(10,61,54,108),13)
path(9,[('M',(14,106)),('L',(32,106)),('Q',((54,106),(54,82))),('L',(54,43)),('Q',((54,22),(32,22))),('Q',((10,22),(10,43))),('Q',((10,64),(32,64))),('L',(53,64))])
im=im.resize((W*10,H),Image.Resampling.LANCZOS)
p=Path(__file__).resolve().parents[2]/'unity/Assets/StarTournament/Resources/ShotgunDigits.png'
im.save(p)
print(p)
