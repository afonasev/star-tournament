from pathlib import Path
import array,subprocess,math,wave,json,base64
r=Path(__file__).parent;R=32000;source=r.parent/'shield/source/Audio'
def read(p):return list(array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(R),'-f','f32le','-'])))
def norm(a):
 peak=max(map(abs,a));start=next(i for i,v in enumerate(a) if abs(v)>.03*peak);a=a[max(0,start-16):];rms=math.sqrt(sum(v*v for v in a[:3200])/3200);return [v/max(rms,1e-8) for v in a]
def low(a,hz):
 out=[];v=0;k=1-math.exp(-2*math.pi*hz/R)
 for x in a:v+=k*(x-v);out.append(v)
 return out
def pitch(a,k):return [a[int(i*k)] for i in range(int(len(a)/k))]
def write(p,a):
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(v*32767) for v in a]).tobytes())
 return 'data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()
banks=[];stats=[]
for k in range(5):
 if k in (0,4):
  old=0 if k==0 else 3
  banks.append(['data:audio/wav;base64,'+base64.b64encode((r.parent/'shield-v2'/f'{kind}-{old}.wav').read_bytes()).decode() for kind in ['single','burst']]);continue
 shots=[]
 for j in range(2):
  punch=norm(read(source/f'impactPunch_heavy_00{j}.ogg'));soft=norm(read(source/f'impactSoft_heavy_00{j}.ogg'));plate=norm(read(source/f'impactPlate_light_00{j}.ogg'));mid=norm(read(source/f'impactPunch_medium_00{j}.ogg'))
  if k==1:layers=[(low(punch,2100),.8,0),(low(soft,1400),.35,.003)];length=.19
  elif k==2:layers=[(low(pitch(punch,.80),1500),.9,0),(low(pitch(soft,.85),1700),.65,.007)];length=.23
  else:layers=[(low(mid,2600),.8,0),(low(plate,1800),.24,.004),(low(soft,1700),.40,.009)];length=.19
  x=[0.]*(int(length*R))
  for a,g,d in layers:
   for i,v in enumerate(a[:len(x)-int(d*R)]):x[i+int(d*R)]+=g*v*math.exp(-i/(R*(.038 if a is layers[-1][0] else .065)))
  for i in range(len(x)):x[i]*=min(1,i/12)*min(1,(len(x)-i)/(R*.03))
  rms=math.sqrt(sum(v*v for v in x[:4800])/4800);gain=min(.085/max(rms,1e-9),.6/max(map(abs,x)));x=[v*gain for v in x];shots.append(x);write(r/f'hit-{k}-{j}.wav',x)
 burst=[0.]*(5*3840+max(map(len,shots)))
 for j in range(6):
  for i,v in enumerate(shots[j%2]):burst[j*3840+i]+=v
 assert max(map(abs,burst))<1
 banks.append([write(r/f'single-{k}.wav',shots[0]),write(r/f'burst-{k}.wav',burst)])
 stats.append({'variant':k,'duration':len(shots[0])/R,'series_peak':max(map(abs,burst))})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'source':'Kenney Impact Sounds, https://kenney.nl/assets/impact-sounds, CC0','processing':'physical foley layers, shortened damped envelopes, lowpass; variant 2 lowered pitch; first150ms normalization','shield_references':'shield-v2 0 and 3, previews reused byte-for-byte','scope':'timbre preview only; spatial requirements recorded separately in ../audio-requirements.json','clips':stats},indent=2))
print(stats)
