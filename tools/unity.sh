#!/bin/sh
set -eu
repo_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
project_dir="$repo_dir/unity"
runner=${UNITY_RUNNER:-$HOME/.local/bin/unity-run}
editor=${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}
[ -x "$runner" ] || { echo "Install ~/.local/share/unity-run/install.sh first" >&2; exit 2; }
case "${1:-}" in
  shared|exclusive)
    mode=$1; shift
    # Direct Editor test calls obey the same full-suite policy as unity/tools.sh.
    run_tests=0
    test_filter=
    next_filter=0
    for arg in "$@"; do
      if [ "$next_filter" = 1 ]; then test_filter=$arg; next_filter=0; continue; fi
      case "$arg" in -runTests) run_tests=1 ;; -testFilter) next_filter=1 ;; esac
    done
    if [ "$run_tests" = 1 ]; then
      [ "$next_filter" = 0 ] || { echo "Missing -testFilter value" >&2; exit 2; }
      if [ -z "$test_filter" ] && [ "${CONFIRM_FULL_TESTS:-}" != 1 ]; then
        echo "Unfiltered tests require separate human confirmation; CONFIRM_FULL_TESTS=1 records that confirmation." >&2
        exit 2
      fi
      if [ -z "${STAR_TOURNAMENT_QA_LAB_HISTORY:-}" ]; then
        evidence_dir="$repo_dir/.local/unity-evidence"
        mkdir -p "$evidence_dir"
        test_dir=$(mktemp -d "$evidence_dir/direct-tests.XXXXXX")
        STAR_TOURNAMENT_QA_LAB_HISTORY="$test_dir/qa-lab-history.json"
        export STAR_TOURNAMENT_QA_LAB_HISTORY
      fi
    fi
    [ -d "$project_dir" ] || { echo "Missing Unity project: $project_dir" >&2; exit 2; }
    has_noaudio=0; has_muted=0; human_audio=0; automated=0
    for arg in "$@"; do
      case "$arg" in
        -noaudio) has_noaudio=1 ;;
        -muted) has_muted=1 ;;
        -humanAudio) human_audio=1 ;;
        -batchmode|-runTests|-executeMethod|-diagnostic|*Review) automated=1 ;;
      esac
    done
    if [ "$has_noaudio" = 1 ] || [ "$has_muted" = 1 ] || [ "$human_audio" = 0 ] || [ "$automated" = 1 ]; then
      if [ "$has_noaudio" = 0 ] && [ "$has_muted" = 0 ]; then exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -noaudio -muted -projectPath "$project_dir" "$@"; fi
      if [ "$has_noaudio" = 0 ]; then exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -noaudio -projectPath "$project_dir" "$@"; fi
      if [ "$has_muted" = 0 ]; then exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -muted -projectPath "$project_dir" "$@"; fi
    fi
    exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -projectPath "$project_dir" "$@" ;;
  player)
    shift
    mode=${UNITY_RUN_MODE:-exclusive}
    [ "$mode" = shared ] || [ "$mode" = exclusive ] || exit 2
    [ "$#" -gt 0 ] || { echo "Provide direct Player executable" >&2; exit 2; }
    executable=$1; shift
    has_noaudio=0; has_muted=0; human_audio=0; automated=0
    for arg in "$@"; do
      case "$arg" in
        -noaudio) has_noaudio=1 ;;
        -muted) has_muted=1 ;;
        -humanAudio) human_audio=1 ;;
        -batchmode|-runTests|-executeMethod|-diagnostic|*Review) automated=1 ;;
      esac
    done
    if [ "$has_noaudio" = 1 ] || [ "$has_muted" = 1 ] || [ "$human_audio" = 0 ] || [ "$automated" = 1 ]; then
      if [ "$has_noaudio" = 0 ] && [ "$has_muted" = 0 ]; then exec "$runner" "--$mode" -- "$executable" -noaudio -muted "$@"; fi
      if [ "$has_noaudio" = 0 ]; then exec "$runner" "--$mode" -- "$executable" -noaudio "$@"; fi
      if [ "$has_muted" = 0 ]; then exec "$runner" "--$mode" -- "$executable" -muted "$@"; fi
    fi
    exec "$runner" "--$mode" -- "$executable" "$@" ;;
  *) echo "Usage: tools/unity.sh shared|exclusive [Editor arguments] | player EXECUTABLE [arguments]" >&2; exit 2 ;;
esac
