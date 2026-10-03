from pathlib import Path
import random,hashlib
r=Path(__file__).parent
exec((r.parent/'death-v2/build.py').read_text().split('hit=read(')[0])
rng=random.Random(408);bank=[];stats=[]
soft=norm(read(src/'impactSoft_heavy_000.ogg'));plate=norm(read(src/'impactPlate_light_000.ogg'))
for k in range(4):
 if k==0:x=read(Path('/Users/eaafonasev/Projects/star-tournament/unity/Assets/StarTournament/Resources/Audio/pickup.wav'))
 else:
  x=[0.]*int(R*.65)
  noise=low([rng.uniform(-1,1) for _ in x],2400)
  if k==1:
   add(x,low(plate,1700),.25,0,.12);add(x,low(soft,700),.28,.045,.18)
  elif k==2:add(x,low(soft,900),.18,0,.15)
  else:add(x,low(soft,700),.22,.17,.2)
  phase=0
  for i in range(len(x)):
   t=i/R
   if k==1:
    e=math.exp(-t*18)*min(1,t/.009);phase+=2*math.pi*(240+80*min(1,t/.15))/R;x[i]+=.16*math.sin(phase)*e
   elif k==2:
    e=math.exp(-t*10)*min(1,t/.018);phase+=2*math.pi*(190+370*(1-math.exp(-t*12)))/R;x[i]+=(.19*math.sin(phase)+.07*math.sin(phase*1.5)+.07*noise[i])*e
   else:
    e=(math.sin(math.pi*t/.24)**2 if t<.24 else math.exp(-(t-.24)*26))*.3
    phase+=2*math.pi*(170+700*min(1,t/.25))/R;x[i]+=(noise[i]*.6+math.sin(phase)*.16)*e
  peak=max(map(abs,x));x=[v*.57/peak for v in x]
 p=r/f'pickup-{k}.wav';bank.append(write(p,x));stats.append({'variant':k,'duration':len(x)/R,'peak':max(map(abs,x)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(r/'manifest.json').write_text(json.dumps({'reference':'Current Unity Resources/Audio/pickup.wav','sources':'Kenney Impact Sounds CC0; original synthesized energy layers','scope':'ordinary pickups, excludes damage bonus; preview only','clips':stats},indent=2))
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(bank)))
print(stats)
