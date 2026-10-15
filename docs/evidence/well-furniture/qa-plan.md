# QA scope
Baseline main 8529129382a844fe964ad92e861c2a57c916bed0. Map furniture affects obstruction, navigation, sight/fire, spawn safety and immutable authoring history. Existing physics fixture is needed, including all spawn/pickup routes, stairs, real lift users and restore. No broad unrelated game tests.

- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'WellArenaTests;WellSurfaceMeshTests'`: geometry, fixed density/clearance, legacy exact hashes and mesh contracts.
- `UNITY_RUN_MODE=shared unity/tools.sh test-play-filter WellArenaPhysicsTests`: real capsule/shot obstruction, six case bypasses, all original navigation/support/lift contracts.
- `UNITY_RUN_MODE=shared STAR_TOURNAMENT_QA_LAB_HISTORY="$PWD/.local/furniture-visual/history.json" unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellLabDetailVisualTests -testResults "$PWD/.local/furniture-visual/visual.xml" -logFile "$PWD/.local/furniture-visual/visual.log"`: 23 old views + 7 furnishing views, visual review, original 650k/64-renderer/24-light caps unchanged.

Wrappers reviewed: filtered commands start -noaudio -muted through unity-run shared, no Player build. Test-only QA history is isolated. Full/Player/release/deploy not authorized or needed; human/device/performance acceptance remains open.

Visual review correction: removed coplanar body/plinth/cap overlap in furniture meshes. Authored positions/colliders unchanged; repeat visual fixture only, retain successful physics/history evidence.

Final visual rerun used the same command with `.local/furniture-visual-final` in all three output/history paths. Final XML: 1/1 passed, 6.113564 s; all 30 saved captures reviewed.
