"""Offline phrase preparation, requires the recorded local music Python/librosa runtime.
Run from the owned code checkout after generating the documented continuous references.
The estimated beat grid has octave ambiguity; it is retained for listening review, not claimed as human acceptance.
"""
from pathlib import Path
import json,subprocess,hashlib,wave
import numpy as np, soundfile as sf, librosa
root=Path.cwd();local=root/'.local/music-exploration'
analysis=json.loads((local/'beat-analysis.json').read_text())
asset=root/'unity/Assets/StarTournament/Resources/Audio/Music';asset.mkdir(parents=True,exist_ok=True)
tracks=[('01-orbit','Орбита','orbit-clean-bass.wav',[84,104,124]),('02-reactor','Реактор','02-reactor.wav',[88,108,128]),('03b-vector-cinematic','Вектор — кинематографичный','03b-vector-cinematic.wav',[84,110,136]),('04-supernova','Сверхновая','04-supernova.wav',[76,96,120])]
manifest={'arrangement':'continuous-phrases-v1','tracks':[]};evidence=[]
for tid,title,name,targets in tracks:
 a=next(a for a in analysis if Path(a['file']).name==name);source=root/a['file'];data,sr=sf.read(source,dtype='float32',always_2d=True)
 entry={'id':tid,'title':title,'phases':[]}
 for i,(region,target) in enumerate(zip(a['regions'],targets)):
  bpm=region['tempo_estimate'];beats=np.array(region['beats'])
  # Fold fast subdivisions to the musical pulse near the intended region's tempo.
  while bpm>150:bpm/=2;beats=beats[::2]
  while bpm<65:bpm*=2;beats=np.sort(np.concatenate([beats,beats[:-1]+np.diff(beats)/2]))
  ratio=target/bpm
  ref=local/'phrase-working.wav';sf.write(ref,data[int(region['start']*sr):int(region['end']*sr)],sr,subtype='PCM_16')
  path=asset/f'{tid}-{i}.wav'
  cmd=['/opt/homebrew/bin/ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(ref),'-af',f'atempo={ratio:.9f},alimiter=limit=0.89125094:level=false:latency=true','-c:a','pcm_s16le',str(path)]
  subprocess.run(cmd,check=True)
  converted=beats/ratio
  # Four-beat pre-roll and four-bar repeat body; these are content markers, not gameplay timing.
  if len(converted)<22:raise RuntimeError(f'Insufficient stable beats in {tid}/{i}: {len(converted)}')
  start=float(converted[4]);end=float(converted[20])
  with wave.open(str(path)) as w:
   seconds=w.getnframes()/w.getframerate();x=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(np.float32)/32768
  assert start>0 and end<seconds and end-start>4*60/target and np.isfinite(x).all()
  phase={'resource':f'Audio/Music/{tid}-{i}','bpm':target,'loopStart':round(start,6),'loopEnd':round(end,6)}
  entry['phases'].append(phase)
  evidence.append({'id':tid,'phase':i,'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'raw_tempo_estimate':region['tempo_estimate'],'folded_pulse_estimate':bpm,'target_bpm':target,'pitch_preserving_atempo_ratio':ratio,'command':cmd,'duration_seconds':seconds,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'peak_dbfs':float(20*np.log10(abs(x).max())),'clipped_samples':int((abs(x)>=32767/32768).sum()),'musical_grid':phase,'human_acceptance':'pending'})
 manifest['tracks'].append(entry)
ref.unlink()
(asset/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(local/'phrase-provenance.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n')
print('Prepared',len(evidence),'pitch-preserving phrases')
