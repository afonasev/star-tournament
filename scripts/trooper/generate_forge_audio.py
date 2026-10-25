"""Deterministic original short plasma chirp; offline authoring, never plays audio."""
import math, random, struct, wave
from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=root/'unity/Assets/StarTournament/Resources/Audio/plasma.wav'
rng=random.Random(1025);rate=48000;duration=.12;phase=0;filtered=0;samples=[]
for i in range(round(duration*rate)):
 t=i/rate;phase+=math.tau*(220+1100*math.exp(-t*38))/rate
 filtered=.75*filtered+.25*rng.uniform(-1,1)
 envelope=min(1,t/.002)*math.exp(-t*38)*min(1,(duration-t)/.012)
 value=envelope*(.36*math.sin(phase)+.12*math.sin(phase*1.93)+.16*filtered)
 samples.append(struct.pack('<h',round(max(-1,min(1,value))*32767)))
with wave.open(str(out),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(b''.join(samples))
