"""Heavy arena shotgun sketch: recorded crack plus a compact explosive body."""
from pathlib import Path
import hashlib, importlib.util, json, math
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('duration',ROOT/'tools/preview-shotgun-duration.py')
d=importlib.util.module_from_spec(spec);spec.loader.exec_module(d)
a=d.arena;R=d.RATE;OUT=ROOT/'.local/shotgun-heavy-v5'

def level(samples,rms):
    measured=math.sqrt(sum(x*x for x in samples[:int(.18*R)])/int(.18*R))
    return [x*rms/max(measured,1e-8) for x in samples]

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    explosion=ROOT/'docs/audio/approved-2026-10-03/explosion-short/explosion-1.wav'
    body=level(a.band(d.load(explosion),65,900),.34)
    clips=[];report={'sources':[], 'files':[], 'recipe':'Original recorded shotgun attack and restored body; compact band-limited explosion underneath; soft saturation; no global slowdown or pitched sine. Offline authoring parameters only, no runtime changes.'}
    report['sources'].append({'file':str(explosion.relative_to(ROOT)),'sha256':hashlib.sha256(explosion.read_bytes()).hexdigest(),'provenance':'docs/audio/approved-2026-10-03/explosion/manifest.json'})
    for take in range(2):
        source=ROOT/f'.local/shotgun-duration-v4/body-{take}.wav'
        shot=d.load(source);crack=level(a.band(shot,800,8500),.16)
        mid=level(a.band(shot,160,2400),.20)
        x=[0.]*(int(.55*R))
        for i in range(len(x)):
            t=i/R
            dry=shot[i] if i<len(shot) else 0
            high=crack[i] if i<len(crack) else 0
            middle=mid[i] if i<len(mid) else 0
            # A pressure burst with a controlled release; avoid the former rumble tail.
            j=i-int(.003*R)
            boom=body[j] if 0<=j<len(body) else 0
            boom*=math.exp(-max(0,t-.055)/.085)
            middle*=math.exp(-max(0,t-.040)/.115)
            high*=math.exp(-max(0,t-.008)/.050)
            v=.55*dry+.65*high+.65*middle+1.45*boom
            # Rounded density instead of clipping the final PCM output.
            x[i]=math.tanh(v*1.45)*min(1,i/16,(len(x)-i)/(.065*R))
        clips.append(x)
        report['sources'].append({'file':str(source.relative_to(ROOT)),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'reproduce':'tools/preview-shotgun-duration.py'})
    gain=.9/max(abs(v) for x in clips for v in x)
    clips=[[v*gain for v in x] for x in clips]
    for take,x in enumerate(clips):
        item=a.write(OUT/f'heavy-{take}.wav',x);item['energy_ms']=d.energy(x)
        item['rms_first_180ms']=math.sqrt(sum(v*v for v in x[:int(.18*R)])/int(.18*R))
        report['files'].append(item)
    sequence=[0.]*(int(5.1*R))
    for t,k in [(0,0),(1.25,1),(2.5,0),(3.2,1),(3.9,0)]:
        start=round(t*R)
        for i,v in enumerate(clips[k]):sequence[start+i]+=v
    report['files'].append(a.write(OUT/'heavy-preview.wav',sequence))
    report['status']='Listen pending. Two isolated shots then three at 0.7s cadence. Peak normalization is not perceptual loudness matching. Original spread/color task still pending.'
    (OUT/'manifest.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report['files'],indent=2))
if __name__=='__main__':main()
