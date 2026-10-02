#!/usr/bin/env python3
"""Import the user-selected v2 sketches as short Unity Resources clips.

Usage: python3 tools/prepare-native-audio.py --source PATH_TO_AUDIO_EXPLORATION_V2
"""

import argparse
import json
import struct
import shutil
import wave
from pathlib import Path


RATE = 32000
DEST = Path(__file__).resolve().parents[1] / "unity/Assets/StarTournament/Resources/Audio"


def read(path):
    with wave.open(str(path), "rb") as f:
        assert f.getframerate() == RATE and f.getnchannels() == 1 and f.getsampwidth() == 2, path
        return list(struct.unpack("<" + "h" * f.getnframes(), f.readframes(f.getnframes())))


def write(name, values):
    with wave.open(str(DEST / (name + ".wav")), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(struct.pack("<" + "h" * len(values), *values))


def cut(values, start, end, fade_in=0.005, fade_out=0.015):
    part = values[round(start * RATE):round(end * RATE)]
    rise, fall = round(fade_in * RATE), round(fade_out * RATE)
    for i in range(min(rise, len(part))):
        part[i] = round(part[i] * i / max(1, rise))
    for i in range(min(fall, len(part))):
        pos = len(part) - 1 - i
        part[pos] = round(part[pos] * i / max(1, fall))
    return part


def seamless(values, start, end, crossfade=0.06):
    part = values[round(start * RATE):round(end * RATE)]
    overlap = round(crossfade * RATE)
    first, middle, last = part[:overlap], part[overlap:-overlap], part[-overlap:]
    bridge = [round(last[i] * (1 - i / overlap) + first[i] * i / overlap) for i in range(overlap)]
    return middle + bridge


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--movement-source", type=Path, default=Path(__file__).resolve().parents[1] / "docs/evidence/movement-audio-exploration/03-weighted-boot")
    args = parser.parse_args()
    manifest = json.loads((args.source / "selection.json").read_text())
    DEST.mkdir(parents=True, exist_ok=True)
    for event, chosen in manifest["files"].items():
        samples = read(args.source / chosen["name"])
        if event in ("steps", "jump"):
            continue  # Superseded by the separately approved weighted-boot bank.
        elif event == "cutter":
            # The chosen A timbre is sustained in its approved hold preview.
            write("cutter-start", cut(samples, 0, .28))
            held = read(args.source / manifest["queue_previews"]["cutter"])
            write("cutter-loop", seamless(held, .40, 1.40))
        elif event == "menu_confirm":
            # Offline asset edit: exclude the second impact at 80 ms.
            write(event, cut(samples, 0, .08, fade_in=0, fade_out=.02))
        else:
            write(event, samples)
    movement = DEST / "Movement"
    movement.mkdir(exist_ok=True)
    for event in ("step", "jump", "land"):
        for index in range(5):
            name = f"{event}-{index}.wav"
            read(args.movement_source / name)  # Enforce the runtime PCM contract.
            shutil.copy2(args.movement_source / name, movement / name)
    print("prepared", len(list(DEST.glob("*.wav"))), "short Unity audio clips in", DEST)


if __name__ == "__main__":
    main()
