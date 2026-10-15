# QA scope
Baseline main 973c28773518eebd64bbab4f43ed66821d5770d8. Owned branch codex/well-material-depth.
Changed: Well decoration/materials/daylight; Well-only reflection capture selection; Well v3 art controls and exact historical profile projection.
Minimum affected EditMode: WellSurfaceMeshTests;WellArenaTests. Union geometry/wall joins, exact authored map and old Lab records. Add explicit v2 predecessor/hash test. PlayMode: WellLabDetailVisualTests, same 23 poses, same runtime daylight setup, no added colliders, shader/material/resources checks. Neighbor graphics test for unchanged non-Well reflection path when applicable.
Commands:
- unity/tools.sh test-edit-filter 'WellSurfaceMeshTests;WellArenaTests'
- UNITY_RUN_MODE=shared STAR_TOURNAMENT_QA_LAB_HISTORY=<owned>/qa-lab-history.json unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellLabDetailVisualTests -testResults <owned>/visual.xml -logFile <owned>/visual.log
No nographics on visual pass. All autonomous launches silent from process start via unity-run shared. No full, Player build, release or deploy. Editor is sufficient for material/render comparisons. Captures are not human/device/gameplay/performance acceptance.

Follow-up after render inspection: WellContactMesh static hemisphere visibility and URP contact shader; final EditMode includes geometry/visibility test. VisualPolishTests.SwitchingEffectsRestoresBaselineAndKeepsGeometry checks non-Well shared binding (passed 15.10). Final captures at native 1920×1080, same 23 camera poses (earlier baseline 1280×720). No resampling used. Geometry tier capped at 650k vertices to account for actual bevels and static contact subdivision, lights remain 24 and renderers <64.
