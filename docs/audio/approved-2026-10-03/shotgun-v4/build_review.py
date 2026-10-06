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
  if b==0:a=read(root.parent/'shotgun-v3'/f'variant-3-{j}.wav')
  else:
   a=[0.]*(int(.95*R));raw=sources[0][j];second=sources[2][1-j]
   # Return to recorded attacks: no full-band saturation flattening the bang.
   rate={1:.65,2:.56,3:.74}[b]
   shot=norm(pitch(raw,rate));other=norm(pitch(second,rate*.91))
   add(a,shot,1.0);add(a,other,{1:.65,2:.90,3:.85}[b],{1:.005,2:.027,3:.011}[b])
   # Sustained compressed recording layer AFTER the initial unflattened transient.
   thump=norm(low(pitch(second,.43),380));rumble=norm(low(pitch(raw,.36),220))
   body=[math.tanh(x*.85)*math.exp(-i/(R*{1:.15,2:.25,3:.10}[b])) for i,x in enumerate(thump)]
   add(a,body,{1:1.25,2:1.9,3:1.3}[b],.014)
   add(a,[x*math.exp(-i/(R*.22)) for i,x in enumerate(rumble)],{1:.18,2:.42,3:.15}[b],.026)
   # Dense non-tonal explosive bloom from recorded broadband tails.
   rng=random.Random(953+j)
   for q in range({1:4,2:5,3:10}[b]):
    grain=pitch(sources[q%3][j][:int(.052*R)],rng.uniform(.60,.96))
    lowpart=low(grain,250);highpart=low(grain,5000)
    grain=[math.tanh((highpart[i]-lowpart[i])*1.6)*math.exp(-i/(R*.026)) for i in range(len(grain))]
    add(a,grain,{1:.18,2:.20,3:.40}[b],.016+q*.006)
   for d,g in [(.043,.18),(.081,.10),(.137,.07)]:add(a,low(shot,1100),g*(1.7 if b==2 else 1),d)
   for i in range(len(a)):
    t=i/R
    a[i]*=min(1,i/18)*math.exp(-max(0,t-.09)/{1:.16,2:.27,3:.12}[b])
    a[i]*=min(1,(len(a)-i)/(R*.07))
   # Peak-normalize the transient, preserving crest and the attack/body contrast.
   peak=max(abs(x) for x in a);a=[x*.90/peak for x in a]
  p=root/f'variant-{b}-{j}.wav'
  with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(x*32767) for x in a]).tobytes())
  bank.append('data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode())
  stats.append({'file':p.name,'peak':max(abs(x) for x in a),'rms_250ms':math.sqrt(sum(x*x for x in a[:12000])/12000),'duration':len(a)/R})
 banks.append(bank)
(root/'index.html').write_text((root/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(root/'manifest.json').write_text(json.dumps({'sources':'../shotgun/manifest.json','reference':'v3 variant 3 unchanged','recipe':'Unflattened recorded dual attacks, delayed compressed low body, low rumble, dense recorded crackle bloom. New candidates peak-normalized 0.9; reference unchanged. Not loudness matched; compare optional RMS-matched mode.','cadence':.7,'clips':stats},indent=2))
print(json.dumps(stats,indent=2))
