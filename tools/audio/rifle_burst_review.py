#!/usr/bin/env python3
"""Offline, reproducible rifle direction review; never plays audio or edits runtime assets."""
import argparse, array, hashlib, json, math, subprocess, wave
from pathlib import Path

RATE = 48000

def decode(path, filters='anull'):
    return list(array.array('f', subprocess.check_output([
        'ffmpeg', '-v', 'error', '-i', str(path), '-af', filters,
        '-ac', '1', '-ar', str(RATE), '-f', 'f32le', '-'])))

def save(path, samples):
    assert samples and all(math.isfinite(x) for x in samples)
    assert max(map(abs, samples)) < 1, 'Clipping'
    with wave.open(str(path), 'wb') as out:
        out.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        out.writeframes(array.array('h', [round(x * 32767) for x in samples]).tobytes())
    return {'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'peak': max(map(abs, samples)), 'rms': math.sqrt(sum(x*x for x in samples)/len(samples)),
            'duration': len(samples)/RATE}

def mix(bank, times):
    result = [0.] * (round(max(times)*RATE) + max(map(len, bank)))
    for n, t in enumerate(times):
        offset = round(t*RATE)
        for i, x in enumerate(bank[n % len(bank)]):
            result[offset+i] += x
    return result

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(); args.output.mkdir(parents=True, exist_ok=True)
    source = args.root/'docs/audio/approved-2026-10-03'
    # All three directions derive from the approved CC0 AK-47 bank, retaining its real attack.
    variants = [
        ('0-current', 'Текущий звук', 'anull'),
        ('1-dry-heavy', '1 · Сухой тяжёлый', 'highpass=f=65,bass=g=7:f=150:w=0.7,treble=g=-3:f=3500,acompressor=threshold=0.07:ratio=3:attack=1:release=40:makeup=1.5'),
        ('2-mechanical', '2 · Резкий механический', 'highpass=f=80,bass=g=4:f=190:w=0.8,equalizer=f=1800:t=q:w=1:g=3,acompressor=threshold=0.08:ratio=2.5:attack=2:release=30:makeup=1.4'),
        ('3-low-punch', '3 · Низкий удар', 'asetrate=43200,aresample=48000,highpass=f=55,bass=g=8:f=115:w=0.7,treble=g=-5:f=2800,acompressor=threshold=0.06:ratio=4:attack=1:release=45:makeup=1.7')]
    banks=[]; files=[]; sources=[]
    for key, label, filters in variants:
        bank=[]
        for i in range(4):
            src = args.root/f'unity/Assets/StarTournament/Resources/Audio/rifle-{i}.wav'
            samples=decode(src, filters)
            if key != '0-current':
                duration = .19 if key.startswith('1') else .16 if key.startswith('2') else .24
                samples=samples[:round(duration*RATE)]
                fade=round(.055*RATE)
                for j in range(max(0,len(samples)-fade),len(samples)):
                    samples[j] *= (len(samples)-j)/fade
                # Match early-body RMS so character can be compared without a loudness trick.
                window=samples[:round(.08*RATE)]
                rms=math.sqrt(sum(x*x for x in window)/len(window))
                gain=min(.095/max(rms,1e-9),.66/max(map(abs,samples)))
                samples=[x*gain for x in samples]
            bank.append(samples)
            if key=='0-current': sources.append({'path':str(src.relative_to(args.root)), 'sha256':hashlib.sha256(src.read_bytes()).hexdigest()})
        banks.append((key,label,filters,bank))
    # Common attenuation for ALL previews/banks, keeping relative loudness and overlap intact.
    peak=max(max(map(abs,mix(bank,[i/12 for i in range(24)]))) for _,_,_,bank in banks)
    gain=min(1.,.85/peak)
    cards=[]
    for key,label,filters,bank in banks:
        bank=[[x*gain for x in clip] for clip in bank]
        for i,clip in enumerate(bank): files.append(save(args.output/f'{key}-{i}.wav',clip))
        # One shot, two short bursts, then a two-second sustained burst. 720 rpm throughout.
        times=[.15]+[.95+i/12 for i in range(5)]+[1.9+i/12 for i in range(5)]+[2.85+i/12 for i in range(24)]
        files.append(save(args.output/f'{key}-review.wav',mix(bank,times)))
        cards.append(f'<section><h2>{label}</h2><audio controls preload="none" src="{key}-review.wav"></audio></section>')
    manifest={'purpose':'Direction selection only; not gameplay acceptance', 'cadence_rpm':720,
              'license':'CC0', 'source':'https://opengameart.org/node/21826',
              'authors':'Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney',
              'sources':sources,'common_gain':gain,'filters':{key:f for key,_,f,_ in banks},'files':files}
    (args.output/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    (args.output/'index.html').write_text('<!doctype html><meta charset="utf-8"><title>Автомат · выбор звука</title><style>body{background:#121923;color:#e9edf4;font:18px system-ui;max-width:900px;margin:40px auto;padding:20px}section{padding:18px;margin:16px 0;background:#202c3b;border-radius:12px}audio{width:100%}h2{font-size:22px}p{color:#c4cedd}</style><h1>Автомат · 720 выстр./мин</h1><p>В каждом примере: одиночный выстрел, две короткие очереди, длинная очередь. Воспроизведение только по нажатию. Варианты 1–3 — направления для выбора; в игру ещё не добавлены. Здесь номинальные 720 выстр./мин; действующий игровой таймер при 50 Hz пока даёт 600.</p>'+''.join(cards))
    print(json.dumps({'output':str(args.output),'files':len(files),'max_peak':max(f['peak'] for f in files)},ensure_ascii=False))

if __name__=='__main__': main()
