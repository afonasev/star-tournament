#!/bin/sh
set -eu
# This records existing human authorization; an agent may not grant it itself.
case "${1:-}" in
  test-edit|test-play)
    [ "${CONFIRM_FULL_TESTS:-}" = 1 ] || {
      echo "Full tests require separate human confirmation (including production). Use a focused filter; set CONFIRM_FULL_TESTS=1 only after that confirmation." >&2
      exit 2
    } ;;
esac
project_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
editor=${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}
evidence_dir="$project_dir/../.local/unity-evidence"
player_app=${UNITY_PLAYER_APP:-$project_dir/Builds/StarTournamentProvingGround.app}
runner=${UNITY_RUNNER:-$HOME/.local/bin/unity-run}
[ -x "$runner" ] || { echo "Install ~/.local/share/unity-run/install.sh first" >&2; exit 2; }
mode=${UNITY_RUN_MODE:-shared}
[ "$mode" = shared ] || [ "$mode" = exclusive ] || exit 2
mkdir -p "$evidence_dir"
isolate_test_history() {
  if [ -z "${STAR_TOURNAMENT_QA_LAB_HISTORY:-}" ]; then
    STAR_TOURNAMENT_QA_LAB_HISTORY="$1/qa-lab-history.json"
    export STAR_TOURNAMENT_QA_LAB_HISTORY
  fi
}
case "${1:-}" in
  lab-stage-releases)
    [ "$#" -eq 2 ] && [ -f "$2" ] || { echo "Usage: unity/tools.sh lab-stage-releases ABSOLUTE_HISTORY_JSON" >&2; exit 2; }
    STAR_TOURNAMENT_LAB_RELEASE_HISTORY="$2" exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -batchmode -quit -projectPath "$project_dir" -executeMethod StarTournament.ProvingGround.Editor.LabReleaseBuild.PromoteMarked -logFile "$evidence_dir/lab-stage-releases.log" ;;
  test-edit-filter|test-play-filter)
    [ "$#" -eq 2 ] && [ -n "$2" ] || { echo "Usage: unity/tools.sh $1 '<testFilter>'" >&2; exit 2; }
    case "$1" in test-edit-filter) platform=EditMode; stem=editmode ;; test-play-filter) platform=PlayMode; stem=playmode ;; esac
    focused_dir=$(mktemp -d "$evidence_dir/focused-$stem.XXXXXX")
    isolate_test_history "$focused_dir"
    {
      echo "scope=focused (affected evidence; NOT a full suite)"
      printf 'platform=%s\nfilter=%s\n' "$platform" "$2"
      printf 'lab_history=%s\n' "$STAR_TOURNAMENT_QA_LAB_HISTORY"
      printf 'revision=%s\n' "$(git -C "$project_dir" rev-parse HEAD)"
      git -C "$project_dir" status --short
    } > "$focused_dir/scope.txt"
    echo "FOCUSED $platform: $2 — affected evidence, NOT a full suite"
    echo "Evidence: $focused_dir"
    exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -batchmode -nographics -projectPath "$project_dir" -runTests -testPlatform "$platform" -testFilter "$2" -testResults "$focused_dir/$stem.xml" -logFile "$focused_dir/$stem.log"
    ;;

  prepare) exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -batchmode -nographics -quit -projectPath "$project_dir" -executeMethod StarTournament.ProvingGround.Editor.ProvingGroundBuild.Prepare -logFile "$evidence_dir/prepare.log" ;;
  test-edit) isolate_test_history "$(mktemp -d "$evidence_dir/full-editmode.XXXXXX")"; exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -batchmode -nographics -projectPath "$project_dir" -runTests -testPlatform EditMode -testResults "$evidence_dir/editmode.xml" -logFile "$evidence_dir/editmode.log" ;;
  test-play) isolate_test_history "$(mktemp -d "$evidence_dir/full-playmode.XXXXXX")"; exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -batchmode -nographics -projectPath "$project_dir" -runTests -testPlatform PlayMode -testResults "$evidence_dir/playmode.xml" -logFile "$evidence_dir/playmode.log" ;;
  build) exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -batchmode -quit -projectPath "$project_dir" -executeMethod StarTournament.ProvingGround.Editor.ProvingGroundBuild.BuildMac -logFile "$evidence_dir/build.log" ;;
  build-release-mac|build-release-win)
    [ -n "${STAR_TOURNAMENT_RELEASE_OUTPUT:-}" ] || { echo "Set absolute STAR_TOURNAMENT_RELEASE_OUTPUT" >&2; exit 2; }
    case "$1" in build-release-mac) target=OSXUniversal; method=BuildReleaseMac ;; build-release-win) target=Win64; method=BuildReleaseWindows ;; esac
    exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -batchmode -quit -projectPath "$project_dir" -buildTarget "$target" -executeMethod "StarTournament.ProvingGround.Editor.ProvingGroundBuild.$method" -logFile "$evidence_dir/$1.log"
    ;;
  editor) mode=${UNITY_RUN_MODE:-exclusive}; exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -projectPath "$project_dir" ;;
  run)
    [ -d "$player_app" ] || { echo "Player is missing: run 'make build' first." >&2; exit 2; }
    mode=${UNITY_RUN_MODE:-exclusive}
    executable=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$player_app/Contents/Info.plist")
    shift
    exec "$runner" "--$mode" -- "$player_app/Contents/MacOS/$executable" "$@" ;;
  *) echo "Usage: unity/tools.sh prepare|test-edit|test-play|test-edit-filter FILTER|test-play-filter FILTER|build|editor|run" >&2; exit 2 ;;
esac
