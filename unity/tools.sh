#!/bin/sh
set -eu
project_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
editor=${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}
evidence_dir="$project_dir/../.local/unity-evidence"
mkdir -p "$evidence_dir"
case "${1:-}" in
  prepare) exec "$editor" -batchmode -nographics -quit -projectPath "$project_dir" -executeMethod StarTournament.ProvingGround.Editor.ProvingGroundBuild.Prepare -logFile "$evidence_dir/prepare.log" ;;
  test-edit) exec "$editor" -batchmode -nographics -projectPath "$project_dir" -runTests -testPlatform EditMode -testResults "$evidence_dir/editmode.xml" -logFile "$evidence_dir/editmode.log" ;;
  test-play) exec "$editor" -batchmode -nographics -projectPath "$project_dir" -runTests -testPlatform PlayMode -testResults "$evidence_dir/playmode.xml" -logFile "$evidence_dir/playmode.log" ;;
  build) exec "$editor" -batchmode -quit -projectPath "$project_dir" -executeMethod StarTournament.ProvingGround.Editor.ProvingGroundBuild.BuildMac -logFile "$evidence_dir/build.log" ;;
  *) echo "Usage: unity/tools.sh prepare|test-edit|test-play|build" >&2; exit 2 ;;
esac
