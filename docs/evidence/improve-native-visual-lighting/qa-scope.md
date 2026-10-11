Baseline: 5447059799465b31edce2514036c6591edc9acbb.
Diff: native rendering controls/controller, main profile additive descriptors, explicit material roles, URP SSAO variants/Bloom resources, graphics scroll.
Affected: settings persistence/navigation, global state/lifecycle, generated arenas, 4 cameras, main profile/Lab migration.
Commands: UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter VisualPolishSettingsTests; test-play-filter VisualPolishTests; adjacent IndependentPauseMenuTests/NativeMenuPresentationTests/SettingsGamepadNavigationTests/OrbitalLeaguePresentationTests plus affected profile/Lab migration fixtures as discovered.
No gameplay/AI changes: no natural-match/full suite. No Player/release builds or deployment. Rendered Editor captures verify visible application; device/Player/GPU/human acceptance pending.
Initial EditMode attempt: compilation stopped for missing direct Core RP assembly reference; no tests ran (NotRun). Fixed direct references, rerun required.

PlayMode exposed required additive Lab registry compatibility: historical release hashes must use a trusted pre-visual registry. Added exact predecessor projection, retaining saved snapshots and hashes. Expand affected tests to DesignLabHistoryTests/LabReleaseCatalogTests/LabSnapshotCopyTests/MotorProfileTests.

Rendered route: unity-run --shared --project WORKTREE/unity -- Unity -batchmode -projectPath WORKTREE/unity -runTests -testPlatform PlayMode -testFilter 'VisualPolishTests;IndependentPauseMenuTests;SettingsGamepadNavigationTests;NativeMenuPresentationTests.CompactControlsDoNotCollapseRosterCardsOrLoseSettingsNavigation;OrbitalLeaguePresentationTests' (graphics enabled, isolated QA history, PNGs to screenshots/). No Player build. Rendered four-camera PNG is a composition of independent viewport captures, not a physical display screenshot.

While the registry/EditMode suite ran, isolated presentation fixes (initial binding, probe capture at existing spawn eye height and Null-GPU guard) were made; these do not alter the running preference/history fixtures. The queued rendered PlayMode run imports this final presentation source.

Additional dependency fix: LabBundle.Validate used score.chainTotal1 unconditionally even for a partial bundle. Keep all checks for present match profiles; guard absent profiles like other CheckOrder contracts. Failed LabReleaseCatalog cases and new visual migration regression rerun: 14/14 passed. Original history/copy/motor fixtures: 66 passed; 11 failing cases are superseded by this focused rerun. No assertion/seed/timeout reductions.
Graphical rerun: 3/3 VisualPolishTests passed; 31 adjacent+visual tests passed on earlier run (one wrong single-seat capture fixture was then explicitly changed to four human seats). Capture-only fix: temporarily use full target viewport per camera and sRGB LDR output, compose four independent frames. Final output is not a physical display screenshot.
