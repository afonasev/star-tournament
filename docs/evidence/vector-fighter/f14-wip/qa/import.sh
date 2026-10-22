#!/bin/sh
set -eu
python3 - <<'PY'
from pathlib import Path
import shutil,json,hashlib
art=Path('unity/Assets/StarTournament/Art')
for kind in ['body','arms']:shutil.copyfile(Path('.local/vector-f14')/f'candidate-{kind}.glb',art/f'trooper-{kind}-animated.glb')
p=art/'asset-manifest.json';d=json.loads(p.read_text())
for e in d['entries']:
 if e.get('key') in ['player-body','player-hands']:e['sha256']=hashlib.sha256((Path('unity')/e['path']).read_bytes()).hexdigest()
p.write_text(json.dumps(d,indent=2)+'\n')
PY
UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -quit -executeMethod StarTournament.ProvingGround.Editor.TrooperPreparation.RefreshExistingScene -logFile "$PWD/.local/vector-f14/import.log" > .local/vector-f14/import-runner.log 2>&1
