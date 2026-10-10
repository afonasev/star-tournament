#!/usr/bin/env python3
"""Reproduce the bot radio voice bank. Run with edge-tts==7.2.8 via uv.
No API key; network synthesis is required only when a source is missing.
"""
import asyncio
import hashlib
import json
from pathlib import Path
import re
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/bot-banter-full-voice'
BANK = ROOT / 'unity/Assets/StarTournament/Resources/Audio/Banter'
CATALOG = ROOT / 'unity/Assets/StarTournament/Runtime/NativeBotBanterVoice.cs'
VOICE = 'ru-RU-DmitryNeural'
RATE, PITCH, RATIO = '+12%', '-18Hz', .94
# Offline asset authoring tempo; runtime AudioSource pitch remains 1.
SPEECH_TEMPO = 1.2
FILTER = (f'[0:a]asetrate=22560,aresample=48000,atempo={SPEECH_TEMPO/RATIO:.9f},'
          'highpass=f=130,lowpass=f=3500,equalizer=f=550:t=q:w=0.8:g=2,'
          'equalizer=f=2200:t=q:w=1:g=2,acompressor=threshold=0.075:ratio=3:attack=2:release=75:makeup=2,'
          'volume=1.3,asoftclip=type=tanh:threshold=0.8:output=0.85:oversample=4,lowpass=f=4000[voice];'
          '[1:a]highpass=f=900,lowpass=f=4500[noise];'
          '[voice][noise]amix=inputs=2:duration=first:normalize=0[radio]')
NOISE = 'anoisesrc=color=white:sample_rate=48000:amplitude=0.0014:seed=42'
META = ROOT / 'unity/Assets/StarTournament/Resources/Audio/Banter/dazhe-ne-pocarapal-radio.mp3.meta'

def run(*args):
    return subprocess.run(args, check=True, capture_output=True, text=True)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def loudness(text):
    return json.loads(re.findall(r'\{\s*"input_i".*?\}', text, re.S)[-1])

async def source(entry, semaphore):
    import edge_tts
    path = EVIDENCE / 'source' / (entry['key'] + '.mp3')
    async with semaphore:
        if not path.exists():
            for attempt in range(3):
                try:
                    await edge_tts.Communicate(entry['speech'], VOICE, rate=RATE, pitch=PITCH, volume='+0%').save(str(path))
                    break
                except Exception:
                    path.unlink(missing_ok=True)
                    if attempt == 2: raise
                    await asyncio.sleep(2 ** attempt)
    return path

def process(entry, src):
    dst = BANK / (entry['key'] + '.mp3')
    base = ['ffmpeg', '-hide_banner', '-nostdin', '-y', '-i', str(src), '-f', 'lavfi', '-i', NOISE]
    first = run(*base, '-filter_complex', FILTER + ';[radio]loudnorm=I=-18:TP=-3:LRA=5:print_format=json[out]',
                '-map', '[out]', '-f', 'null', '-')
    m = loudness(first.stderr)
    normalization = (f"loudnorm=I=-18:TP=-3:LRA=5:measured_I={m['input_i']}:measured_TP={m['input_tp']}:"
                     f"measured_LRA={m['input_lra']}:measured_thresh={m['input_thresh']}:offset={m['target_offset']}:"
                     'linear=true:print_format=json')
    result = run(*base, '-filter_complex', FILTER + ';[radio]' + normalization + '[out]', '-map', '[out]',
                 '-ar', '48000', '-ac', '1', '-codec:a', 'libmp3lame', '-b:a', '128k', str(dst))
    meta = Path(str(dst) + '.meta')
    if not meta.exists():
        meta.write_text(re.sub(r'guid: [a-f0-9]+', 'guid: ' + uuid.uuid4().hex, META.read_text()))
    probe = json.loads(run('ffprobe', '-v', 'error', '-show_format', '-show_streams', '-of', 'json', str(dst)).stdout)
    measured = run('ffmpeg', '-hide_banner', '-nostdin', '-i', str(dst), '-af', 'loudnorm=I=-18:TP=-3:LRA=5:print_format=json', '-f', 'null', '-')
    entry['metrics'] = loudness(measured.stderr)
    entry['duration_seconds'] = float(probe['format']['duration'])
    entry['sample_rate'] = int(probe['streams'][0]['sample_rate'])
    entry['channels'] = probe['streams'][0]['channels']
    entry['sha256'] = {'source': sha(src), 'radio': sha(dst)}
    print(entry['key'], entry['duration_seconds'], entry['metrics']['input_i'], flush=True)

def catalog(entries):
    rows = '\n'.join('            { ' + json.dumps(e['text'], ensure_ascii=False) + ', "Audio/Banter/' + e['key'] + '" },' for e in entries)
    CATALOG.write_text('''// Exact phrase/resource catalog; synchronized by tools/audio/generate_bot_banter.py.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace StarTournament.ProvingGround
{
    public static class NativeBotBanterVoice
    {
        public static readonly IReadOnlyDictionary<string,string> Clips = new ReadOnlyDictionary<string,string>(
            new Dictionary<string,string>(StringComparer.Ordinal)
        {
''' + rows + '''
        });
    }
}
''')
    meta = Path(str(CATALOG) + '.meta')
    if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n')

async def main():
    entries = json.loads((EVIDENCE / 'manifest.json').read_text())['entries']
    assert len(entries) == 20 and len({e['text'] for e in entries}) == 20 and len({e['key'] for e in entries}) == 20
    EVIDENCE.mkdir(parents=True, exist_ok=True); (EVIDENCE / 'source').mkdir(exist_ok=True);BANK.mkdir(parents=True, exist_ok=True)
    semaphore = asyncio.Semaphore(3)
    sources = await asyncio.gather(*(source(e, semaphore) for e in entries))
    for entry, src in zip(entries, sources): process(entry, src)
    catalog(entries)
    provenance = {'voice': VOICE, 'provider': 'Microsoft Edge online TTS via edge-tts', 'edge_tts_version': '7.2.8',
                  'rate': RATE, 'pitch': PITCH, 'pitch_ratio': RATIO, 'speech_tempo': SPEECH_TEMPO, 'filter_complex': FILTER, 'noise': NOISE,
                  'normalization': 'two-pass loudnorm I=-18 LUFS / TP=-3dBTP / LRA=5; measurements decoded MP3',
                  'direction': 'lower rough male, sharper pace and compressed/saturated radio timbre; emotional aggression requires human listening',
                  'acceptance': 'pending human/gameplay listening', 'entries': entries}
    (EVIDENCE / 'generation.json').write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + '\n')

if __name__ == '__main__': asyncio.run(main())
