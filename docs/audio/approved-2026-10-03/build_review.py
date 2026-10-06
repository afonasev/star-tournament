from pathlib import Path
import subprocess,array,math,wave,json,base64
root=Path(__file__).parent
R=48000
banks=[('/Users/eaafonasev/Projects/star-tournament/unity/Assets/StarTournament/Resources/Audio/rifle.wav',[0]),('source/Prepared SFX Library/AR-15/D_32P.wav',[.70,5.64]),('source/Prepared SFX Library/AK-47/C_28P.wav',[.61,3.25,6.02,9.15]),('source/Prepared SFX Library/Carl Gustav M45/G_20P.wav',[.34,2.25,5.26])]
data=[]; stats=[]
for k,(file,starts) in enumerate(banks):
 raw=array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(root/file),'-ac','1','-ar',str(R),'-f','f32le','-']))
 clips=[]
 for j,t in enumerate(starts):
  if k:
   lo=max(0,round((t-.025)*R));hi=round((t+.025)*R)
   threshold=max(abs(x) for x in raw[lo:hi])*.07
   onset=next(i for i in range(lo,hi) if abs(raw[i])>=threshold)
   a=list(raw[max(0,onset-48):onset+round(.42*R)])
  else:a=list(raw)
  # Preserve attack, taper the recorded tail for dense gameplay.
  for i in range(len(a)):
   if k and i<24:a[i]*=i/24
   if k and i>len(a)-int(.1*R):a[i]*=max(0,(len(a)-i)/(.1*R))
  rms=math.sqrt(sum(x*x for x in a[:7200])/7200)
  gain=min(.05/max(rms,1e-9),.7/max(abs(x) for x in a))
  a=[x*gain for x in a]
  p=root/f'variant-{k}-{j}.wav'
  with wave.open(str(p),'wb') as w:
   w.setparams((1,2,R,0,'NONE','not compressed'));w.writeframes(array.array('h',[round(max(-1,min(1,x))*32767) for x in a]).tobytes())
  clips.append('data:audio/wav;base64,'+base64.b64encode(p.read_bytes()).decode())
  stats.append(dict(file=p.name,source=file,onset_seconds=round((onset-48)/R,5) if k else 0,peak=round(max(abs(x) for x in a),4),attack_rms=round(math.sqrt(sum(x*x for x in a[:7200])/7200),4)))
 data.append(clips)
(root/'manifest.json').write_text(json.dumps({'source':'https://opengameart.org/node/21826','license':'CC0','authors':'Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney','cadence_seconds':.1,'notes':'Mono excerpts, attack-aligned, tail taper; first 150ms RMS matched with peak limit. Direction review only.','clips':stats},indent=2))
html=(root/'template.html').read_text().replace('BANK_DATA',json.dumps(data))
(root/'index.html').write_text(html)
print(json.dumps(stats,indent=2))
