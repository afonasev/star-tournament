from pathlib import Path
import array,subprocess,math,wave,json,base64
r=Path(__file__).parent;R=48000

def read(p):return list(array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(R),'-f','f32le','-'])))
def trim(a):
 peak=max(map(abs,a));i=next(i for i,x in enumerate(a) if abs(x)>.04*peak);return a[max(0,i-48):]
def pitch(a,k):return [a[int(i*k)]*(1-i*k%1)+a[int(i*k)+1]*(i*k%1) for i in range(int((len(a)-1)/k))]
def low(a,hz):
 v=0;out=[];k=1-math.exp(-2*math.pi*hz/R)
 for x in a:v+=k*(x-v);out.append(v)
 return out
def norm(a):
 rms=math.sqrt(sum(x*x for x in a[:12000])/12000);return [x/max(rms,.00001) for x in a]
def write(p,a):
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(max(-1,min(1,x))*32767) for x in a]).tobytes())
 return 'data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()
a=norm(trim(read(r/'source/explosion3.ogg')));b=norm(trim(read(r/'source/explosion1.ogg')));d=norm(trim(read(r/'source/distant.wav')));launch=read(r.parent/'rocket/variant-1.wav');banks=[];stats=[]
for i in range(3):
 if i==0:x=a[:]
 elif i==1:
  x=pitch(b,.72);body=low(x,450);x=[v+.75*body[j] for j,v in enumerate(x)]
 else:
  x=d[:];short=low(a,1500)
  for j in range(min(len(x),len(short))):x[j]+=.8*short[j]
 duration=[.8,1.45,1.7][i];x=x[:int(duration*R)]
 for j in range(len(x)):x[j]*=min(1,j/24)*min(1,(len(x)-j)/(R*.22))
 rms=math.sqrt(sum(v*v for v in x[:12000])/12000);g=min(.13/rms,.85/max(map(abs,x)));x=[v*g for v in x]
 combo=[0.]*(int(.8*R)+len(x))
 for j,v in enumerate(launch):combo[j]+=v
 for j,v in enumerate(x):combo[int(.8*R)+j]+=v
 banks.append([write(r/f'explosion-{i+1}.wav',x),write(r/f'sequence-{i+1}.wav',combo)])
 stats.append({'variant':i+1,'duration':len(x)/R,'peak':max(map(abs,x)),'rms250':math.sqrt(sum(v*v for v in x[:12000])/12000)})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'sources':[{'url':'https://opengameart.org/content/explosions-4','author':'EZduzziteh','license':'CC0'},{'url':'https://opengameart.org/content/muffled-distant-explosion','author':'NenadSimic','license':'CC0'},{'url':'https://opengameart.org/content/4-projectile-launches','author':'Michel Baradari','license':'CC BY 3.0','use':'approved launch 1 in sequence previews'}],'processing':'trim, pitch, low-frequency layering, fade and gain; variant 3 blends distant and explosion3','visual_flight_seconds':.8,'game_integration':False,'clips':stats},indent=2))
print(stats)
