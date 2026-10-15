# QA scope
Baseline main a8f96e3eee81f7911ea9f083589534a8634b7188. Presentation-only: materials, decorative modules and plants, tangent generation. Authored geometry, physics, simulation, camera/input and Lab migration unchanged.

- `unity/tools.sh test-edit-filter 'WellSurfaceMeshTests;WellArenaTests'`: world UV/tangent geometry and closed F1 joints plus immutable map/items/spawns.
- Graphics-enabled Editor: `UNITY_RUN_MODE=shared STAR_TOURNAMENT_QA_LAB_HISTORY=<owned path> unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellLabDetailVisualTests -testResults <owned XML> -logFile <owned log>`: 23 saved views; actual map equality, no decor colliders, supported shaders/normal maps and existing resource limits.

No `-nographics` for render capture; both commands use unity-run shared and quiet flags before startup. NativeVisualPresentation uses local preference pipeline as before. No Player build required: affected render paths are observable in Editor. Full suite, Player/build/release/deploy and human/device/performance acceptance remain open and are not claimed.
