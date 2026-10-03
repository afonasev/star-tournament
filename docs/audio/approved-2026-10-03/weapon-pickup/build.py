from pathlib import Path
import hashlib
r=Path(__file__).parent
exec((r.parent/'death-v2/build.py').read_text().split('hit=read(')[0])
plate=norm(read(src/'impactPlate_light_000.ogg'));plate2=norm(read(src/'impactPlate_light_001.ogg'));soft=norm(read(src/'impactSoft_heavy_000.ogg'));cloth=norm(read(src/'footstep_carpet_000.ogg'))
bank=[];stats=[]
for k in range(4):
 if k==0:x=read(r.parent/'pickup/pickup-0.wav')
 else:
  x=[0.]*int(R*.6)
  if k==1:
   add(x,low(plate,5500),.58,0,.12);add(x,low(pitch(plate2,1.16),3700),.3,.065,.1);add(x,low(soft,650),.17,.015,.13)
  elif k==2:
   add(x,low(pitch(plate,.66),3700),.58,0,.18);add(x,low(pitch(plate2,.82),3000),.42,.12,.14);add(x,low(soft,850),.4,.026,.2);add(x,low(plate,2500),.12,.205,.085)
  else:
   add(x,low(cloth,2800),.3,0,.17);add(x,low(pitch(plate2,.92),4000),.45,.035,.13);add(x,low(soft,1000),.32,.07,.18);add(x,low(plate,2400),.18,.175,.095)
  peak=max(map(abs,x));x=[v*.6/peak for v in x]
 p=r/f'pickup-{k}.wav';bank.append(write(p,x));stats.append({'variant':k,'duration':len(x)/R,'peak':max(map(abs,x)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(bank)))
(r/'manifest.json').write_text(json.dumps({'event':'weapon-pickup','reference':'../pickup/pickup-0.wav','sources':{'url':'https://kenney.nl/assets/impact-sounds','author':'Kenney','license':'CC0','files':['impactPlate_light_000.ogg','impactPlate_light_001.ogg','impactSoft_heavy_000.ogg','footstep_carpet_000.ogg']},'processing':'Layered recorded metal, soft impacts and cloth; pitch, lowpass, cut and fades; no energy tones','game_integration':False,'clips':stats},indent=2))
assert all(s['peak']<1 for s in stats)
print(stats)
