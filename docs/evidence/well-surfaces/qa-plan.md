# QA scope
Baseline b9c7cbe8ff41c3ce14102ed1c627845b426ba861. Only presentation textures and triangle material partition change; no authored/gameplay/profile values change.

- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter WellSurfaceMeshTests`: preserve exact vertices, triangles, UV/normals/tangents/contact colours, winding, coverage and joined surfaces.
- `UNITY_RUN_MODE=shared STAR_TOURNAMENT_QA_LAB_HISTORY="$PWD/.local/surfaces-visual/history.json" unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellLabDetailVisualTests -testResults "$PWD/.local/surfaces-visual/visual.xml" -logFile "$PWD/.local/surfaces-visual/visual.log"`: exact authoring/no collider changes, shader and material routing, unchanged resource limits; 30 prior + 3 ceiling views reviewed.

Wrapper route inspected: shared unity-run, -noaudio -muted from start, isolated QA history, no Player build. Full/gameplay simulation not warranted by presentation-only diff. Player/device/performance/human acceptance and release/deploy remain open.
