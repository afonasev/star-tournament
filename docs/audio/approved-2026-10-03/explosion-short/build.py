from pathlib import Path
import wave,array,json,base64,hashlib
r=Path(__file__).parent;R=48000

def read(p):
 with wave.open(str(p),'rb') as w:return list(array.array('h',w.readframes(w.getnframes())))
def write(p,a):
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',a).tobytes())
 return 'data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()
a=read(r.parent/'explosion/explosion-2.wav');launch=read(r.parent/'rocket/variant-1.wav');short=a[:int(.65*R)]
# First 180 ms unchanged; smooth cosine release to silence at 650 ms.
import math
for i in range(int(.18*R),len(short)):
 t=(i/R-.18)/(.65-.18);short[i]=round(short[i]*(.5+.5*math.cos(math.pi*t)))
banks=[]
for i,x in enumerate([a,short]):
 combo=[0]*(int(.8*R)+len(x))
 for j,v in enumerate(launch):combo[j]+=v
 for j,v in enumerate(x):combo[int(.8*R)+j]+=v
 banks.append([write(r/f'explosion-{i}.wav',x),write(r/f'sequence-{i}.wav',combo)])
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'source':'../explosion/manifest.json','base_variant':2,'original_duration':len(a)/R,'new_duration':.65,'unchanged_attack_seconds':.18,'processing':'cosine fade from .18 to .65 seconds, no gain change','status':'requested refinement; preview only'},indent=2))
(r.parent/'explosion/selection.json').write_text(json.dumps({'variant':2,'status':'direction selected; shorter tail requested','feedback':'2 , но можно быстрее звук заканчивать'},ensure_ascii=False,indent=2))
assert short[:8640]==a[:8640]
assert max(abs(v) for v in short)<=max(abs(v) for v in a)
print('Verified: 1.45s -> 0.65s; first 180ms unchanged; last sample',short[-1])
