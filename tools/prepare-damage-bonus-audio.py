#!/usr/bin/env python3
"""Reproducible authored sketches for damage bonus alerts (no runtime synthesis)."""
import math
import struct
import wave
from pathlib import Path

RATE = 32000
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/evidence/damage-bonus-alerts/audio'


def render(variant, collected):
    # Frequencies/envelopes are authored asset content, not gameplay parameters.
    length = (0.76, 0.86, 0.92)[variant] if collected else (0.52, 0.62, 0.68)[variant]
    pulses = (3, 2, 3)[variant] if collected else (2, 2, 1)[variant]
    data = []
    phase = 0.0
    for i in range(round(length * RATE)):
        t = i / RATE
        cycle = length / pulses
        u = (t % cycle) / cycle
        gate = math.sin(math.pi * min(1, u / 0.82)) ** 1.1 if u < 0.82 else 0
        if collected:
            freq = (220, 185, 250)[variant] + (85, 100, 65)[variant] * math.sin(2 * math.pi * u)
        else:
            freq = (260, 300, 230)[variant] + (150, 110, 190)[variant] * u
        phase += 2 * math.pi * freq / RATE
        tone = math.sin(phase) + .28 * math.sin(phase * 2) + .14 * math.sin(phase * 3)
        metal = .09 * math.sin(phase * 4.13) * math.exp(-10 * u)
        envelope = min(1, t / .008, (length - t) / .025)
        data.append(int(32767 * .48 * envelope * gate * (tone + metal)))
    return data


def write(path, data):
    with wave.open(str(path), 'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(RATE)
        f.writeframes(struct.pack('<' + 'h' * len(data), *data))


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    for v in range(3):
        spawn, pickup = render(v, False), render(v, True)
        write(OUT / f'{v + 1}-spawn.wav', spawn)
        write(OUT / f'{v + 1}-pickup.wav', pickup)
        write(OUT / f'{v + 1}-pair.wav', spawn + [0] * RATE + pickup)
