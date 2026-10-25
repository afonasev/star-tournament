"""Controlled duration revision: preserve pitch, restore recorded body, compare pace."""
from pathlib import Path
import array, hashlib, importlib.util, json, math, subprocess, wave
ROOT=Path(__file__).resolve().parents[1]; OUT=ROOT/'.local/shotgun-duration-v4'; RATE=48000
spec=importlib.util.spec_from_file_location('arena',ROOT/'tools/preview-shotgun-arena.py'); arena=importlib.util.module_from_spec(spec);spec.loader.exec_module(arena)

def load(p):
    return list(array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(RATE),'-f','f32le','-'])))

def energy(a):
    total=sum(x*x for x in a); acc=0.; out={}
    for i,x in enumerate(a):
        acc+=x*x
        for q in (.5,.9,.95,.99):
            if str(q) not in out and acc>=q*total:out[str(q)]=round(i/RATE*1000,1)
    return out

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    source=ROOT/'docs/audio/approved-2026-10-03/source/Prepared SFX Library/Model 12/K_22P.wav'
    raw=load(source);clips=[]
    for t in (.84,7.44):
        lo=round((t-.025)*RATE);hi=round((t+.025)*RATE); threshold=max(abs(x) for x in raw[lo:hi])*.07
        onset=next(i for i in range(lo,hi) if abs(raw[i])>=threshold)
        a=raw[onset-48:onset+round(.65*RATE)]
        a=arena.band(a,65,7500)
        peak=max(abs(x) for x in a);a=[x/peak for x in a]
        # Preserve the transient. Raise the actual recording's short body with
        # 5ms RMS compression, rather than pitching down or adding a sine rumble.
        width=240;power=[x*x for x in a]; acc=sum(power[:width]); target=[]
        for i,x in enumerate(a):
            if i and i+width-1<len(a):acc+=power[i+width-1]-power[i-1]
            rms=math.sqrt(max(acc,1e-12)/width)
            t=i/RATE
            wanted=.19*math.exp(-max(0,t-.015)/.10)
            gain=min(20.,max(.7,wanted/max(rms,1e-7)))
            blend=min(1,max(0,(t-.003)/.010))
            v=x*((1-blend)+gain*blend)
            v*=min(1,i/12,(len(a)-i)/(RATE*.06))
            target.append(v)
        clips.append(target)
    gain=.9/max(abs(x) for a in clips for x in a);clips=[[x*gain for x in a] for a in clips]
    report={'source':str(source.relative_to(ROOT)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'notes':'Model 12 CC0 source. Original pitch, original attack. Body RMS raised for duration diagnosis. Slower comparison uses atempo=0.7, preserving pitch. No runtime edits.','files':[]}
    for i,a in enumerate(clips):
        p=OUT/f'body-{i}.wav'; info=arena.write(p,a);info['energy_ms']=energy(a);report['files'].append(info)
        subprocess.run(['ffmpeg','-v','error','-y','-i',str(p),'-af','atempo=0.7','-ar',str(RATE),str(OUT/f'slower-{i}.wav')],check=True)
    for name in ('body','slower'):
        seq=[]
        for i in range(2):seq+=load(OUT/f'{name}-{i}.wav')+[0.]*RATE
        info=arena.write(OUT/f'{name}.wav',seq);report['files'].append(info)
    old=load(ROOT/'.local/shotgun-arena-v3/2-arena-full-0.wav')
    report['previous_energy_ms']=energy(old)
    (OUT/'manifest.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
