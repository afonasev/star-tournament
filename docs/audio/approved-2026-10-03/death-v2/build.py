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
  x=read(r.parent/'death/death-3.wav')
 else:
  x=[0.]*(int(1.3*R))
  soft=norm(read(src/'impactSoft_heavy_000.ogg'));soft2=norm(read(src/'impactSoft_heavy_001.ogg'));cloth=norm(read(src/'footstep_carpet_000.ogg'));cloth2=norm(read(src/'footstep_carpet_001.ogg'))
  # Soft padded suit: no metal, plate or bell source in these candidates.
  fall=norm(low(low(pitch(soft,.72),1450),1450));settle=norm(low(pitch(soft2,.85),1100))
  add(x,low(cloth,2100),.10,.27,.22)
  add(x,fall,.58,.48,.37);add(x,settle,.27,.565,.24);add(x,low(cloth2,1800),.10,.64,.25)
  file={1:'1yell1.wav',2:'2yell1.wav',3:'1yell2.wav'}[k]
  voice=norm(read(r.parent/'death/source/yelling sounds'/file));voice=pitch(voice,{1:.92,2:.84,3:.88}[k])
  cutoff={1:1350,2:950,3:750}[k]
  voice=low(low(voice,cutoff),cutoff)
  # Reject sub-bass rumble, then normalize the muffled vocal independently.
  sub=low(voice,90);voice=[v-sub[i] for i,v in enumerate(voice)];peak=max(map(abs,voice));voice=[v/peak for v in voice]
  duration={1:.48,2:.55,3:.34}[k]
  add(x,voice,{1:.24,2:.24,3:.22}[k],.025,duration)
  write(r/f'voice-{k}.wav',[v*.24*min(1,i/80)*min(1,(min(len(voice),int(duration*R))-i)/(R*.05)) for i,v in enumerate(voice[:int(duration*R)])])
  assert max(map(abs,x))<.90
 combo=[0.]*(int(.25*R)+len(x))
 for i,v in enumerate(hit):combo[i]+=v
 for i,v in enumerate(x):combo[int(.25*R)+i]+=v
 assert max(map(abs,combo))<1
 banks.append([write(r/f'death-{k}.wav',x),write(r/f'hit-death-{k}.wav',combo)]);stats.append({'variant':k,'peak':max(map(abs,x)),'duration':len(x)/R})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'sources':'../death/manifest.json (Kenney + HaelDB, CC0)','reference':'previous death variant 3 unchanged','processing':'two-pole vocal lowpass 1350/950/750Hz, modest pitch reduction, shortened voice; common soft heavy body/cloth fall; no metal sources','voice_files':['1yell1.wav','2yell1.wav','1yell2.wav'],'contact_seconds':.48,'game_integration':False,'clips':stats},indent=2))
print(stats)
