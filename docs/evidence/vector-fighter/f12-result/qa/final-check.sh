#!/bin/sh
set -eu
mkdir -p .local/vector-f12/final-native
STAR_TOURNAMENT_GRIP_FACTORY=1 STAR_TOURNAMENT_GRIP_MESH=1 STAR_TOURNAMENT_WALK_EVIDENCE="$PWD/.local/vector-f12/final-native" STAR_TOURNAMENT_QA_LAB_HISTORY="$PWD/.local/vector-f12/final-native/history.json" UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter 'FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene;OrbitalGripDoesNotAccumulateAcrossFrozenSamples;FactorySupportGripsKeepForearmDirectionAndNaturalWrist;SupportWristAlignmentPreservesPalmDigitsAndBoneLengths' -testResults "$PWD/.local/vector-f12/final-native/results.xml" -logFile "$PWD/.local/vector-f12/final-native/editor.log" > .local/vector-f12/final-native/runner.log 2>&1
