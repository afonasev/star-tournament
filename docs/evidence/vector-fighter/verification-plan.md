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
