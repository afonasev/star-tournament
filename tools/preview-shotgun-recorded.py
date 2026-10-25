"""Preview original recorded shotgun transients, without pitch shifts or synthetic layers."""
from pathlib import Path
import array, hashlib, json, math, subprocess, wave
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'.local/shotgun-recorded-v2'; OUT.mkdir(parents=True,exist_ok=True)
SRC=ROOT/'docs/audio/approved-2026-10-03/source/Prepared SFX Library'
RATE=48000
BANKS=[('1-model12','Model 12/K_22P.wav',[.84,7.44]),('2-mossberg','Mossberg/N_30P.wav',[1.69,4.96]),('3-nova','Nova/O_21P.wav',[.43,3.46])]
manifest={'source_url':'https://opengameart.org/content/the-free-firearm-sound-library','license':'CC0','authors':'Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney','processing':'Original pitch; mono; onset crop retaining 2ms preattack; 100ms tail fade; common bank gain to peak .90; no compression, synthesized layers or echo. Not loudness matched. Two takes per preview.','banks':[]}
for name,file,starts in BANKS:
 p=SRC/file
 raw=array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(RATE),'-f','f32le','-']))
 clips=[];onsets=[]
 for t in starts:
  lo=round((t-.025)*RATE); hi=round((t+.025)*RATE)
  threshold=max(abs(x) for x in raw[lo:hi])*.07
  onset=next(i for i in range(lo,hi) if abs(raw[i])>=threshold)
  a=list(raw[onset-96:onset+round(.78*RATE)])
  for i in range(len(a)):
   a[i]*=min(1,i/12,(len(a)-i)/(.1*RATE))
  clips.append(a);onsets.append(onset/RATE)
 gain=.9/max(abs(x) for a in clips for x in a)
 sequence=[]
 for a in clips:sequence += [x*gain for x in a]+[0.]*int(RATE*.85)
 dest=OUT/(name+'.wav')
 with wave.open(str(dest),'wb') as w:
  w.setparams((1,2,RATE,0,'NONE','not compressed'))
  w.writeframes(array.array('h',(round(x*32767) for x in sequence)).tobytes())
 with wave.open(str(dest)) as w:
  assert w.getnchannels()==1 and w.getsampwidth()==2 and w.getframerate()==RATE
  pcm=array.array('h',w.readframes(w.getnframes())); assert max(abs(x) for x in pcm)<32767
 manifest['banks'].append({'name':name,'source':file,'source_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'onsets':onsets,'gain':gain,'output_sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'peak':max(abs(x) for x in pcm)/32768,'seconds':len(pcm)/RATE})
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps(manifest,indent=2))
