#!/bin/sh
set -eu
sh .local/vector-f14/import-13.sh
mkdir -p .local/vector-f14/final-geometry
STAR_TOURNAMENT_GRIP_FACTORY=1 STAR_TOURNAMENT_GRIP_MESH=1 STAR_TOURNAMENT_WALK_EVIDENCE="$PWD/.local/vector-f14/final-geometry" STAR_TOURNAMENT_QA_LAB_HISTORY="$PWD/.local/vector-f14/final-geometry/history.json" UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter 'FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene' -testResults "$PWD/.local/vector-f14/final-geometry/play.xml" -logFile "$PWD/.local/vector-f14/final-geometry/play.log" > .local/vector-f14/final-geometry/play-runner.log 2>&1
UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform EditMode -testFilter 'TrooperImportTests' -testResults "$PWD/.local/vector-f14/final-geometry/edit.xml" -logFile "$PWD/.local/vector-f14/final-geometry/edit.log" > .local/vector-f14/final-geometry/edit-runner.log 2>&1
