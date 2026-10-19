# User data compatibility — focused source check

Baseline: `01ca6df0`, branch `codex/game-standard-alignment`. Prior application source: `edd095ae`. Requirement: UPDATES-DATA-01 at game-spec-templates `0c2d4562683252d54736f294dc1af463050eac17`.

Changes: serialization preservation tests only; runtime format unchanged. Dependencies: actual Unity JsonUtility, PlayerProfileCatalog, DesignLabHistory, GamepadLookSettings. Execute in shared Editor with `-noaudio -muted -nographics`; no Player, installer, packaging, full suite or deployment.

Command: `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'PlayerProfilesTests;LabParameterStorageTests.UnsupportedStorageSchemaPreservesSourceAndDisablesWrites;LabParameterStorageTests.CompactSavesHaveOnlyParametersAndExactRestartIdentity;LabParameterStorageTests.LegacyCompactionKeepsCustomHiddenParametersAndBackup;LabParameterStorageTests.FailedCompactionPreservesLegacyAndSelection'`.

Selected contracts: legacy profile read/default values without rewriting, unsupported document/schema preserved and writes denied, compact restart identity, explicit legacy compaction keeps original backup, atomic publish failure keeps original/selection. Excludes the large external-history fixture, gameplay and unrelated UI. These checks do not prove native installed rollback, registry/plist paths, historical release formats, or future schema migration.

Additional startup lifecycle check: `UNITY_RUN_MODE=shared unity/tools.sh test-play-filter 'NativeLabStartupTests.InvalidHistoryKeepsDefaultAndLabCannotWriteSource;NativeLabStartupTests.UnloadCancelsBlockedHistoryWithoutCreatingFile'`. Reason: actual menu/Lab write controls and cancellation depend on Unity lifecycle; EditMode serialization alone cannot prove this. Uses fixture-owned history and wrapper-owned QA paths, silent shared Editor; no Player build.
