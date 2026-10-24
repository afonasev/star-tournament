# Individual effect levels in Design Lab

User request: tune each effect relative to the others in Lab; keep ordinary player settings coarse.
Baseline: `main@1b0839ac742e1df95f6f90d9cc5ddfb5672cf02d` (2026-10-24).
Owned branch/worktree: `codex/lab-effect-mix`, `/Users/eaafonasev/Projects/star-tournament-effect-mix`.
Route: small, unambiguous extension of the existing Lab audio descriptor controls; no new screen or product choice.

Added 19 independent `audio.effect.<cue>Gain` multipliers (0–4, step .05, neutral 1): rifle, shotgun, rocket launch, cutter, rocket flight, explosion, armor/body impacts, five pickup cues, three menu cues, bot speech and two kill-series announcements. Existing movement, death, ready and damage-alert levels remain available. Weapon multipliers also use the existing overall weapon level. Saved revision applies to the next match; old snapshots use neutral fallback without rewriting hashes. No player settings controls were added. Spec updated in `docs/GAME_SPEC.md`.

## Verification

- `unity/tools.sh test-edit-filter NativeMusicTests`: 17/17 PASS (profile compatibility and neighboring audio contracts).
- PlayMode `ApprovedSpatialAudioTests`: 16/16 PASS after harness repair; all existing assertions retained.
- PlayMode `DesignLabWorkspaceTests.IndividualEffectFieldsSaveForNextMatchAndRenderInAudioGroup`: 1/1 PASS; 19 searchable/editable fields, independent draft, save/next-match application, actual Editor screenshot.
- Both PlayMode commands: `UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter <above> -testResults <owned.xml> -logFile <owned.log>`, isolated `STAR_TOURNAMENT_QA_LAB_HISTORY`.
- Initial combined PlayMode run: 3 PASS / 14 FAIL. The fixture used the startup-locked game audio; diagnostic unmute cannot override session mute. The fixture now constructs its own consumer against the real session with the engine output disabled and listener zero before scene load. Startup mute protection is unchanged; original failures remain in durable evidence.
- `git diff --check` PASS. No full tests, Player build, release or publication. Human audio/device acceptance remains pending.

Durable screenshot, XML, logs, scope and report: shared planning home `docs/evidence/lab-individual-effect-levels/`. Main checkout's pre-existing Unity settings/untracked files were preserved. Generated settings in the owned worktree were restored after QA.
