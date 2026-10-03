from pathlib import Path
import subprocess,array,math,wave,base64,json
r=Path(__file__).parent;R=32000;src=r.parent/'shield/source/Audio'
def read(p):return list(array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(R),'-f','f32le','-'])))
def trim(a):
 peak=max(map(abs,a));i=next(i for i,v in enumerate(a) if abs(v)>.025*peak);return a[max(0,i-20):]
def norm(a):
 a=trim(a);peak=max(map(abs,a));return [v/max(peak,1e-8) for v in a]
def low(a,hz):
 k=1-math.exp(-2*math.pi*hz/R);v=0;o=[]
 for x in a:v+=k*(x-v);o.append(v)
 return o
def pitch(a,k):return [a[int(i*k)] for i in range(int(len(a)/k))]
def add(out,a,g,t,duration=None):
 a=a[:int(duration*R)] if duration else a
 for i,v in enumerate(a[:len(out)-int(t*R)]):out[int(t*R)+i]+=v*g*min(1,i/24)*min(1,(len(a)-i)/(R*.045))
def write(p,a):
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(v*32767) for v in a]).tobytes())
 return 'data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()
hit=read(r.parent/'body-hit/hit-2-0.wav');banks=[];stats=[]
for k in range(4):
 if k==0:
  x=read(Path('/Users/eaafonasev/Projects/star-tournament/unity/Assets/StarTournament/Resources/Audio/death.wav'));x=[v*.5 for v in x]
 else:
  x=[0.]*(int(1.25*R));soft=norm(read(src/'impactSoft_heavy_000.ogg'));punch=norm(read(src/'impactPunch_heavy_001.ogg'));plate=norm(read(src/'impactPlate_light_000.ogg'))
  if k==1:
   add(x,low(plate,1400),.07,.12,.13);add(x,low(soft,2400),.70,.48,.30);add(x,low(punch,1100),.26,.59,.19)
  else:
   add(x,low(plate,1900),.10,.13,.16);add(x,low(pitch(soft,.8),1900),.85,.48,.39);add(x,low(pitch(punch,.82),1300),.40,.56,.27);add(x,low(plate,1500),.13,.69,.20)
  if k==3:
   voice=norm(read(r/'source/yelling sounds/2yell1.wav'));add(x,low(voice,3800),.38,.025,.68)
  peak=max(map(abs,x));x=[v*.65/peak for v in x]
 combo=[0.]*(int(.25*R)+len(x))
 for i,v in enumerate(hit):combo[i]+=v
 for i,v in enumerate(x):combo[int(.25*R)+i]+=v
 assert max(map(abs,combo))<1
 banks.append([write(r/f'death-{k}.wav',x),write(r/f'hit-death-{k}.wav',combo)]);stats.append({'variant':k,'peak':max(map(abs,x)),'duration':len(x)/R})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'sources':[{'author':'Kenney','url':'https://kenney.nl/assets/impact-sounds','license':'CC0'},{'author':'HaelDB','url':'https://opengameart.org/content/male-gruntyelling-sounds','license':'CC0 selected option','file':'2yell1.wav'}],'processing':'foley layering, filtered armor movement, body contact at .48s, secondary settling; variant 3 trimmed voice; current gain .5','note':'schematic collapse timing only, not game animation; chosen body hit precedes death by .25 seconds in combo','clips':stats},indent=2))
print(stats)
