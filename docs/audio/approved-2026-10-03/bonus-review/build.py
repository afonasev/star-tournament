from pathlib import Path
import random,hashlib
r=Path(__file__).parent
exec((r.parent/'death-v2/build.py').read_text().split('hit=read(')[0])
rng=random.Random(111);banks={};stats=[]
for kind in ['weapon','damage','speed','heal','shield']:
 banks[kind]=[]
 for k in range(1,4):
  if kind in ['weapon','shield']:
   p=r.parent/(f'weapon-pickup/pickup-{k}.wav' if kind=='weapon' else f'shield-charge/charge-{k}.wav');banks[kind].append('data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode());continue
  D=.85 if kind=='damage' else .62;N=int(R*D);noise=[rng.uniform(-1,1) for _ in range(N)];air=low(noise,4500);smooth=low(noise,900);x=[0.]*N;phase=0
  for i in range(N):
   t=i/R;u=t/D
   if kind=='damage':
    f=[72+110*u,100+180*u,85+90*u][k-1];phase+=2*math.pi*f/R
    env=math.sin(math.pi*min(1,u/.65)/2) if u<.65 else math.exp(-(u-.65)*15)
    if k==1:v=.32*math.sin(phase)+.15*math.sin(phase*1.06)+smooth[i]*.8
    elif k==2:v=math.tanh(2*(.22*math.sin(phase)+.13*math.sin(phase*1.414)))*.55+air[i]*.13
    else:v=(.26*math.sin(phase)+.2*math.sin(phase*1.5)+.11*math.sin(phase*2.12))*(.75+.25*math.sin(2*math.pi*21*t))+smooth[i]*.3
    x[i]=v*env
   elif kind=='speed':
    phase+=2*math.pi*([330+650*u,430+450*u,260+850*u][k-1])/R
    env=math.sin(math.pi*min(1,u/.55)/2) if u<.55 else math.exp(-(u-.55)*18)
    if k==1:v=.2*math.sin(phase)+.1*math.sin(phase*1.5)+air[i]*.22
    elif k==2:v=.16*math.sin(phase)+.12*math.sin(phase*2)+.08*math.sin(phase*3)+air[i]*.12
    else:v=air[i]*.85+.09*math.sin(phase)+.07*math.sin(phase*1.5)
    x[i]=v*env
   else:
    onset=[.045,.07,.035][k-1];length=[.16,.29,.12][k-1];q=(t-onset)/length
    env=math.sin(math.pi*q)**.7 if 0<q<1 else 0
    x[i]=(air[i]*[.65,.42,.8][k-1]+smooth[i]*.2)*env
   x[i]*=min(1,(N-i)/(R*.045))
  if kind=='heal':
   plate=norm(low(read(src/'impactPlate_light_001.ogg'),[2300,1500,3200][k-1]));soft=norm(low(read(src/'impactSoft_heavy_000.ogg'),1000))
   add(x,plate,.13,0,.055);add(x,soft,.16,[.23,.4,.18][k-1],.10)
   if k==3:add(x,plate,.10,.24,.055)
  peak=max(map(abs,x));x=[v*.56/peak for v in x];p=r/f'{kind}-{k}.wav';banks[kind].append(write(p,x));stats.append({'kind':kind,'variant':k,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'duration':D,'peak':max(map(abs,x))})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'status':'preview only; no runtime changes','sources':'Generated energy/air textures. Healing mechanism layers: Kenney Impact Sounds CC0. Weapon and shield: existing sibling manifests.','clips':stats},indent=2))
print('Built 9 new clips plus 6 existing weapon/shield clips; all new peaks 0.56.')
