"""Original CC0 shotgun design: compact arena blast and fuller arena blast.

No game audio is sampled. Run from any directory; ffmpeg is required.
"""
from pathlib import Path
import array, hashlib, json, math, subprocess, wave
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'.local/shotgun-arena-v3'
RATE=48000
SOURCES=[('Model 12/K_22P.wav',[.84,7.44]),('Mossberg/N_30P.wav',[1.69,4.96]),('Nova/O_21P.wav',[.43,3.46])]

def low(a,hz):
    k=1-math.exp(-2*math.pi*hz/RATE); v=0.; result=[]
    for x in a:
        v+=k*(x-v); result.append(v)
    return result

def pitch(a,ratio):
    result=[]
    for i in range(int((len(a)-1)/ratio)):
        p=i*ratio; j=int(p); f=p-j
        result.append(a[j]*(1-f)+a[j+1]*f)
    return result

def band(a,lo,hi):
    l=low(a,lo); h=low(a,hi)
    return [y-x for x,y in zip(l,h)]

def scaled(a):
    # Match short-body energy before mixing; avoid the old full-band low-pitch stack.
    rms=math.sqrt(sum(x*x for x in a[:4800])/4800)
    return [x/max(rms,1e-8) for x in a]

def add(dst,a,gain,delay=0,decay=.1):
    start=round(delay*RATE)
    for i,x in enumerate(a[:len(dst)-start]):
        dst[start+i]+=x*gain*math.exp(-max(0,i/RATE-.012)/decay)

def write(path,a):
    pcm=array.array('h',(round(max(-1,min(1,x))*32767) for x in a))
    with wave.open(str(path),'wb') as w:
        w.setparams((1,2,RATE,0,'NONE','not compressed'));w.writeframes(pcm.tobytes())
    with wave.open(str(path)) as w:
        assert (w.getnchannels(),w.getsampwidth(),w.getframerate())==(1,2,RATE)
        p=array.array('h',w.readframes(w.getnframes()))
        assert p and max(abs(x) for x in p)<32767
    return {'file':path.name,'peak':max(abs(x) for x in p)/32768,'seconds':len(p)/RATE,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    banks=[];manifest={'source_url':'https://opengameart.org/content/the-free-firearm-sound-library','license':'CC0','authors':'Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney','sources':[],'variants':[]}
    for filename,times in SOURCES:
        p=ROOT/'docs/audio/approved-2026-10-03/source/Prepared SFX Library'/filename
        raw=array.array('f',subprocess.check_output(['ffmpeg','-v','error','-i',str(p),'-ac','1','-ar',str(RATE),'-f','f32le','-']))
        takes=[]
        for t in times:
            lo=round((t-.025)*RATE);hi=round((t+.025)*RATE)
            threshold=max(abs(x) for x in raw[lo:hi])*.07
            onset=next(i for i in range(lo,hi) if abs(raw[i])>=threshold)
            takes.append(scaled(list(raw[onset-48:onset+round(.8*RATE)])))
        banks.append(takes)
        manifest['sources'].append({'file':filename,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    for name,decay,bodygain in [('1-arena-dry',.065,.65),('2-arena-full',.105,.95)]:
        clips=[]
        for take in range(2):
            a=[0.]*round(RATE*.5)
            # Original-pitch crack, low-mid recorded pressure, one near-coincident second barrel.
            front=scaled(band(banks[0][take],110,6500))
            body=scaled(band(pitch(banks[1][take],.88),75,650))
            snap=scaled(band(banks[2][take],550,8000))
            add(a,front,.75,decay=decay)
            add(a,body,bodygain,.002,decay=decay*.85)
            add(a,snap,.40,.009,decay=decay*.65)
            # Parallel density, with the first transient left linear. No tone/rumble oscillator.
            for i,x in enumerate(a):
                wet=min(1,max(0,(i/RATE-.004)/.012))*.42
                a[i]=x*(1-wet)+math.tanh(x*.8)*1.25*wet
                a[i]*=min(1,i/12,(len(a)-i)/(RATE*.035))
            clips.append(a)
        peak=max(abs(x) for a in clips for x in a); gain=.9/peak
        clips=[[x*gain for x in a] for a in clips]
        files=[write(OUT/f'{name}-{i}.wav',a) for i,a in enumerate(clips)]
        preview=[0.]*(RATE*5)
        # Two isolated takes, followed by three shots at the game's 0.7-second cadence.
        for t,k in [(0,0),(1.2,1),(2.4,0),(3.1,1),(3.8,0)]:
            start=round(t*RATE)
            for i,x in enumerate(clips[k]):preview[start+i]+=x
        files.append(write(OUT/f'{name}.wav',preview))
        manifest['variants'].append({'name':name,'decay_seconds':decay,'body_gain':bodygain,'files':files})
    manifest['notes']='Direction sketches based on Quake / Quake-DOOM brief. No sampled game assets; no added reverb or synthetic rumble. Peaks matched, not perceptual loudness. No listening or runtime acceptance inferred.'
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(json.dumps(manifest['variants'],indent=2))

if __name__=='__main__':main()
