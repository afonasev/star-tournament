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
 for j in range(2):
  base=read(root.parent/'shotgun-v2'/f'variant-3-{j}.wav')
  if b==0:a=base[:]
  else:
   a=[0.]*(int(.70*R));add(a,norm(base),.80)
   raw=norm(pitch(sources[2][j],.52));lo=low(raw,110);hi=low(raw,820)
   # Dense low-mid chest impact, with body concentrated behind the attack.
   chest=norm([math.tanh((h-l)*1.8) for h,l in zip(hi,lo)])
   chest=[x*min(1,i/96)*math.exp(-max(0,i/R-.035)/.11) for i,x in enumerate(chest)]
   add(a,chest,{1:.72,2:.38,3:.88}[b],.005)
   # Dry recorded blast attack and a tight second barrel, not a pitched oscillator.
   attack=norm(pitch(sources[0][1-j],.73));l=low(attack,330);h=low(attack,3200)
   attack=[(h[i]-l[i])*math.exp(-i/(R*.07)) for i in range(len(h))]
   add(a,attack,{1:.18,2:.48,3:.48}[b]);add(a,attack,{1:.12,2:.32,3:.30}[b],.012)
   rng=random.Random(734+j)
   for q in range(7 if b>1 else 3):
    grain=pitch(sources[1][j][:int(.035*R)],rng.uniform(.62,.9));l=low(grain,450);h=low(grain,3800)
    grain=[(h[i]-l[i])*math.exp(-i/(R*.011)) for i in range(len(h))]
    add(a,grain,{1:.055,2:.14,3:.13}[b],.018+q*.006)
   # Parallel saturation retains the original transient while filling its body.
   a=[.60*x+.40*math.tanh(x*1.35) for x in a]
   for i in range(len(a)):
    a[i]*=min(1,i/24)*min(1,(len(a)-i)/(R*.06))
  rms=math.sqrt(sum(x*x for x in a[:12000])/12000);gain=min(.12/max(rms,1e-9),.83/max(abs(x) for x in a));a=[x*gain for x in a]
  p=root/f'variant-{b}-{j}.wav'
  with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(x*32767) for x in a]).tobytes())
  bank.append('data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode())
  stats.append({'file':p.name,'peak':max(abs(x) for x in a),'rms_250ms':math.sqrt(sum(x*x for x in a[:12000])/12000),'duration':len(a)/R})
 banks.append(bank)
(root/'index.html').write_text((root/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(root/'manifest.json').write_text(json.dumps({'sources':['../shotgun/manifest.json','../shotgun-v2/manifest.json'],'base':'v2 variant 3','recipe':'Original v2 cannon blast plus band-limited low-mid body, recorded double attack, short recorded crackle grains, parallel saturation. First 250ms RMS matched.','cadence':.7,'clips':stats},indent=2))
print(json.dumps(stats,indent=2))
