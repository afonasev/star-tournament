# Affected QA selection

Baseline: `fba20397b4fd019b8b02540e5e99abb29ae21eb6`.
Diff: NativeVisualPresentation arena-local light scaling/reset, version 18 visual descriptors/defaults and Lab historical migration, Lunar paving floor role. No gameplay, meshes, textures, participants/equipment materials, camera/input/HUD or project asset settings changes.

Dependencies: Unity Light/Material APIs and renderer lifetime, existing visual strength/off isolation, trusted historical Lab registries and migration.

Selected commands before execution:
- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'VisualPolishSettingsTests;DesignLabHistoryTests'`: metadata, stepped validation, trusted prior registries, immutable hashes/custom/default migration. Broadened to history fixture because the shared default registry version changes.
- GPU Editor via `~/.local/bin/unity-run --shared --project <owned>/unity -- <Unity> -batchmode -projectPath <owned>/unity -runTests -testPlatform PlayMode -testFilter 'ArenaLightAndSurfaceRolesCompareFrozenZonesAndRestoreSources;SwitchingEffectsRestoresBaselineAndKeepsGeometry;GradingAndBloomStayIndependentAndZeroDisablesGrading' -testResults <owned>/playmode.xml -logFile <owned>/playmode.log`. No `-nographics`, isolated `STAR_TOURNAMENT_QA_LAB_HISTORY` and `STAR_TOURNAMENT_QA_LOADING_SCREENSHOTS`. Existing engine executes actual render, reset/rebind, light and material state assertions, four seats × two zone groups × four weapons × three maps. Time frozen, matched poses before/after.
- `git diff --check`, strict validation of this planning change.

No full suite, natural matches, FPS benchmark, Player/release/installer build, publication or installed-app update. Editor capture/technical tests do not establish human/Player/device/visual acceptance.

Second candidate: add bounded visual.ambientLift and reuse existing frozen participants as subjects in the same matching captures. First 60/60 history checks passed; after the new additive descriptor, repeat VisualPolishSettingsTests (actual v17 migration + older visual/grading registries) and the same 3 GPU contracts. No history search algorithm/domain changed; the new predecessor projection removes the new descriptor explicitly.
