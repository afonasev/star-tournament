from pathlib import Path
import array,subprocess,math,wave,json,base64
r=Path(__file__).parent;R=32000

def read(p):return list(array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(R),'-f','f32le','-'])))
def low(a,hz):
 k=1-math.exp(-2*math.pi*hz/R);v=0;out=[]
 for x in a:v+=k*(x-v);out.append(v)
 return out
def norm(a):
 rms=math.sqrt(sum(v*v for v in a)/len(a));return [v/max(rms,1e-8) for v in a]
def loop(a):
 n=int(.10*R);a=a[:int(2.1*R)];bridge=[a[-n+i]*(1-i/n)+a[i]*i/n for i in range(n)];return a[n:-n]+bridge

def write(p,a):
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(max(-1,min(1,x))*32767) for x in a]).tobytes())
 return 'data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()
motor=norm(read(r/'source/generator.mp3')[R:]);hum=norm(read(r/'source/hum.wav')[7*R:]);jet=norm(read(r.parent/'rocket/source/rocket_launch_2.wav')[int(.45*R):]);L=min(len(motor),len(hum),len(jet));motor=motor[:L];hum=hum[:L];jet=jet[:L]
lo=low(motor,600);smooth=low(hum,2800);air=low(jet,3300);airlo=low(jet,240)
current=read(Path('/Users/eaafonasev/Projects/star-tournament/unity/Assets/StarTournament/Resources/Audio/cutter-loop.wav'));start=read(Path('/Users/eaafonasev/Projects/star-tournament/unity/Assets/StarTournament/Resources/Audio/cutter-start.wav'))
raws=[current,loop([lo[i]+.18*smooth[i] for i in range(L)]),loop([smooth[i]*(.82+.18*math.sin(2*math.pi*31*i/R))+.16*motor[i] for i in range(L)]),loop([.80*(air[i]-airlo[i])+.38*lo[i] for i in range(L)])]
banks=[];stats=[]
for k,raw in enumerate(raws):
 a=norm(raw);gain=min(.075,.60/max(map(abs,a)));a=[v*gain for v in a];write(r/f'loop-{k}.wav',a);bank=[]
 for mode,duration in enumerate([1.,4.]):
  out=[0.]*(int((duration+.16)*R))
  for i in range(len(out)):
   t=i/R;envelope=min(1,t/(.18 if k==0 else .055))*max(0,min(1,(duration+.14-t)/.14));out[i]=a[i%len(a)]*envelope
   if k==0 and i<len(start):out[i]+=start[i]*.7*.16
  bank.append(write(r/f'preview-{k}-{mode}.wav',out))
 banks.append(bank);stats.append({'variant':k,'loop_seconds':len(a)/R,'rms':math.sqrt(sum(v*v for v in a)/len(a)),'peak':max(map(abs,a)),'seam_step':abs(a[0]-a[-1])})
(r/'index.html').write_text((r/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'sources':[{'author':'YCbCr','url':'https://opengameart.org/content/generator-loop','license':'CC0'},{'author':'Hansjörg Malthaner','attribution_url':'https://opengameart.org/users/varkalandar','url':'https://opengameart.org/content/force-field-electric-hum','license':'CC BY 4.0 (selected option)'},{'author':'dklon','url':'https://opengameart.org/content/rocket-launch-pack','license':'CC BY 3.0'}],'processing':'steady excerpts, filters, blends, crossfade looping, variant 2 amplitude modulation; preview attack/release ramps; not native Player mix','preview_hold_seconds':[1,4],'clips':stats},indent=2))
print(stats)
