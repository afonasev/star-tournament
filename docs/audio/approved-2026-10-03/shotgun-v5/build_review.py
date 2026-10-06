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
banks=[];stats=[]
for b in range(2):
 bank=[]
 for j in range(2):
  first=read(root.parent/'shotgun-v4'/f'variant-3-{j}.wav')
  if b==0:a=first
  else:
   # Distinct second recorded blast 55 ms later, equally forceful.
   second=pitch(read(root.parent/'shotgun-v4'/f'variant-3-{1-j}.wav'),.97)
   a=[0.]*(len(second)+round(.055*R));add(a,first,1);add(a,second,.96,.055)
   peak=max(abs(x) for x in a);a=[x*.9/peak for x in a]
  p=root/f'variant-{b}-{j}.wav'
  with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(x*32767) for x in a]).tobytes())
  bank.append('data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode())
  stats.append({'file':p.name,'peak':max(abs(x) for x in a),'duration':len(a)/R,'second_barrel_delay':.055 if b else None})
 banks.append(bank)
(root/'index.html').write_text((root/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(root/'manifest.json').write_text(json.dumps({'source':'../shotgun-v4/manifest.json','base':'v4 variant 3','recipe':'Two alternate recorded processed blasts, second at 55ms with 0.97 playback rate and 0.96 gain; common peak normalization. One game shot, two sound/flash pulses in prototype only.','cadence':.7,'clips':stats},indent=2))
print(json.dumps(stats,indent=2))
