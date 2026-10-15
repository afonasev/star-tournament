# QA scope
Baseline main 5b0a6e95, branch codex/well-lab-detail. Diff affects Well presentation, mesh UVs and Well art defaults only. WellArenaCatalog/physics/navigation/serialization unchanged.

- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter "WellSurfaceMeshTests;WellArenaTests"`: union/seams, new UV mapping, immutable Well structure and lifts.
- `UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellLabDetailVisualTests -testResults <owned>/.local/well-detail-visual.xml -logFile <owned>/.local/well-detail-visual.log`: actual render of 20 views, shader support, immutable authoring, no decoration colliders, bounded renderer/vertex/light counts. No -nographics.
- Review all saved PNGs for all floors, lift/stair joints, garden and equipment. Resource counts are not a performance benchmark.
- New worktree import is allowed; Player build, full suite and publication are not required or run. Human/gameplay/device acceptance remains pending.
