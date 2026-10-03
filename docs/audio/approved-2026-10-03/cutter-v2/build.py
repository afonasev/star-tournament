from pathlib import Path
import array,wave,math,json,base64
r=Path(__file__).parent;R=32000

def read(p):
 with wave.open(str(p),'rb') as w:return [v/32768 for v in array.array('h',w.readframes(w.getnframes()))]
def write(p,a):
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(v*32767) for v in a]).tobytes())
 return 'data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()
a=read(r.parent/'cutter/loop-0.wav');b=read(r.parent/'cutter/loop-3.wav');banks=[];stats=[]
# 0 and 4 reproduce previous previews exactly; three mixes interpolate both layers.
for k,blend in enumerate([0,.25,.5,.75,1]):
 if k in [0,4]:
  prev=0 if k==0 else 3
  banks.append(['data:audio/wav;base64,'+base64.b64encode((r.parent/'cutter'/f'preview-{prev}-{mode}.wav').read_bytes()).decode() for mode in range(2)])
  continue
 bank=[]
 for mode,duration in enumerate([1.,4.]):
  x=[]
  for i in range(int((duration+.16)*R)):
   t=i/R;env=min(1,t/.055)*max(0,min(1,(duration+.14-t)/.14))
   v=((1-blend)*a[i%len(a)]+blend*b[i%len(b)])/math.sqrt((1-blend)**2+blend**2)
   x.append(v*env)
  bank.append(write(r/f'preview-{k}-{mode}.wav',x))
 banks.append(bank);stats.append({'variant':k,'current_weight':1-blend,'plasma_weight':blend,'peak':max(map(abs,x))})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'source':'../cutter/manifest.json','processing':'blend approved-to-explore directions 0 and 3; equal-power normalization; 55ms attack, 140ms release; reference previews unchanged','variants':stats,'runtime_integration':False},indent=2))
(r.parent/'cutter/selection.json').write_text(json.dumps({'status':'none accepted','rejected':[1,2],'direction':'between 0 and 3','feedback':'возможно что-то между 0 и 3, пока никакой вариант не подходит, 1 и 2 вообще мимо'},ensure_ascii=False,indent=2))
print(stats)
