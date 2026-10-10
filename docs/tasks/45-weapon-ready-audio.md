# Weapon ready audio — 2026-10-10

Baseline: `6dc4662a`. Branch/worktree: `codex/weapon-ready-audio`, `/Users/eaafonasev/.codex/worktrees/weapon-ready-audio/star-tournament`. Primary checkout has unrelated dirty Unity ProjectSettings; preserve them.

Scope: user requested a clear weapon-switch sound marking when shooting can begin. One local mechanical latch (.16 s), after switch + target cooldown, before shot processing. Existing ammo/trigger rules remain. No opponent broadcast, no duplicated split-screen cues. Add `audio.weaponReadyGain` to named proving-ground Lab registry with metadata/default 2/range 0–4/step .05 and trusted predecessor migration. Synthesis source is `tools/prepare-weapon-ready-audio.py`; asset preload enabled.

QA selected from this diff and dependencies (no Player build):
- `unity/tools.sh test-edit-filter 'NativeMusicTests;WeaponSwitchPresentationTests.AnimationTracksRemainingTimeAndFinishesOnTheFireBoundary;WeaponSwitchPresentationTests.VisibleWeaponChangesAtTheApexAndNeverOnTheFirstHalf;WeaponSwitchPresentationTests.AllWeaponPairsShareTheSamePhaseWithoutMutatingState'`: new control/history migration (including pre-death profiles), existing switch/fire/pose boundaries. Broad history fixture was 52/52 on baseline; no unrelated legacy-registry transform changes.
- `unity/tools.sh test-play-filter 'ApprovedSpatialAudioTests;NativeAudioRoutingTests;NativeRoundMusicTests;NativeMenuMusicTests;NativeCombatTests.RifleHoldAndSameTickSelectionUseDistinctAuthoritativeShots;CutterSessionTests.BeamPiercesTwoTargetsStopsAtWallAndClearsAtFocusBoundary;NativeAchievementSessionTests.ActualMotorFireSwitchAndRestoreCountersExcludeInputAttemptsAndTeleport'`: all four ready cues, cooldown, ordering before same-tick held rifle fire, pool saturation, silence/mute/restore/death/empty cases, shared output, death regression, adjacent trigger/cutter/statistics contracts and common mix.

Full suite, natural matches, Player/release builds, installed game update and publication deferred; listening approval in real gameplay remains separate.

Initial QA: EditMode 18/18. PlayMode 20/22; two new tests had fixture defects (sub-tick float countdown expectation and a selected-ammo mismatch during restock). Runtime source unchanged. Corrected test expectations to authoritative timer states and constructed a valid explicit restock snapshot; all assertions/weapon coverage preserved. Repeat only the two corrected methods:
`unity/tools.sh test-play-filter 'ApprovedSpatialAudioTests.EveryWeaponGetsOneLocalReadyCueOnlyAtTheEndOfItsSwitch;ApprovedSpatialAudioTests.WeaponReadyHasNoGhostsFromRestoreDeathEmptyAmmoOrMute'`.

Final QA: EditMode 18/18; all 22 distinct PlayMode cases closed (20 initial + 2 corrected repeats). Runtime unchanged between runs. Evidence: `docs/evidence/weapon-ready-audio/`. Source/asset/generator/evidence committed and integrated into main; task worktree/cache archived and merged branch removed. Player build, publication and exact gameplay listening remain deferred.
