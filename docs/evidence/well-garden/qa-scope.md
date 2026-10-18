# Affected QA

Baseline: a8438e3c676c07c71cedc4c631514005ec01a56f (`main`). Change: presentation-only WellLabGarden + single call in WellArenaPresentation, GAME_SPEC, targeted tests.
Dependencies: original garden deck, actual ground-floor portals, existing two beds, Well v5 lighting and NativeVisualPresentation, mesh ownership/budgets. No simulation, navigation, collider, profile or authoring changes.

Minimum sufficient commands, run from owned worktree:

1. `tools/unity.sh shared -batchmode -noaudio -muted -runTests -testPlatform EditMode -testFilter 'WellArenaTests;WellDaylightProfileTests' -testResults .../editmode.xml -logFile .../editmode.log -nographics`: immutable map/history/authoring and accepted v5 profile preservation.
2. `tools/unity.sh shared -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter 'WellGardenPresentationTests;WellStoreyPresentationTests' -testResults .../playmode.xml -logFile .../playmode.log`: real rendering, both actual entrances, connected flagstones, clear landing, no cosmetic colliders, 56 furniture/24 lights, original renderer/vertex bounds, unchanged storey materials. Graphics enabled for real PNGs, no Player build.

Inspect real PNGs before completion. Preserve failures/NotRun; no weakened old assertions or expanded timeout. Full tests, natural matches, Player build/device/human/release/deploy not run or implied by these checks.

Logs retained as `.log.gz`. EditMode ran before art refinements; unchanged authoring/profile source was not rerun. Final PlayMode ran after each related art/import change and checks pre-garden authoring bytes, budgets and GPU texture RGB.
