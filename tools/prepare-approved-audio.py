#!/usr/bin/env python3
"""Prepare approved previews without executing historical review generators."""
import array, hashlib, json, math, shutil, wave
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'docs/audio/approved-2026-10-03'
DST = ROOT / 'unity/Assets/StarTournament/Resources/Audio'

def read(p):
    with wave.open(str(p), 'rb') as w:
        assert w.getnchannels() == 1 and w.getsampwidth() == 2, p
        return w.getframerate(), [v / 32768 for v in array.array('h', w.readframes(w.getnframes()))]

def bridge(a, n):
    # Rotate the loop then overlap its tail and head. No silence or demo envelope.
    return a[n:-n] + [a[-n+i]*(1-i/n)+a[i]*(i/n) for i in range(n)]

def write(name, rate, a):
    with wave.open(str(DST / (name+'.wav')), 'wb') as w:
        w.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        w.writeframes(array.array('h', [round(max(-1,min(1,v))*32767) for v in a]).tobytes())

def main():
    for item in json.loads((SRC/'verified-inputs.json').read_text()):
        assert hashlib.sha256((SRC/item['file']).read_bytes()).hexdigest() == item['sha256'], item
    mapping = {
        'rocket':'rocket/variant-1.wav', 'explosion':'explosion-short/explosion-1.wav',
        'shield-hit':'shield-v2/hit-0-0.wav', 'weapon-pickup':'weapon-pickup/pickup-2.wav',
        'shield-pickup':'shield-charge/charge-2.wav', 'damage-pickup':'bonus-review/damage-3.wav',
        'speed-pickup':'bonus-review/speed-1.wav', 'heal-pickup':'healing-v2/heal-2.wav',
        'damage-bonus-spawn':'remaining/spawn-3.wav', 'damage-bonus-pickup':'damage-alert-v2/alert-3.wav',
        'jump':'remaining/jump-1.wav', 'land':'remaining/land-1.wav',
        'menu_move':'remaining/move-3.wav', 'menu_confirm':'remaining/confirm-3.wav', 'menu_back':'remaining/back-3.wav',
        'rifle':'variant-2-0.wav', 'shotgun':'shotgun-v5/variant-1-0.wav',
    }
    for i in range(4): mapping[f'rifle-{i}']=f'variant-2-{i}.wav'
    for i in range(2):
        mapping[f'shotgun-{i}']=f'shotgun-v5/variant-1-{i}.wav'
        mapping[f'body-hit-{i}']=f'body-hit/hit-2-{i}.wav'
    for i,n in enumerate(['2','3','fall-only']): mapping[f'death-{i}']=f'death-v2/death-{n}.wav'
    for name, file in mapping.items(): shutil.copyfile(SRC/file, DST/(name+'.wav'))
    rate,a=read(SRC/'cutter/loop-0.wav');r,b=read(SRC/'cutter/loop-3.wav');assert rate==r
    # Same 50/50 equal-power formula as the accepted preview. Smooth only the wrap.
    mixed=[(.5*a[i%len(a)]+.5*b[i%len(b)])/math.sqrt(.5) for i in range(rate*4)]
    write('cutter-loop',rate,bridge(mixed,round(rate*.03)))
    rate,a=read(SRC/'rocket-flight/flight-2.wav')
    write('rocket-flight',rate,bridge(a[round(rate*.2):round(rate*2.8)],round(rate*.04)))
    template=(DST/'rifle.wav.meta').read_text()
    for p in DST.glob('*.wav'):
        meta=p.with_suffix('.wav.meta')
        if not meta.exists():
            guid=hashlib.md5(('StarTournament/approved-2026-10-03/'+p.name).encode()).hexdigest()
            import re
            meta.write_text(re.sub(r'guid: \w+', 'guid: '+guid, template))
    manifest={'copies':mapping,'cutter':{'formula':'(0.5*loop0 + 0.5*loop3)/sqrt(0.5)','hold_seconds':4,'wrap_crossfade_seconds':.03,'attack_seconds':.055,'release_seconds':.14},'rocket-flight':{'source':'rocket-flight/flight-2.wav','crop_seconds':[.2,2.8],'wrap_crossfade_seconds':.04},'outputs':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in DST.glob('*.wav') if p.stem in mapping or p.stem in ['cutter-loop','rocket-flight']}}
    (SRC/'runtime-preparation.json').write_text(json.dumps(manifest,indent=2))
    print('Prepared',len(mapping)+2,'clips; approved one-shots copied byte-for-byte.')

if __name__=='__main__':main()
