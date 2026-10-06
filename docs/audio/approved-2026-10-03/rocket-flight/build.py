from pathlib import Path
import array,math,random,subprocess,wave,json,base64,hashlib
r=Path(__file__).parent;R=32000;N=R*3
raw=list(array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(r.parent/'rocket/source/launches/rlaunch.wav'),'-ac','1','-ar',str(R),'-f','f32le','-'])))
def low(a,h):
 k=1-math.exp(-2*math.pi*h/R);v=0;out=[]
 for x in a:v+=k*(x-v);out.append(v)
 return out
def norm(a):
 p=max(map(abs,a));return [x/p for x in a]
# Sustain the recorded combustion texture using overlapping windowed grains.
rng=random.Random(309);texture=[0.]*N;length=min(len(raw),int(.16*R))
for start in range(-length,N,length//4):
 off=rng.randrange(max(1,len(raw)-length))
 for j in range(length):
  i=start+j
  if 0<=i<N:texture[i]+=raw[off+j]*math.sin(math.pi*j/length)**2
texture=norm(texture);noise=[rng.uniform(-1,1) for _ in range(N)]
broad=norm(low(noise,4200));bass=norm(low(texture,420));mid=norm(low(texture,1900));air=norm([a-b for a,b in zip(broad,low(broad,800))]);bank=[];stats=[]
for k in range(1,4):
 out=[]
 for i in range(N):
  t=i/R
  if k==1:v=.58*mid[i]+.22*bass[i]+.12*air[i]
  elif k==2:v=.35*mid[i]+.5*bass[i]+.08*air[i]+.045*math.sin(2*math.pi*87*t)*(1+.22*math.sin(2*math.pi*19*t))
  else:v=.27*mid[i]+.18*bass[i]+.4*air[i]+.045*math.sin(2*math.pi*(310*t+6*math.sin(t*6)))
  out.append(v)
 out=norm(out);out=[v*.65*min(1,i/(R*.06),(N-1-i)/(R*.12)) for i,v in enumerate(out)]
 p=r/f'flight-{k}.wav'
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(x*32767) for x in out]).tobytes())
 bank.append(base64.b64encode(p.read_bytes()).decode());stats.append({'variant':k,'peak':max(map(abs,out)),'duration':3,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(r/'manifest.json').write_text(json.dumps({'source':'https://opengameart.org/content/4-projectile-launches','author':'Michel Baradari','license':'CC BY 3.0','processing':'overlapping grains of rlaunch.wav; filtered layers, generated airflow and motor tones; shared peak and envelope','game_integration':False,'clips':stats},indent=2))
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(bank)))
print(stats)
