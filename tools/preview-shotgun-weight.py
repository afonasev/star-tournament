from pathlib import Path
import subprocess, json, wave, array, math
root=Path(__file__).resolve().parents[1]; out=root/'.local/shotgun-weight'; out.mkdir(parents=True,exist_ok=True); src=root/'unity/Assets/StarTournament/Resources/Audio'
filters={
 'A-dense':'asetrate=39690,aresample=44100,bass=g=6:f=110:w=0.7,equalizer=f=3200:t=q:w=0.8:g=-4,alimiter=limit=0.9:level=false',
 'B-punch':'bass=g=5:f=150:w=0.8,equalizer=f=700:t=q:w=0.7:g=3,equalizer=f=4500:t=q:w=0.8:g=-5,acompressor=threshold=0.25:ratio=2:attack=10:release=90:makeup=1.15,alimiter=limit=0.9:level=false',
 'C-thunder':'asetrate=37485,aresample=44100,bass=g=7:f=85:w=0.8,equalizer=f=2800:t=q:w=0.8:g=-5,aecho=0.8:0.5:65:0.22,alimiter=limit=0.9:level=false'}
manifest={}
for name, filt in [('original','anull')]+list(filters.items()):
    clips=[]
    for i in range(2):
        dest=out/f'{name}-{i}.wav'
        subprocess.run(['ffmpeg','-v','error','-y','-i',str(src/f'shotgun-{i}.wav'),'-af',filt,'-ar','44100','-ac','1',str(dest)],check=True)
        with wave.open(str(dest)) as w: a=array.array('h',w.readframes(w.getnframes()))
        # Aim for common RMS with peak headroom; peak limiting can leave RMS unequal.
        rms=math.sqrt(sum(x*x for x in a)/len(a)); gain=min(3600/rms,30000/max(abs(x) for x in a))
        a=array.array('h',(round(x*gain) for x in a)); clips+=list(a)+[0]*44100
    dest=out/f'{name}.wav'
    with wave.open(str(dest),'wb') as w:
        w.setparams((1,2,44100,0,'NONE','not compressed'));w.writeframes(array.array('h',clips).tobytes())
    manifest[name]={'filter':filt,'peak':max(abs(x) for x in clips)/32768,'preview':str(dest)}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps(manifest,indent=2))
