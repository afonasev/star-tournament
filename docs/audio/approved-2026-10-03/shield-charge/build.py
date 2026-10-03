from pathlib import Path
import random,hashlib
r=Path(__file__).parent
exec((r.parent/'death-v2/build.py').read_text().split('hit=read(')[0])
rng=random.Random(918);bank=[];stats=[]
for k in range(4):
 if k==0:x=read(r.parent/'pickup/pickup-0.wav')
 else:
  D={1:.68,2:.86,3:.62}[k];N=int(R*D);noise=[rng.uniform(-1,1) for _ in range(N)];air=low(noise,2800);bass=low(noise,180);x=[];phase=0
  for i in range(N):
   t=i/R;u=t/D
   # Swelling energy, then a short field-lock decay. No hard hit transient.
   swell=math.sin(math.pi*min(1,u/.7)/2)**1.3
   env=swell if u<.7 else math.exp(-(u-.7)*19)
   freq={1:160+430*u,2:95+190*u,3:240+520*u}[k];phase+=2*math.pi*freq/R
   if k==1:v=.35*math.sin(phase)+.16*math.sin(phase*2.006)+air[i]*.7
   elif k==2:v=.42*math.sin(phase)+.18*math.sin(phase*1.005)+bass[i]*2+air[i]*.3
   else:v=.22*math.sin(phase)+.15*math.sin(phase*1.5)+air[i]*1.1
   x.append(v*env*min(1,(N-i)/(R*.05)))
  p=max(map(abs,x));x=[v*.56/p for v in x]
 p=r/f'charge-{k}.wav';bank.append(write(p,x));stats.append({'variant':k,'duration':len(x)/R,'peak':max(map(abs,x)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(bank)))
(r/'manifest.json').write_text(json.dumps({'event':'shield-pickup-charge','source':'Original synthesized charge layers; variant 0 is existing pickup reference','game_integration':False,'clips':stats},indent=2))
print(stats)
