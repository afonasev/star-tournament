# VECTOR — focused checks

Baseline: `main` / `00fbd33209eb755dd5f86499aa8ea12867a2efd0`.
Owned branch: `codex/vector-fighter`. Main has foreign Unity settings changes; preserve them.

Diff: new original costume geometry/identity material role, same skeleton/actions/glove provenance, additive airborne poses; pass grounded to world presentation; trooper profile v9 with historical v8 reconstruction.

- Blender exported GLB readback and studio front/back/helmet. **Visual FAIL** at current iteration: flat/simple shells, helmet proportions and surface detail remain below selected reference. This is not a finished model.
- `unity/tools.sh prepare`: necessary import and persisted prefab/scene regeneration. No Player build.
- `unity/tools.sh test-edit-filter 'TrooperImportTests;WeaponSwitchPresentationTests;DesignLabHistoryTests'`: asset binding, materials/weights, grip switch and historical-profile compatibility.
- `unity/tools.sh test-play-filter 'VectorAirbornePresentationTests;TrooperPresentationTests;ParticipantColorIdentityTests;DeathRagdollTests'`: affected skeletal/view/color/death contracts only, not full suite.
- Actual Editor Play Mode sequences for world movement/jump/land/death and four FPS weapons remain required. Studio images cannot establish gameplay acceptance.

Full suite / Player / device / human acceptance: NotRun; no claim. Paid services are excluded by user decision. API/network exploratory attempts produced no external 3D asset; implementation continues locally in Blender.

2026-10-20 execution update: the first focused EditMode process exited 2 after disk-full errors writing result XML and Unity state. No XML exists, so this is incomplete evidence, not a passing test run. Log retained in editmode-incomplete-disk-full.log. The final model also fixes duplicate skin vertex groups discovered by offline validation.

The rerun replaces the whole DesignLabHistoryTests fixture with OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes, whose cases include the newly added BeforeVectorFighter and prior factory retunes. This directly covers the changed predecessor reconstruction without unrelated historical permutations. Import/weapon-switch tests remain selected. Assertions and durations are unchanged.

Focused EditMode rerun: 21/21 Passed (focused-editmode.9Xm1z0/editmode.xml), including the new predecessor case. Final prepare passed. Retained only the generated TrooperProfile block in the scene; unrelated regenerated defaults and object identifiers were removed from the diff.

Next PlayMode uses the same four selected fixtures via `unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter 'VectorAirbornePresentationTests;TrooperPresentationTests;ParticipantColorIdentityTests;DeathRagdollTests'`, with graphics enabled for saved RenderTexture captures. Shared runner; no desktop focus/input/audio or performance measurement. Isolated QA history and results in .local/unity-evidence/vector-playmode. Optional native walk and skeletal pose captures are enabled. Pose fixture screenshots validate presentation states, not an ordinary player-driven jump trajectory.

Initial graphical PlayMode: 8 Passed / 5 Failed. Preserve playmode-initial-fail.xml and log. Findings: new identity role unnecessarily selected mode 2 instead of the existing neutral-albedo mode 1; duplicate same-name mesh assets across body/FPS exports; renamed legacy vector-armored-hands node; corpse settling changed when all airborne world poses were forced into the two-hand aim clip. Fixes retain the existing node/mode contract, deduplicate only verified identical mesh data during prepare, and preserve the previous world locomotion clip selection beneath the additive leg pose. Also remove the oversized elbow hinge from the FPS mesh while keeping it on the world body. Ragdoll settling fix remains a hypothesis until rerun; no physics tuning or assertion changes.

Next affected rerun: EditMode TrooperImportTests and WeaponSwitchPresentationTests; graphical PlayMode the five failed methods, VectorAirbornePresentationTests, and FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene for refreshed final FPS captures. Historical-profile tests already passed and their implementation is unchanged.

Fix1 PlayMode: 4/7 pass, 3 fail. Shared mesh/FPS node/walk/new airborne fixture pass. Body glove ceramic still inherits the FPS node's mode; body and FPS renderer names must differ while sharing their mesh. Ragdoll failure remains identical (0.750596344 vs <0.3). Next diagnostic temporarily disables only additive hip/knee amplitude to isolate whether the new leg pose causes settling. This ablation is not delivery or acceptance evidence. Original source saved at .local/vector-fighter/VectorAirbornePose.cs.before-ablation and must be restored.

Airborne-leg ablation: 1/1 diagnostic pass. This isolates the settling regression to the added leg pose, not the replacement mesh. Restored the real airborne source; next candidate uses a symmetric tuck with the same configured amplitudes. Physics parameters and test thresholds remain unchanged.

Symmetric airborne tuck: 2/2 Passed (real additive pose enabled), including unchanged corpse settling/pause/respawn and airborne/FPS reset fixture. The earlier asymmetric pose is rejected. No physics or test threshold changes.

Final graphical pose/FPS run: 2/4 passed; both failures are legacy layout assertions requiring TECI_helmet, UpperLeg renderer names and more than three material slots. Those implementation details no longer represent the chosen new model. Existing legacy assertions are retained unchanged for the original asset. VECTOR now checks actual colored submesh vertices weighted to all 13 named armor regions (head, chest, spine, both upper/lower arms, upper/lower legs and feet), neutral cloth/visor/hardware modes, and FPS VECTOR plate color/maps in addition to the original glove ceramic. This preserves and strengthens regional color coverage while allowing shared mesh batching. Run ParticipantColorIdentityTests against this adapted asset contract.

Regional VECTOR coverage assertions passed; the remaining color fixture failure counted both retained glove ceramic and the new forearm paint as one old slot. Retained the original exact glove count and added a separate exact count for the single shared VECTOR paint slot.

Visual FPS inspection found an open elbow cuff after removing the oversized hinge. Added closed limb ends and the upper-arm fabric continuation to the FPS mesh. Rerun import/weapon checks, color ownership, shared assets, airborne and walk capture. The final walk fixture records every existing 20ms simulation sample for a short real Unity FPS video; assertions, simulation duration and speed are unchanged.

## F1 visual refinement — 2026-10-21

Baseline `d71baf3d37d7b5e54b9a35b762637ac592fa13e1`, branch `codex/vector-fighter`.
User requests slimmer FPS arms, smaller armor, matte dark cloth, and closer concept fidelity.
Scope: Blender-authored geometry/materials, regenerated two GLBs and persisted prefabs;
rig/actions/camera/grips/motor unchanged. Readability dimensions are authored asset content.
Checks: candidate GLB renders first; then `unity/tools.sh prepare`, focused
`unity/tools.sh test-edit-filter 'TrooperImportTests;WeaponSwitchPresentationTests'`;
graphical shared/muted PlayMode filter `ParticipantColorIdentityTests;TrooperPresentationTests.FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene;TrooperPresentationTests.SeatsShareAssetsButNeverRenderAnotherFirstPersonView;VectorAirbornePresentationTests`.
Use existing walk capture for all four weapons, 20 representative frames; no full suite,
Player build or benchmark. This is sufficient for mesh/material/color ownership and
unchanged attachment/animation contracts; visual comparison remains separate.

F1 first geometry/material run: 5/5 focused PlayMode passed. Actual FPS still shows a
white sleeve patch beside the camera. The point equipment fill is at camera origin;
inverse-square attenuation overexposes the nearest surface. Move that light behind
the camera via named `view.fillBackMeters` (Trooper @10), preserving intensity,
layer and weapon/camera transforms. Historical @9 reconstructed with
BeforeDiffuseEquipmentFill; test prior saved values/hashes and fill attachment.
Extend EditMode selection to FirstPersonWalkTests and
DesignLabHistoryTests.OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes.
Existing version assertions in FirstPersonWalkTests advance from stale @8 to current
@10; exact old @7 and fill strengths/ranges are retained.

F1 expanded EditMode: 31/32 passed; all seven history cases passed. The existing
FirstPersonWalkTests.ResponseIsCadenceIndependentAndFadesAfterStoppingOrLeavingGround
failed: WalkOffset contained the new 0.025 m airborne offset. Preserve its original
zero-ground-step assertion; expose combined ViewOffset separately and consume that
in the weapon-switch root. Airborne fixture checks ViewOffset and independently
asserts no ground steps. Rerun FirstPersonWalkTests, asset/weapon checks and the
focused graphical set. No history source changed after its seven passing cases.
Rear armor/helmet intersections found visually were corrected in the final candidate;
a queued graphical command was cancelled before admission so it would not capture
an intermediate model. No executed test was interrupted.

Final geometry and ViewOffset split: EditMode 25/25 passed; graphical PlayMode 5/5
passed. The .45 m light retreat removes the white sleeve patch but makes equipment
too dark visually. Tune only its new @10 default to .25 m; preserve intensity and
all existing test thresholds. Repeat the same graphical set to inspect all weapons
and verify per-seat fill profile binding; no asset or history migration code changed.

F1 final .25 m fill: graphical PlayMode 5/5 passed. Inspected actual Rifle,
Shotgun, RocketLauncher and Cutter frames: no white cloth patch, smaller forearm
armor, glove/weapon detail visible, no visible sleeve holes in these samples.
Final source differs from the passing 25/25 EditMode only in the new fill distance
(default/scene .45 → .25 m); final graphical seat test verifies that exact binding.
Seven historical registry cases passed in the retained initial 31/32 run; no
history migration code changed afterward. Full suite, Player build, ordinary full
world action video, performance/device/human acceptance remain NotRun.

## F2 grip and shoulder refinement
Baseline 41c8773d375039d92af5038590f3b2d6abf9606e. Authored shoulder/torso geometry and small armor detail; diagnose actual primary grip and correct it. Preserve thin forearms/matte cloth.
First capture diagnostic actual Unity grip from multiple directions with the existing walk fixture. Then prepare changed GLBs and run TrooperImportTests, WeaponSwitchPresentationTests plus TrooperPresentationTests.OrbitalGripDoesNotAccumulateAcrossFrozenSamples, FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene, ParticipantColorIdentityTests, VectorAirbornePresentationTests. Add an actual contact contract once geometry is measured. Shared, -noaudio -muted; isolated QA history. No full suite or Player build.

F2 diagnosis: palm middle proximal x=0.1679 in weapon mount space vs rifle outer grip face x=0.055. A -12 degree wrist yaw closes the palm to x=0.0801 with distal phalanx x=-0.0423. First candidate erroneously restored the prefab attachment after sampling and displaced the gun; rejected despite two old stability checks passing. Restore prior additive wrist/mount transforms BEFORE graph evaluation, then preserve the actual sampled weapon world pose during fitting. New focused contact test checks palm interval, distal crossing, and unchanged weapon/support hand in aim/fire.
Profile @11 adds named grip.primaryWristYaw; reconstruct @10 with BeforePrimaryGripFit and cover its saved-history case. Repeat FirstPersonWalkTests for profile initialization/upgrade. Exact selected history case: OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes("BeforePrimaryGripFit").
Final graphical filter also includes OrbitalGripDoesNotAccumulateAcrossFrozenSamples and DeathRespawnAndRepeatClearViewAndCorpseState, because wrist/mount pre-sample restoration touches reset/death behavior.

F2 final EditMode: 27/27 passed, including actual primary palm/weapon invariance in aim/fire, existing weapon switch/walk/import checks and BeforePrimaryGripFit saved-history case. XML/log retained as f2-editmode-pass.*. No failed assertions or weakened thresholds in this final run.

F2 final graphical PlayMode: 6/6 passed. Inspected all four final FPS captures and rifle grip side views. Final body/arm hashes match f2-source.json and actual studio render-source.json. Source/profile snapshot is unchanged since the 27/27 EditMode result. All Unity runs used shared wrapper, -noaudio -muted, separate QA histories.
No full suite, Player build, release, ordinary full world action video, physical device/performance or human acceptance was performed.

## F3 all-weapon grip / local test Player
Baseline 2f13175cf113f27d82275b26f7ab88b43d694e25, codex/vector-fighter.
First expand existing capture helper to inspect both hands and exact weapon mesh bounds.
Diagnostic: shared graphical/muted walk fixture with isolated Lab history, all four weapons.
Then select affected grip/switch/airborne/death/reset and saved-history contracts after actual diff.
Player build explicitly requested in successor handoff; local build and muted native smoke only.
Full suite, install, release/deploy not authorized.

F3 refinement: per-weapon absolute palm contacts, support palm yaw/roll/finger/thumb
opposition and length-preserving arm solve; primary fit retains established handle
geometry. Sampled local transforms restored before graph evaluation, including unkeyed
channels. Trooper @12; BeforeWeaponGripFits reconstructs @11. Weapon presentation
chooses current visible slot before grip evaluation (including switch apex and muzzle).
Affected EditMode: WeaponSwitchPresentationTests; FirstPersonWalkTests; exact saved
history BeforeWeaponGripFits and BeforePrimaryGripFit cases. Graphical PlayMode:
walk capture, frozen grip, death/respawn/repeat, VectorAirbornePresentationTests and
NativeWeaponSwitchPresentationTests (if present). No asset change: prior import and
identity results remain valid; whole game/full suite not repeated.

F3 final fit uses segment-specific support curls so MCP joints support the underside
and the next phalanges wrap the outside. Absolute per-weapon contacts avoid drift
between aim/fire/locomotion samples. Final graphical set 4/4 passed (f3-final3);
all four FPS and both sides of both hands inspected. Intermediate fits that passed
structural QA but clipped side plates were rejected visually. Final EditMode reruns
27 affected cases after the final metadata/segment curl changes.

F3 final EditMode: 27/27 passed (f3-qa/final-editmode.xml), including both predecessor
history cases and four primary/support contact cases across aim/fire/air/reset.
Final graphical fit: 4/4 passed; no assertion failures or weakened contracts.
Prepare succeeded; retained only intended TrooperProfile @12 scene block. Other
regenerated scene/default/serializer settings changes discarded from this owned worktree.

First native window launch stayed inactive before scenario execution: NotRun; stopped
only own PID 21604. Native batch launch exited 0 and completed actual four-weapon
jump/landing/fire assertions, but every ScreenCapture failed (no window backbuffer).
Preserved first build identity and logs. Replace only development review capture with
direct seat-camera RenderTexture output; no gameplay/profile change, prior QA remains
valid. Rebuild authorized local test candidate, then repeat native scenarios.

Native switch smoke initial FAIL: exact resting-root assertion in legacy fixture.
The fixture teleports to old (0,-1.2,+/-5) coordinates and begins switching without
settling; current air/landing ViewOffset is valid and must stay separate from zero
ground WalkOffset. Preserve all exact comparisons, durations and switch contracts.
Use actual authored spawns, require all participants grounded after 100 settling
ticks and reset their visual poses at that clock. Capture the same sequence directly
from cameras. This changes development evidence setup only, not gameplay or QA
assertions. Preserve switch-initial-fail.log.gz; repeat exact native switch witness.

Grounded native fixture still FAILed the legacy root comparison. This is the same
pre-F1 assumption: root == rest, whereas the accepted F1/F2 runtime contract is
root == rest + ViewOffset (ground WalkOffset remains independent and zero in air).
Update the diagnostic's expected root to configured rest + actual legitimate
ViewOffset, preserving exact equality, switch durations, all model/fire/apex/pause/
seat/death assertions and thresholds. No disabling air presentation or gameplay
changes. The separate airborne and 4-weapon real jump witnesses retain coverage.
Preserve switch-grounded-fixture-fail.log.gz.

Final exact Player source 36667859544f6fb214a4b1981749ef39cb07c8ef: build exit 0,
STAR_PROVING_BUILD_PASS 411961820. Native switch smoke 39 checks / 36 transitions
passed; all four weapons real-session jump/landing/fire passed. Both native commands
exit 0, direct camera captures saved and inspected; no backbuffer capture errors in
final runs. f3-build.json records commands, identity and classification.
Gameplay/grip/profile source is unchanged since the final 27/27 EditMode and 4/4
graphical PlayMode snapshot; subsequent changes affect development review helpers.
Full suite, install, release, deploy, physical controller/audio/performance and human
acceptance remain NotRun. No merge.
