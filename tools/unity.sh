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
    exec "$runner" "--$mode" --project "$project_dir" -- "$editor" -projectPath "$project_dir" "$@" ;;
  player)
    shift
    mode=${UNITY_RUN_MODE:-exclusive}
    [ "$mode" = shared ] || [ "$mode" = exclusive ] || exit 2
    [ "$#" -gt 0 ] || { echo "Provide direct Player executable" >&2; exit 2; }
    exec "$runner" "--$mode" -- "$@" ;;
  *) echo "Usage: tools/unity.sh shared|exclusive [Editor arguments] | player EXECUTABLE [arguments]" >&2; exit 2 ;;
esac
