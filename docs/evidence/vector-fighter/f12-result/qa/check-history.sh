#!/bin/sh
set -eu
mkdir -p .local/vector-f12/history
STAR_TOURNAMENT_QA_LAB_HISTORY="$PWD/.local/vector-f12/history/isolated.json" UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -nographics -runTests -testPlatform EditMode -testFilter 'OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes.*BeforeStableSupportGrip;OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes.*BeforeAlignedSupportWrist;OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes.*BeforeClosedVerticalGrips' -testResults "$PWD/.local/vector-f12/history/results.xml" -logFile "$PWD/.local/vector-f12/history/editor.log" > .local/vector-f12/history/runner.log 2>&1
