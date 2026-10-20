#!/bin/sh
set -eu
task_root=$(pwd)
case "$1" in
 changed) filter='ArenaPickupCircleCoverageTests.TunnelsAllSixAnchors;ArenaPickupCircleCoverageTests.LunarAllElevenAnchors' ;;
 preserved) filter='ArenaPickupCircleCoverageTests.CombatBowlAllTwelveAnchors;ArenaPickupCircleCoverageTests.WellAllElevenAnchors;WellPickupCirclePresentationTests' ;;
 *) exit 2 ;;
esac
export STAR_TOURNAMENT_QA_LAB_HISTORY="$task_root/.local/uniform-circles/$1-history.json"
export STAR_TOURNAMENT_ALL_CIRCLES_OUTPUT="$task_root/.local/uniform-circles/views"
export STAR_TOURNAMENT_WELL_CIRCLES_OUTPUT="$task_root/.local/uniform-circles/well-regression-views"
exec "$HOME/.local/bin/unity-run" --shared --project "$task_root/unity" -- /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -noaudio -muted -projectPath "$task_root/unity" -runTests -testPlatform PlayMode -testFilter "$filter" -testResults "$task_root/.local/uniform-circles/$1-playmode.xml" -logFile "$task_root/.local/uniform-circles/$1-playmode.log"
