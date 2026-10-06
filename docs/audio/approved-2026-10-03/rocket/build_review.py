from pathlib import Path
import array,math,wave,json,base64,subprocess
root=Path(__file__).parent;R=48000

def read(p):return list(array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(R),'-f','f32le','-'])))
def low(a,hz):
 k=1-math.exp(-2*math.pi*hz/R);v=0;out=[]
 for x in a:v+=k*(x-v);out.append(v)
 return out
def pitch(a,r):return [a[int(i*r)]*(1-i*r%1)+a[int(i*r)+1]*(i*r%1) for i in range(int((len(a)-1)/r))]
def trim(a):
 peak=max(abs(x) for x in a);i=next(i for i,x in enumerate(a) if abs(x)>peak*.035);return a[max(0,i-96):]
def scale(a):
 rms=math.sqrt(sum(x*x for x in a[:16800])/16800);g=min(.10/max(rms,1e-9),.8/max(abs(x) for x in a));return [x*g for x in a]
src=['/Users/eaafonasev/Projects/star-tournament/unity/Assets/StarTournament/Resources/Audio/rocket.wav','source/launches/rlaunch.wav','source/rocket_launch_2.wav','source/rocket_launch_4.wav']
banks=[];stats=[]
for b,file in enumerate(src):
 a=read(root/file)
 if b:
  a=trim(a)
  if b==1:a=pitch(a,1.20)
  if b==3:
   a=pitch(a,.78);body=low(a,350);a=[x+1.8*body[i] for i,x in enumerate(a)]
   shot=read(root.parent/'shotgun'/'variant-3-0.wav');shot=pitch(shot,.6);shot=low(shot,600)
   peak=max(abs(x) for x in shot);g=.45*max(abs(x) for x in a)/peak
   for i in range(min(len(shot),len(a))):a[i]+=shot[i]*g*math.exp(-i/(R*.06))
  n=int({1:.55,2:1.0,3:.85}[b]*R);a=a[:n]
  for i in range(len(a)):a[i]*=min(1,i/48)*min(1,(len(a)-i)/(R*.14))
 a=scale(a);p=root/f'variant-{b}.wav'
 with wave.open(str(p),'wb') as w:w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(x*32767) for x in a]).tobytes())
 banks.append(['data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode()]);stats.append({'file':p.name,'source':file,'duration':len(a)/R,'peak':max(abs(x) for x in a),'rms350ms':math.sqrt(sum(x*x for x in a[:16800])/16800)})
(root/'index.html').write_text((root/'template.html').read_text().replace('BANK_DATA',json.dumps(banks)))
(root/'manifest.json').write_text(json.dumps({'sources':[{'author':'Michel Baradari','url':'https://opengameart.org/content/4-projectile-launches','license':'CC BY 3.0','use':'variant 1: trim, speed and gain changes'},{'author':'dklon','url':'https://opengameart.org/content/rocket-launch-pack','license':'CC BY 3.0','use':'variants 2 and 3: trim, gain, pitch and bass layering'},{'author':'Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney','url':'https://opengameart.org/node/21826','license':'CC0','use':'variant 3 low launch thump'}],'cadence':1.2,'visual_flight_seconds':.8,'visual_note':'illustrative, not simulation trajectory','clips':stats},indent=2))
print(json.dumps(stats,indent=2))
