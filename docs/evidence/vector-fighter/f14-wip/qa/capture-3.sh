#!/bin/sh
set -eu
mkdir -p .local/vector-f14/study-3
STAR_TOURNAMENT_GRIP_OVERRIDES="$PWD/.local/vector-f14/fit-next/grip.txt" STAR_TOURNAMENT_GRIP_FACTORY=1 STAR_TOURNAMENT_GRIP_MESH=1 STAR_TOURNAMENT_WALK_EVIDENCE="$PWD/.local/vector-f14/study-3" STAR_TOURNAMENT_QA_LAB_HISTORY="$PWD/.local/vector-f14/study-3/history.json" UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter 'FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene' -testResults "$PWD/.local/vector-f14/study-3/results.xml" -logFile "$PWD/.local/vector-f14/study-3/editor.log" > .local/vector-f14/study-3/runner.log 2>&1
