#!/bin/sh
set -eu
root=$(pwd)
python3 - <<'PY'
from pathlib import Path
import shutil,json,hashlib
art=Path('unity/Assets/StarTournament/Art')
for kind in ['body','arms']:shutil.copyfile(Path('.local/vector-f11')/f'candidate-{kind}.glb',art/f'trooper-{kind}-animated.glb')
p=art/'asset-manifest.json';d=json.loads(p.read_text())
for e in d['entries']:
 if e.get('key') in ['player-body','player-hands']:e['sha256']=hashlib.sha256((Path('unity')/e['path']).read_bytes()).hexdigest()
p.write_text(json.dumps(d,indent=2)+'\n')
PY
UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -quit -executeMethod StarTournament.ProvingGround.Editor.TrooperPreparation.RefreshExistingScene -logFile "$root/.local/vector-f11/final-import.log" > .local/vector-f11/final-import-runner.log 2>&1
mkdir -p .local/vector-f11/final-native
STAR_TOURNAMENT_GRIP_FACTORY=1 STAR_TOURNAMENT_GRIP_MESH=1 STAR_TOURNAMENT_WALK_EVIDENCE="$root/.local/vector-f11/final-native" STAR_TOURNAMENT_QA_LAB_HISTORY="$root/.local/vector-f11/final-native/history.json" UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter 'FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene;OrbitalGripDoesNotAccumulateAcrossFrozenSamples;MixedReviewHasOwnedMaterialsForEveryParticipant;SupportWristAlignmentPreservesPalmDigitsAndBoneLengths' -testResults "$root/.local/vector-f11/final-native/results.xml" -logFile "$root/.local/vector-f11/final-native/editor.log" > .local/vector-f11/final-native/runner.log 2>&1
STAR_TOURNAMENT_QA_LAB_HISTORY="$root/.local/vector-f11/final-edit-history.json" UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -nographics -runTests -testPlatform EditMode -testFilter 'TrooperImportTests' -testResults "$root/.local/vector-f11/final-edit-results.xml" -logFile "$root/.local/vector-f11/final-edit.log" > .local/vector-f11/final-edit-runner.log 2>&1
