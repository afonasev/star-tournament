"""Prepare selected menu motif and audition two transition timings, not a Player capture.

Requires the previously installed local Stable Audio music Python runtime.
The original selected concept remains unchanged. No new synthesis or live pitch shift.
"""
from pathlib import Path
import argparse, hashlib, json, subprocess
import numpy as np
import soundfile as sf
import librosa

parser=argparse.ArgumentParser()
parser.add_argument('--source',type=Path,required=True)
parser.add_argument('--out',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
out=args.out;out.mkdir(parents=True,exist_ok=True)
data,sr=sf.read(args.source,dtype='float32',always_2d=True)
assert sr==44100 and data.shape[1]==2 and np.isfinite(data).all()
mono=librosa.resample(data.mean(axis=1),orig_sr=sr,target_sr=22050)
tempo,frames=librosa.beat.beat_track(y=mono,sr=22050,trim=True)
beats=librosa.frames_to_time(frames,sr=22050)
chroma=librosa.feature.chroma_stft(y=mono,sr=22050)
def harmonic(start,end):
    region=chroma[:,int(start*22050/512):int(end*22050/512)].mean(axis=1)
    return region/np.linalg.norm(region)
def rms(start,end):
    return float(np.sqrt(np.mean(data[int(start*sr):int(end*sr)]**2)))
candidates=[]
for entry in (4,8,12):
    for count in (32,48):
        if entry+count>=len(beats):continue
        start,end=float(beats[entry]),float(beats[entry+count])
        fade=(end-start)/count*4
        if start<fade or end>34:continue
        similarity=float(np.dot(harmonic(start-fade,start),harmonic(end-fade,end)))
        level_difference=abs(20*np.log10(rms(start-fade,start)/rms(end-fade,end)))
        candidates.append({'loopStart':start,'loopEnd':end,'fadeSeconds':fade,'beatCount':count,
                           'harmonic_similarity':similarity,'level_difference_db':float(level_difference),
                           'score':similarity-.025*level_difference+.02*(count/48)})
chosen=max(candidates,key=lambda c:c['score'])
start,end,fade=[int(round(chosen[k]*sr)) for k in ('loopStart','loopEnd','fadeSeconds')]
clip=data[:end+int(.2*sr)]
sf.write(out/'menu-arena.wav',clip,sr,subtype='PCM_16')
# Quantize through the saved PCM so previews and the eventual imported source agree.
clip,_=sf.read(out/'menu-arena.wav',dtype='float32',always_2d=True)
manifest={'id':'menu-arena','title':'Арена','resource':'Audio/Music/menu-arena',
          'bpm':60*chosen['beatCount']/(chosen['loopEnd']-chosen['loopStart']),
          'loopStart':start/sr,'loopEnd':end/sr,'loopFadeSeconds':fade/sr}
gain=.55 # Current shipped round-music-v1 presentation gain, not a new choice.
def envelopes(samples):
    t=np.linspace(0,1,samples,dtype='float32')
    a,b=np.cos(t*np.pi*.5),np.sin(t*np.pi*.5)
    bound=np.maximum(1,a+b)
    return (a/bound)[:,None],(b/bound)[:,None]
def menu(seconds):
    length=int(round(seconds*sr)); result=np.zeros((length,2),dtype='float32')
    write=0;position=0;events=[]
    while write<length:
        count=min(end-fade-position,length-write)
        result[write:write+count]=clip[position:position+count]
        write+=count;position+=count
        if write>=length:break
        count=min(fade,length-write);a,b=envelopes(fade)
        result[write:write+count]=clip[end-fade:end-fade+count]*a[:count]+clip[start-fade:start-fade+count]*b[:count]
        events.append({'at':write/sr,'seconds':fade/sr})
        write+=count;position=start
    return result*gain,events
def save(name,a):
    wav=out/(name+'.wav');mp3=out/(name+'.mp3')
    assert np.isfinite(a).all() and np.max(np.abs(a))<1
    sf.write(wav,a,sr,subtype='PCM_16')
    subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(wav),
                    '-codec:a','libmp3lame','-b:a','192k',str(mp3)],check=True)
    return {'wav':wav.name,'mp3':mp3.name,'seconds':len(a)/sr,'peak':float(np.max(np.abs(a))),
            'wav_sha256':hashlib.sha256(wav.read_bytes()).hexdigest(),
            'mp3_sha256':hashlib.sha256(mp3.read_bytes()).hexdigest()}
loop,events=menu(80);loop_item=save('menu-loop-review',loop)
loop_item['loop_events']=events
rounds=json.loads((root/'unity/Assets/StarTournament/Resources/Audio/Music/manifest.json').read_text())['tracks']
reviews=[]
for seconds in (2,4):
    parts=[];scenarios=[]
    for i,track in enumerate(rounds):
        # Alternate launch points so the comparison does not rely on one convenient splice.
        launch_position=8 if i%2==0 else 20
        timeline,_=menu(launch_position+24)
        menu_segment=timeline[int((launch_position-8)*sr):int((launch_position+16)*sr)]
        source=root/'unity/Assets/StarTournament/Resources'/str(track['phases'][0]['resource']+'.wav')
        incoming,rate=sf.read(source,dtype='float32',always_2d=True);assert rate==sr
        n=int(seconds*sr);a,b=envelopes(n);mixed=menu_segment.copy()
        mixed[8*sr:(8+seconds)*sr]=menu_segment[8*sr:(8+seconds)*sr]*a+incoming[:n]*gain*b
        mixed[(8+seconds)*sr:]=incoming[n:16*sr]*gain
        item=save(f'{track["id"]}-transition-{seconds}s',mixed)
        item.update(track=track['id'],transition_start=8,transition_seconds=seconds,
                    menu_source_position_at_launch=launch_position,match_source_sha256=hashlib.sha256(source.read_bytes()).hexdigest())
        scenarios.append(item)
        # A short fade at review segment boundaries marks independent test scenarios.
        pad=int(.15*sr);mixed[:pad]*=np.linspace(0,1,pad)[:,None];mixed[-pad:]*=np.linspace(1,0,pad)[:,None]
        parts.append(mixed)
    montage=save(f'transitions-comparison-{seconds}s',np.concatenate(parts))
    reviews.append({'transition_seconds':seconds,'montage':montage,'scenarios':scenarios,
                    'timeline':[{'start':i*24,'track':t['title'],'transition_at':i*24+8} for i,t in enumerate(rounds)]})
provenance={'source':str(args.source),'source_sha256':hashlib.sha256(args.source.read_bytes()).hexdigest(),
            'method':'selected concept PCM excerpt; beat-aligned two-voice loop and bounded equal-power envelopes',
            'raw_tempo_estimate':np.asarray(tempo).tolist(),'beats':beats.tolist(),'loop_candidates':candidates,
            'selected_content_markers':chosen,'menu_manifest':manifest,
            'menu_clip_sha256':hashlib.sha256((out/'menu-arena.wav').read_bytes()).hexdigest(),
            'gain':gain,'loop_review':loop_item,'transitions':reviews,
            'scope':'offline prediction of source timeline, not human listening or exact Player evidence'}
(out/'menu-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(out/'provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'markers':manifest,'loop_events':events,'transition_reviews':[(r['transition_seconds'],r['montage']['seconds']) for r in reviews]},ensure_ascii=False))
