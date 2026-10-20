#!/bin/sh
set -eu
root=$(pwd)
export STAR_TOURNAMENT_QA_LAB_HISTORY="$root/.local/all-arena-circles/play-history.json"
export STAR_TOURNAMENT_ALL_CIRCLES_OUTPUT="$root/.local/all-arena-circles/views"
export STAR_TOURNAMENT_WELL_CIRCLES_OUTPUT="$root/.local/all-arena-circles/well-regression-views"
exec "$HOME/.local/bin/unity-run" --shared --project "$root/unity" -- /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -noaudio -muted -projectPath "$root/unity" -runTests -testPlatform PlayMode -testFilter 'ArenaPickupCircleCoverageTests;WellPickupCirclePresentationTests' -testResults "$root/.local/all-arena-circles/playmode.xml" -logFile "$root/.local/all-arena-circles/playmode.log"
