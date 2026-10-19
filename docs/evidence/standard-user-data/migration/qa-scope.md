# Versioned user data — affected QA
Baseline613034bf, branch codex/game-standard-alignment. Actual legacy source tag v0.1.2@e497ca6f.

Changed behavior: profile v1 imports read-only into new v2 namespace; Lab v2 imports read-only into new v3 namespace. Explicit saves use new destination only. Existing invalid destination rejects fallback. QA overrides disable production legacy paths. No UI/design/gameplay changes.

Run `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'VersionedUserDataTests;PlayerProfilesTests;LabParameterStorageTests.UnsupportedStorageSchemaPreservesSourceAndDisablesWrites;LabParameterStorageTests.CompactSavesHaveOnlyParametersAndExactRestartIdentity;LabParameterStorageTests.LegacyCompactionKeepsCustomHiddenParametersAndBackup;LabParameterStorageTests.FailedCompactionPreservesLegacyAndSelection'`. Reason: actual Unity JsonUtility and atomic history publication; new namespace tests plus affected prior preservation contracts. Excludes unrelated gameplay, large external history and full suite.

Run `UNITY_RUN_MODE=shared unity/tools.sh test-play-filter 'NativeLabStartupTests.InvalidHistoryKeepsDefaultAndLabCannotWriteSource;NativeLabStartupTests.UnloadCancelsBlockedHistoryWithoutCreatingFile'`. Reason: actual startup path wiring, QA isolation and lifecycle cancellation, without Player build.

Both use shared runner, silent from startup -noaudio -muted. No installer/release/Player build or publication. Native installed/platform/registry/plist/real old Player gates remain NotRun.

The profile publication-failure test was added after the first Editor compilation; run only the new method separately: `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'VersionedUserDataTests.FailedProfilePublicationPreservesLegacyAndRemovesTemporaryFile'`. First run executed 15 cases (six new namespace cases plus nine preservation cases), so it does not count this later method. Runtime source remained unchanged between these checks.
