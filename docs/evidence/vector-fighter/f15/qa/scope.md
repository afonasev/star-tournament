# F15 affected verification
Baseline: codex/vector-fighter 128ab5628be893312b203891fc406c66bc506056.
Scope: glove surface and skinning, with fixed skeleton/animation/arms/mounts and approved Shotgun shape.
Before native review: generator two connected surfaces assertion; GLB node/animation exact comparison; neutral posed inverse-bind diagnostics for every hand/weapon.
Native import: UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -quit -executeMethod StarTournament.ProvingGround.Editor.TrooperPreparation.RefreshExistingScene
Capture and fixed pose: focused FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene, ArticulatedFingerGripPreservesArmAndWeaponPlacement, OrbitalGripDoesNotAccumulateAcrossFrozenSamples, MixedReviewHasOwnedMaterialsForEveryParticipant.
Import: TrooperImportTests. Profile migration tests only if defaults change.
No full suite, Player, merge, release or deploy. Visual review includes all four weapons and both hands; technical PASS is not human art acceptance.

Additional native phase cases: RifleGripHasNativeFireAndAirbornePhases, ShotgunGripHasNativeFireAndAirbornePhases, RocketGripHasNativeFireAndAirbornePhases, CutterGripHasNativeFireAndAirbornePhases. Each uses actual session fire/jump actions and asserts observed phases. Split per weapon to retain the existing 180s timeout; no timeout or original assertion was relaxed. Final repeat selects capture, fixed-arm and these four cases plus import; material identity and frozen-pose checks passed earlier in F15 and material slots/runtime pose logic were not changed afterward.
