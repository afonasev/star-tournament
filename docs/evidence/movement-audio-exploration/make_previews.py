#!/usr/bin/env python3
"""Reproducible sound-direction sketches; no runtime asset replacement."""
import array
import hashlib
import json
import math
from pathlib import Path
import random
import subprocess
import wave
import zipfile

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
RATE = 32000
SOURCE = ROOT / '.local/movement-audio-source/impact.zip'
URL = 'https://kenney.nl/assets/impact-sounds'

def decode(path, cutoff=6500):
    filters = ['-af', f'highpass=f=65,lowpass=f={cutoff}'] if cutoff else []
    data = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(path), '-ac', '1', '-ar', str(RATE), *filters, '-f', 'f32le', '-'])
    return list(array.array('f', data))

def envelope(x):
    # Short click-free boundaries, preserving the internal foley transient.
    return [v * min(1, i / 96, (len(x)-1-i) / 480) for i, v in enumerate(x)]

def level(x, rms=.115, peak=.65):
    active = [v for v in x if abs(v) > .002]
    gain = min(rms / max(.001, math.sqrt(sum(v*v for v in active)/max(1,len(active)))), peak/max(.001,max(map(abs,x))))
    return envelope([v*gain for v in x])

def mix(a, b, offset=0, gain=1):
    result = list(a) + [0.] * max(0, offset+len(b)-len(a))
    for i,v in enumerate(b): result[i+offset] += v*gain
    return result

def write(name,x):
    peak=max(map(abs,x), default=0)
    assert peak < 1, (name,peak)
    path=OUT/name
    path.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(path),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE)
        f.writeframes(array.array('h',[round(v*32767) for v in x]).tobytes())
    return {'file':name,'seconds':len(x)/RATE,'peak':peak,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}

def respeed(x, speed):
    out=[]
    for i in range(int((len(x)-1)/speed)):
        t=i*speed;j=int(t);out.append(x[j]*(1-(t-j))+x[j+1]*(t-j))
    return out

def sequence(steps,jumps,lands, baseline=False):
    rng=random.Random(101)
    out=[0.]*(RATE*15)
    # Current 9 m/s, 2.1 m stride and 50 Hz motor => 0.24 s trigger spacing.
    # First slower movement at 4.2 m/s, then full speed; three jump/land pairs.
    events=[]
    for t in [0.3+i*.5 for i in range(6)]+[4+i*.24 for i in range(12)]: events.append((t,'step'))
    for t in [8,10.2,12.4]: events.extend([(t,'jump'),(t+.70,'land')])
    bank={'step':steps,'jump':jumps,'land':lands}
    previous={key:-1 for key in bank}
    for t,key in events:
        choices=[i for i in range(len(bank[key])) if i!=previous[key]] or [0]
        idx=rng.choice(choices);previous[key]=idx
        x=bank[key][idx]
        if not baseline: x=respeed(x,rng.uniform(.982,1.018))
        # Match installed 80% Effects and 0.65 local gain. New samples keep
        # relative motion balance in their banks; no per-file loudness boost.
        gain=.8*.65*({'step':.5,'jump':.9,'land':.8}[key] if baseline else {'step':.5,'jump':.38,'land':.6}[key])
        if not baseline: gain*=rng.uniform(.94,1.04)
        out=mix(out,x,round(t*RATE),gain)
    return out

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    src=SOURCE.parent/'extracted'
    with zipfile.ZipFile(SOURCE) as z:
        for name in z.namelist():
            if name.startswith('Audio/footstep_') or name.endswith('License.txt'): z.extract(name,src)
    reports=[]
    old={key:[decode(ROOT/f'unity/Assets/StarTournament/Resources/Audio/{key}.wav',None)] for key in ['step','jump','land']}
    reports.append(write('00-current.wav',sequence(old['step'],old['jump'],old['land'],True)))
    presets=[('01-concrete',4800,'concrete',None),('02-soft-sole',3200,'carpet','concrete'),('03-weighted-boot',4200,'concrete','wood')]
    for name,cutoff,material,secondary in presets:
        steps=[];jumps=[];lands=[]
        for i in range(5):
            x=decode(src/f'Audio/footstep_{material}_{i:03}.ogg',cutoff)
            if secondary:
                layer=decode(src/f'Audio/footstep_{secondary}_{(i+2)%5:03}.ogg',cutoff)
                x=mix(x,layer,round(.009*RATE),.35 if material=='carpet' else .17)
            step=level(x,.10 if material=='carpet' else .115)
            # Ground push and quiet shoe friction, no pitched electronic sweep.
            push=level(respeed(x,1.1),.085,.42)
            # Two feet contact a few milliseconds apart; each has a different take.
            other=decode(src/f'Audio/footstep_{material}_{(i+1)%5:03}.ogg',cutoff-600)
            land=level(mix(respeed(x,.94),other,round((.027+i*.003)*RATE),.48),.14,.75)
            steps.append(step);jumps.append(push);lands.append(land)
            for kind,clip in [('step',step),('jump',push),('land',land)]: reports.append(write(f'{name}/{kind}-{i}.wav',clip))
        reports.append(write(f'{name}-queue.wav',sequence(steps,jumps,lands)))
    manifest={'classification':'OFFLINE_AUDIO_DIRECTION_SKETCH_NOT_PLAYER_ACCEPTANCE','baseline_revision':'50656793e7537484623e6e41554b56fd66fd6496','source':URL,'source_license':'CC0','source_zip_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'timing':'0.3–2.8 s slow steps, 4–6.64 s full speed, 8/10.2/12.4 s jump with approximate flat-ground landing +0.70 s','effects_percent':80,'local_gain':.65,'new_preview_gains':{'step':.5,'jump':.38,'land':.6},'variants':5,'random_seed':101,'files':reports}
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps([r for r in reports if '/' not in r['file']],indent=2))

if __name__=='__main__':main()
