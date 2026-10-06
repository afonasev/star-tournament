from pathlib import Path
import hashlib
r=Path(__file__).parent
exec((r.parent/'death-v2/build.py').read_text().split('hit=read(')[0])
audio=Path('/Users/eaafonasev/Projects/star-tournament/unity/Assets/StarTournament/Resources/Audio')
soft=norm(read(src/'impactSoft_heavy_000.ogg'));cloth=norm(read(src/'footstep_carpet_000.ogg'));metal=norm(read(src/'impactPlate_light_001.ogg'))
def tone(x,f,start,d,g):
 for i in range(int(d*R)):
  j=int(start*R)+i
  if j<len(x):
   t=i/R;x[j]+=g*math.sin(2*math.pi*f*t)*min(1,t/.006)*math.exp(-t*7)*min(1,(d-t)/.035)
banks={};stats=[]
for kind in ['spawn','jump','land','move','confirm','back']:
 banks[kind]=[]
 ref=audio/({'spawn':'damage-bonus-spawn.wav','jump':'Movement/jump-0.wav','land':'Movement/land-0.wav','move':'menu_move.wav','confirm':'menu_confirm.wav','back':'menu_back.wav'}[kind])
 for k in range(4):
  if k==0:x=read(ref)
  else:
   D=1.1 if kind=='spawn' else .55 if kind in ['jump','land'] else .34;x=[0.]*int(D*R)
   if kind=='spawn':
    if k==1:
     for f,t,g in [(165,0,.25),(156,.06,.15),(247,.26,.2),(330,.43,.16)]:tone(x,f,t,.55,g)
    elif k==2:
     for f,t,g in [(110,0,.3),(220,0,.16),(146,.26,.26),(207,.26,.18),(294,.52,.23)]:tone(x,f,t,.38,g)
    else:
     for t in [0,.16,.32]:tone(x,140,t,.17,.3);tone(x,199,t,.17,.12)
     tone(x,280,.51,.43,.2)
   elif kind=='jump':
    add(x,low(pitch(cloth,[1.15,.9,1.3][k-1]),[2400,1700,3200][k-1]),.35,0,.22)
    add(x,low(pitch(soft,[1.3,1,1.45][k-1]),900),[.23,.38,.16][k-1],.01,.14)
    if k==3:add(x,low(cloth,1800),.2,.08,.13)
   elif kind=='land':
    add(x,low(pitch(soft,[.95,.69,1.1][k-1]),[1500,950,1800][k-1]),.5,0,.26)
    add(x,low(cloth,2500),[.17,.22,.35][k-1],.04,.2)
    if k==2:add(x,low(pitch(soft,.9),650),.18,.095,.14)
   else:
    if k==1:
     f={'move':360,'confirm':440,'back':310}[kind];tone(x,f,0,.085,.3)
     if kind!='move':tone(x,f*(1.5 if kind=='confirm' else .75),.09,.12,.25)
    elif k==2:
     add(x,low(metal,2000),.3,0,.055);add(x,low(soft,900),.15,.02,.07)
     if kind!='move':add(x,low(pitch(metal,1.3 if kind=='confirm' else .72),1500),.22,.08,.08)
    else:
     f={'move':600,'confirm':520,'back':450}[kind];tone(x,f,0,.065,.23);tone(x,f*1.5,0,.06,.1)
     if kind!='move':tone(x,f*(1.5 if kind=='confirm' else .66),.085,.16,.2)
   peak=max(map(abs,x));x=[v*(.53 if kind not in ['move','confirm','back'] else .38)/peak for v in x]
  p=r/f'{kind}-{k}.wav';banks[kind].append(write(p,x));stats.append({'kind':kind,'variant':k,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'duration':len(x)/R,'peak':max(map(abs,x))})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'status':'preview only; runtime unchanged','reference':'Existing Resources/Audio; movement reference is bank sample 0','sources':'Original synthesized tones, Kenney Impact Sounds CC0: soft heavy 000, carpet 000, plate light 001','clips':stats},indent=2))
assert all(s['peak']<=1 for s in stats)
print('Built six groups, each current reference + three alternatives; no clipping.')
