#!/usr/bin/env python3
"""Author the short mechanical latch used for local weapon readiness.

Asset waveform design only: no gameplay timing or Balance Lab defaults live here.
Deterministic synthesis, no external recording or licensed source material.
"""
import array
import math
from pathlib import Path
import random
import sys
import wave

RATE = 32000
DURATION = .16
SEED = 20261010
DEST = Path(__file__).resolve().parents[1] / "unity/Assets/StarTournament/Resources/Audio/weapon-ready.wav"


def main():
    noise = random.Random(SEED)
    samples = []
    previous_noise = 0
    for index in range(round(RATE * DURATION)):
        t = index / RATE
        white = noise.uniform(-1, 1)
        high = white - previous_noise
        previous_noise = white
        value = 0
        # One immediate latch and a closely spaced mechanical settling contact.
        for onset, strength in ((0, 1), (.018, .48)):
            age = t - onset
            if age < 0:
                continue
            attack = min(1, age / .0008)
            value += strength * attack * (
                .42 * high * math.exp(-age / .009)
                + .36 * math.sin(2 * math.pi * 310 * age) * math.exp(-age / .027)
                + .28 * math.sin(2 * math.pi * 1780 * age) * math.exp(-age / .023)
                + .16 * math.sin(2 * math.pi * 2870 * age) * math.exp(-age / .014)
            )
        samples.append(value * min(1, (DURATION - t) / .012))
    # Offline peak ceiling: keep headroom before the runtime's shared SFX budget.
    scale = 10 ** (-1.5 / 20) / max(abs(x) for x in samples)
    pcm = array.array("h", (round(x * scale * 32767) for x in samples))
    if sys.byteorder != "little":
        pcm.byteswap()
    DEST.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(DEST), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm.tobytes())
    print(DEST)


if __name__ == "__main__":
    main()
