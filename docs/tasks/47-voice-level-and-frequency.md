# Voice level and bot reaction frequency — 2026-10-10

Baseline: `1deb6de0b9ede1be2b4a6c187d8fcf6678f6013b`. Branch: `codex/voice-level-and-frequency`. Worktree: `/Users/eaafonasev/Projects/star-tournament-voice-tuning` (sparse checkout). Primary checkout has unrelated Unity ProjectSettings changes; preserved.

User scope: +20% loudness for bot phrases and kill-series announcements; bot phrases may appear more frequently, at most once every ten seconds. Applied +20% asset amplitude after normalization (+1.5836 dB), rather than source gains that the existing mix limiter would cancel. Regenerated all 20 MP3s from original TTS sources with the existing radio/tempo processing and added final ×1.2 in the generator. Two kill-series WAVs amplified ×1.2. Existing resource paths, GUIDs, format, pitch and duration preserved. Presentation policy: initial silence 10s, global and per-bot cooldown 10s; chance, eligibility and repeat suppression unchanged.

QA selected before execution from changed assets, presentation policy and their dependencies:
- Offline ffmpeg decode before/after: all 22 RMS ratios within 1.18–1.22 (MP3 encoding tolerance), no decoded peak at or above 1, identical decoded sample counts. Passed 22/22; detailed hashes, metrics and ratios: `docs/evidence/voice-level-and-frequency/audio-verification.json`.
- Python generator syntax via `ast.parse`; `git diff --check`; JSON evidence parsing. No Unity or Player build involved.
- Affected Unity command: `unity/tools.sh test-edit-filter 'BotBanterTests;BotBanterVoiceCatalogTests'`. Selected for cooldown/event/repeat behavior and imported resource catalog. **NotRun**: disk has under 1 GiB free; full worktree creation failed with no space left, and isolated Unity import cannot be provisioned. Added exact 10-second boundary coverage, unexecuted.

Full suite, Player/release builds, deployment/installed-game update and gameplay listening remain deferred. No builds or new caches created. Preserve committed evidence; sparse task checkout is removable after integration, while source and the pending QA record remain in Git.

Cleanup: removed task-owned Python bytecode. No temporary WAVs remain. The small sparse worktree and branch are retained for the pending affected Unity QA; no merge or installed-game update has been claimed. Failed managed worktree creation rolled itself back; existing unrelated `/Users/eaafonasev/.codex/worktrees/8937/star-tournament` was left untouched.

## Follow-up 2026-10-11: expose frequency in Balance Lab
Baseline `4fd5d50b`. New descriptor `presentation.botBanterIntervalSeconds`, group Боты → Реплики, label Интервал реплик, seconds, default/minimum 10, maximum 120, step 1. Runtime builds the presentation policy from the frozen match profile; applies to initial silence and both cooldowns, text and voice together. Trusted predecessor migration preserves prior hashes and values; custom values persist on save/reopen.
Before QA: select `unity/tools.sh test-edit-filter 'BotBanterTests;BotBanterVoiceCatalogTests;NativeMusicTests'` for cooldown boundaries/custom Lab policy, imported recordings, and adjacent profile-history migrations. This is affected EditMode only, no Player build or full suite. Disk refreshed: 14 GiB available; earlier NotRun can now be resolved.

Final automated QA: 40/40 EditMode passed, zero skipped/inconclusive. Runtime/profile/script compilation succeeded. Selected fixtures include both the ten-second and custom 30-second boundaries, imported recording catalog, new-control save/reopen and trusted history migration, and adjacent audio-history contracts. Evidence: `docs/evidence/voice-level-and-frequency/tests/editmode.xml` and `scope.txt`. This resolves the earlier affected-QA NotRun; no Player build was needed. Actual gameplay/audio listening, Player/release builds, full tests and publication remain deferred.

Integration/cleanup: integrate both task commits into main with the integration lease, preserving unrelated ProjectSettings. Durable source and evidence in Git. Remove this task's temporary sparse worktree, Library/Temp/Logs and local test outputs after integration; no new Player artifacts exist. Keep the primary checkout and all unrelated resources.
