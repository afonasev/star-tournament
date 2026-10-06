from pathlib import Path
import array,math,wave,json,base64,subprocess
r=Path(__file__).parent;R=32000

def read(p):return list(array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(R),'-f','f32le','-'])))
def low(a,hz):
 k=1-math.exp(-2*math.pi*hz/R);v=0;out=[]
 for x in a:v+=k*(x-v);out.append(v)
 return out
def norm(a):
 rms=math.sqrt(sum(v*v for v in a)/len(a));return [v/max(rms,1e-8) for v in a]
def pitch(a,k):return [a[int(i*k)] for i in range(int(len(a)/k))]
def write(p,a):
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(v*32767) for v in a]).tobytes())
 return 'data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()
hum=norm(read(r.parent/'cutter/source/hum.wav')[7*R:]);banks=[];stats=[]
for k in range(4):
 shots=[]
 for j in range(2 if k else 1):
  if k==0:x=read(Path('/Users/eaafonasev/Projects/star-tournament/unity/Assets/StarTournament/Resources/Audio/hit.wav'))
  else:
   duration={1:.25,2:.19,3:.40}[k];N=int(duration*R)
   punch=norm(read(r/f'source/Audio/impactPunch_heavy_00{j}.ogg'));bell=norm(pitch(read(r/f'source/Audio/impactBell_heavy_00{j}.ogg'),.57))
   body=low(punch,850);h=hum[j*4000:j*4000+N];soft=low(h,700);x=[]
   for i in range(N):
    t=i/R;impact=(body[i] if i<len(body) else 0)*math.exp(-t/.04)
    if k==1:v=.55*impact+.8*soft[i]*math.exp(-t/.065)+.22*math.sin(2*math.pi*(270*t-90*t*t))*math.exp(-t/.07)
    elif k==2:v=.25*impact+1.0*(h[i]-soft[i])*math.exp(-t/.043)*(0.7+.3*math.sin(2*math.pi*87*t))+.24*h[i]*math.exp(-t/.055)
    else:v=.30*impact+.48*bell[i%len(bell)]*math.exp(-t/.105)+.55*soft[i]*math.exp(-t/.14)
    x.append(v*min(1,i/16)*min(1,(N-i)/(R*.03)))
  rms=math.sqrt(sum(v*v for v in x[:4800])/4800);gain=min(.085/max(rms,1e-9),.6/max(map(abs,x)));x=[v*gain for v in x];shots.append(x)
  write(r/f'hit-{k}-{j}.wav',x)
 burst=[0.]*(5*3840+max(map(len,shots)))
 for j in range(6):
  for i,v in enumerate(shots[j%len(shots)]):burst[j*3840+i]+=v
 peak=max(map(abs,burst));assert peak<1,(k,peak)
 banks.append([write(r/f'single-{k}.wav',shots[0]),write(r/f'burst-{k}.wav',burst)])
 stats.append({'variant':k,'single_duration':len(shots[0])/R,'burst_peak':peak,'variants':len(shots)})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'sources':[{'author':'Kenney','url':'https://kenney.nl/assets/impact-sounds','license':'CC0'},{'author':'Hansjörg Malthaner','url':'https://opengameart.org/content/force-field-electric-hum','attribution':'https://opengameart.org/users/varkalandar','license':'CC BY 4.0'}],'processing':'impact and electrical layers, filtering, pitch, envelopes; variant 1 added short resonant tone; first 150ms level matching','series_interval':.12,'series_hits':6,'scope':'shield-hit only; body-hit remains separate; game unchanged','clips':stats},indent=2))
(r.parent/'hit-direction.json').write_text(json.dumps({'actors':'humans in armor with energy shields, not robots','events':['shield hit','body hit'],'user_correction':'нужен звук попадания по щиту, звук попадания по телу','status':'shield sound selection in progress'},ensure_ascii=False,indent=2))
print(stats)
