# Affected QA
Baseline main 3fcffe879c77ce8fe20167a1c2778cd410f729d1; approved structure recorded 2026-10-15.
Changes: Well map revision 2, taller slabs/rooms, shared room grid, floor-2 corridor, ten eligible corner spawns, nine pickups, stairs/terminals, presentation offsets and immutable authoring version.
Commands (shared, -noaudio -muted from startup):
- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'WellArenaTests'`: canonical geometry, identity, inventory, authoring/Lab-history compatibility, lift phase/restore contracts.
- `UNITY_RUN_MODE=shared unity/tools.sh test-play-filter 'WellArenaPhysicsTests;WellArenaMatchTests'`: every spawn/pickup physical clearance, eight-participant allocation/contact pickups, all routes, bidirectional stair traversal, both lift middle exits, actual unavailable-lift stair following, outer corridors, windows/grates, pause/restore/Repeat.
- `STAR_TOURNAMENT_QA_LAB_HISTORY=<owned path> UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -runTests -testPlatform PlayMode -testFilter WellArenaVisualTests -testResults <owned path> -logFile <owned path>`: offscreen render with graphics enabled, inspect eight views. No desktop input or audio device access.
New Library import/compile and runner admission are setup, not test execution. Never treat missing XML as a pass.
No full suite, delivery Player, installer or publication. Editor is sufficient for current physics/navigation/render checks. Human Player/gameplay/device acceptance remains pending.

Expanded dependency: the new immutable Well authoring version must decode old Lab histories. Added a frozen trusted revision-1 authoring generator, narrow historical registry selection and compact-schema resolution. Run both old schema-1 and compact schema-2 Well migration with exact old hashes and current custom lift speed, plus existing read-only-authoring and compact-tamper contracts:
`UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'WellArenaTests;DesignLabHistoryTests.AuthoringAndDiagnosticMetadataCannotBeSavedAsGameplayTuning;DesignLabHistoryTests.LevelAuthoringIsExcludedWhileGameplayAndReadabilityRemainVisible;LabParameterStorageTests.CompactSavesHaveOnlyParametersAndExactRestartIdentity;LabParameterStorageTests.SelfConsistentCompactTamperingIsRejectedWithoutWriting'`.
The existing complete real-match fixture is repeated after this initialization dependency change. No full history or performance suite is requested or run.
