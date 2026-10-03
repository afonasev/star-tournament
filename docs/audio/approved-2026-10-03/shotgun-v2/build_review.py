from pathlib import Path
import array,math,wave,json,base64,random
root=Path(__file__).parent; R=48000

def read(p):
 with wave.open(str(p),'rb') as w:return [x/32768 for x in array.array('h',w.readframes(w.getnframes()))]
def pitch(a,rate):
 n=int((len(a)-1)/rate)
 return [a[int(i*rate)]*(1-(i*rate%1))+a[int(i*rate)+1]*(i*rate%1) for i in range(n)]
def low(a,hz):
 k=1-math.exp(-2*math.pi*hz/R);v=0;out=[]
 for x in a:v+=k*(x-v);out.append(v)
 return out
def norm(a):
 rms=math.sqrt(sum(x*x for x in a[:12000])/12000)
 return [x/max(rms,1e-8) for x in a]
def add(dst,src,gain,delay=0):
 start=round(delay*R)
 for i,x in enumerate(src[:len(dst)-start]):dst[start+i]+=x*gain
sources=[[norm(read(root.parent/'shotgun'/f'variant-{k}-{j}.wav')) for j in range(2)] for k in (1,2,3)]
banks=[];stats=[]
for b in range(4):
 bank=[]
 for j in range(1 if b==0 else 2):
  if b==0:a=read(root.parent/'shotgun'/'variant-0-0.wav')
  else:
   a=[0.]*(int(.85*R)); primary=sources[(b-1)%3][j];second=sources[b%3][1-j]
   rate={1:.83,2:.68,3:.56}[b]
   core=norm(pitch(primary,rate));body=norm(low(core,450 if b<3 else 300))
   # Two barrels coalesce into a single heavy transient, not two spaced shots.
   add(a,core,.5);add(a,norm(pitch(second,rate*.96)),.40,{1:.009,2:.016,3:.023}[b])
   add(a,body,{1:.40,2:.55,3:.80}[b])
   # Short recorded broadband fragments add the ragged blast texture.
   rng=random.Random(320+b*10+j)
   for q in range({1:3,2:7,3:4}[b]):
    grain=pitch(second[:int(.038*R)],rng.uniform(.8,1.25));smooth=low(grain,650)
    grain=[(x-smooth[i])*math.exp(-i/(R*.017)) for i,x in enumerate(grain)]
    add(a,grain,{1:.10,2:.16,3:.11}[b],.012+q*.007)
   # Shaped outdoor tail / early room reflections, never a periodic echo.
   tail=low(core,1600 if b<3 else 850)
   for delay,gain in [(0.039,.11),(.071,.08),(.113,.05)]:add(a,tail,gain*(1.5 if b==3 else 1),delay)
   # Broad soft compression adds density while retaining attack.
   drive={1:.85,2:1.20,3:.95}[b]
   a=[math.tanh(x*drive) for x in a]
   duration={1:.40,2:.51,3:.70}[b]
   for i in range(len(a)):
    t=i/R
    a[i]*=min(1,i/30)*math.exp(-max(0,t-.035)/({1:.105,2:.14,3:.21}[b]))
    if t>duration-.06:a[i]*=max(0,(duration-t)/.06)
   a=a[:int(duration*R)]
  rms=math.sqrt(sum(x*x for x in a[:12000])/12000);gain=min(.12/max(rms,1e-9),.83/max(abs(x) for x in a));a=[x*gain for x in a]
  p=root/f'variant-{b}-{j}.wav'
  with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(x*32767) for x in a]).tobytes())
  bank.append('data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode())
  stats.append({'file':p.name,'peak':max(abs(x) for x in a),'rms_250ms':math.sqrt(sum(x*x for x in a[:12000])/12000),'duration':len(a)/R})
 banks.append(bank)
(root/'index.html').write_text((root/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(root/'manifest.json').write_text(json.dumps({'sources':'../shotgun/manifest.json','license':'CC0 source recordings; original layered edits','recipe':'Recorded double blast, pitch reduction, filtered body, recorded crackle grains, early reflections, saturation and tail shaping. No game audio copied.','cadence':.7,'clips':stats},indent=2))
print(json.dumps(stats,indent=2))
