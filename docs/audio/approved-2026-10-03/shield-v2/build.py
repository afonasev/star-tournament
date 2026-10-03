from pathlib import Path
import array,wave,math,json,base64,subprocess
r=Path(__file__).parent;R=32000

def read(p):
 with wave.open(str(p),'rb') as w:return [v/32768 for v in array.array('h',w.readframes(w.getnframes()))]
def write(p,a):
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(v*32767) for v in a]).tobytes())
 return 'data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()
base=read(r.parent/'shield/hit-0-0.wav');banks=[];stats=[]
for k in range(4):
 shots=[]
 for j in range(2 if k else 1):
  x=base[:]
  if k:
   # Preserve first 30ms and base level exactly; attach the shield response after it.
   phase=0.
   for i in range(int(.03*R),len(x)):
    t=(i/R-.03);attack=min(1,t/.006);decay=math.exp(-t/[1,.065,.055,.115][k]);variation=1+j*.025
    if k==1:
     phase+=2*math.pi*(250+850*math.exp(-t/.021))*variation/R
     layer=math.sin(phase)+.3*math.sin(phase*1.47)
     value=.10*layer*attack*decay
    elif k==2:
     # Rough electrical reflection made from the existing transient, with short irregular delays.
     a=base[max(0,i-int(.014*R))%len(base)];b=base[max(0,i-int(.019*R))%len(base)]
     value=(a-b)*1.2*math.sin(2*math.pi*690*variation*t)*attack*decay
     value+=.065*math.sin(2*math.pi*(750*t+1400*t*t))*attack*decay
    else:
     layer=math.sin(2*math.pi*340*variation*t)+.46*math.sin(2*math.pi*571*variation*t)+.23*math.sin(2*math.pi*913*variation*t)
     value=.085*layer*attack*decay
    x[i]+=value
  assert x[:960]==base[:960]
  assert max(map(abs,x))<.85
  shots.append(x);write(r/f'hit-{k}-{j}.wav',x)
 burst=[0.]*(5*3840+len(base))
 for j in range(6):
  for i,v in enumerate(shots[j%len(shots)]):burst[j*3840+i]+=v
 assert max(map(abs,burst))<1
 banks.append([write(r/f'single-{k}.wav',shots[0]),write(r/f'burst-{k}.wav',burst)])
 stats.append({'variant':k,'single_peak':max(map(abs,shots[0])),'series_peak':max(map(abs,burst)),'first_30ms_unchanged':True})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'source':'../shield/hit-0-0.wav (level-adjusted current game hit)','processing':'preserved base and first 30ms; added short energy deflection, electrical reflection or resonant field response; no change to base gain','series_interval':.12,'clips':stats},indent=2))
(r.parent/'shield/selection.json').write_text(json.dumps({'status':'new candidates rejected','rejected':[1,2,3],'preferred_base':0,'feedback':'текущий звук достаточно выразительнный, но не совсем на щит похож, остальные не звучат совсем как нужно'},ensure_ascii=False,indent=2))
print(stats)
