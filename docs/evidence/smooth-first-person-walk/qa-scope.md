# Affected QA

Baseline: 8116b624a5ae5136b88a8cebfcb6d1272878c28d; agreed planning revision: 5a4afe7.

Diff: TrooperVisual first-person clip selection and session-clock movement envelope; switch applies shared arms/weapon offset; grounded sampling from CombatPresentation. Trooper profile v7 adds four numeric descriptors; exact trusted v6 projection in history migration. World motion, simulation and camera are unchanged.

Commands selected before execution:
- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'FirstPersonWalkTests;WeaponSwitchPresentationTests;TrooperImportTests.ProfileAndAttributionAreComplete;DesignLabHistoryTests.StartupMigrationKeepsDiskHistoryUntilExplicitLabMutation;DesignLabHistoryTests.MigrationCacheSeparatesShippedMetadataAndKeepsDetachedHistoricalSnapshots;DesignLabHistoryTests.SmoothLtUpgradePreservesLegacyDelayAndHistoricalHash'` — actual persisted rig and clips; slow bounded cycle, response/cadence/freeze/air/reset, fire/hit/switch, descriptors and adjacent historical schemas.
- Graphics-enabled shared Unity Editor PlayMode, filter `TrooperPresentationTests;RifleBulletSessionTests.FirstVisibleFlightUsesStrafedMuzzleThenFreezesLaunchOffset` — affected real scene rigs, per-seat isolation, respawn/pause and frame screenshots. The rifle muzzle/first-flight test covers the changed shot-pose sampling dependency. Isolated Lab history and output paths; no desktop/system input/device/audio dependency. No Player build in either route.

Full suite, Player/release builds, deployment and physical/human perception acceptance remain deferred. No natural matches: AI/long-term gameplay is not changed.

The first graphics PlayMode attempt found a real serialized-profile upgrade defect before scene tests could run: EnsureTrooperDescriptors populated valueIndex via old Set calls before appending new values. The owned Editor was terminated after identifying the repeated startup error; this attempt is incomplete/NotRun, not a passed run. The fix invalidates that cache after the append. Added a real serialized-v6 profile regression test and repeated affected EditMode plus graphics PlayMode after this runtime change.

The nine scene tests passed after the profile-cache fix. Inspection of motion.csv then exposed a weak new fixture: Restore on every sample triggered StateRestored and reset the sway, so its upper-bound assertion was vacuous. Replaced that setup with ordinary movement/action ticks and added a positive motion bound. Reran only `TrooperPresentationTests.FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene` because the correction changes only this fixture; the other eight PlayMode results and the 23 EditMode results remain valid for the same runtime source snapshot.

The ordinary-tick fixture initially failed its exact-zero stationary-seat assertion because capsules had not settled after placement. Kept all assertions/bounds unchanged; added neutral native physics setup ticks and an explicit precondition that the stationary seat is below the walk threshold. This distinguishes actual capsule correction from borrowed first-person motion. No runtime changes were made for this fixture correction.

The settled fixture exposed that its chosen straight path at (-8,0,2) runs immediately into a wall (confirmed in the captured frame), so a positive sustained-walking bound correctly failed. Moved the fixture to the already-tested open north flank at (-8,0,26), yaw 90, with the stationary seat at (8,0,30). Kept the positive motion bound and smoothness bounds unchanged. This changes only test setup; movement/gameplay source remains unchanged.
