"""Audition prepared phrases with the two-voice loop/crossfade timeline; not a Player capture."""
from pathlib import Path
import json,wave,subprocess,hashlib
import numpy as np,soundfile as sf
root=Path.cwd();assets=root/'unity/Assets/StarTournament/Resources/Audio/Music'
out=root/'.local/music-exploration/phase-previews';out.mkdir(parents=True,exist_ok=True)
manifest=json.loads((assets/'manifest.json').read_text());results=[];sr=44100;hop=882;dt=hop/sr
for track in manifest['tracks']:
 phrases=track['phases'];clips=[sf.read(root/'unity/Assets/StarTournament/Resources'/str(p['resource']+'.wav'),dtype='float32',always_2d=True)[0] for p in phrases]
 positions=[0.,0.];slots=[0,0];weights=[1.,0.];active=0;incoming=-1;phase=requested=0;transition=0.;length=1.
 output=[];events=[]
 for frame in range(3000):
  elapsed=frame*dt;requested=max(requested,2 if elapsed>=40.2 else 1 if elapsed>=19.8 else 0)
  if incoming>=0:
   transition+=dt;t=min(1,transition/length);weights[active]=np.cos(t*np.pi/2);weights[incoming]=np.sin(t*np.pi/2)
   if t>=1:weights[active]=0;active=incoming;incoming=-1;weights[active]=1
  else:
   p=phrases[phase];beat=60/p['bpm'];fade=min(4*beat,(p['loopEnd']-p['loopStart'])*.25)
   loop=positions[active]>=p['loopEnd']-fade;change=requested>phase and (positions[active]/beat)%1<max(dt/beat,.035)
   if loop or change:
    incoming=1-active;nextphase=requested if change else phase;slots[incoming]=nextphase
    positions[incoming]=0 if change else max(0,phrases[nextphase]['loopStart']-fade)
    transition=0;length=fade;weights[incoming]=0;phase=nextphase;events.append({'seconds':round(elapsed,2),'phase':phase,'kind':'advance' if change else 'loop'})
  segment=np.zeros((hop,2),np.float32)
  for slot in range(2):
   if slot==active or slot==incoming:
    start=round(positions[slot]*sr);block=clips[slots[slot]][start:start+hop]
    segment[:len(block)]+=block*weights[slot]/max(1,sum(weights));positions[slot]+=dt
  output.append(segment)
 data=np.concatenate(output);data[:sr]*=np.linspace(0,1,sr)[:,None];data[-2*sr:]*=np.linspace(1,0,2*sr)[:,None]
 peak=float(abs(data).max());data*=min(1,10**(-1/20)/peak)
 f=out/(track['id']+'.wav');sf.write(f,data,sr,subtype='PCM_16')
 subprocess.run(['/opt/homebrew/bin/ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(f),'-codec:a','libmp3lame','-b:a','256k',str(f.with_suffix('.mp3'))],check=True)
 results.append({'id':track['id'],'events':events,'seconds':60,'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'peak_dbfs':float(20*np.log10(abs(data).max())),'evidence':'offline audition, not native Player capture'})
 print(track['id'],len(events),flush=True)
(out/'manifest.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
