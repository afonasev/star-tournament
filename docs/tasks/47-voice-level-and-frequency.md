# Voice level and bot reaction frequency — 2026-10-10

Baseline: `1deb6de0b9ede1be2b4a6c187d8fcf6678f6013b`. Branch: `codex/voice-level-and-frequency`. Worktree: `/Users/eaafonasev/Projects/star-tournament-voice-tuning` (sparse checkout). Primary checkout has unrelated Unity ProjectSettings changes; preserved.

User scope: +20% loudness for bot phrases and kill-series announcements; bot phrases may appear more frequently, at most once every ten seconds. Applied +20% asset amplitude after normalization (+1.5836 dB), rather than source gains that the existing mix limiter would cancel. Regenerated all 20 MP3s from original TTS sources with the existing radio/tempo processing and added final ×1.2 in the generator. Two kill-series WAVs amplified ×1.2. Existing resource paths, GUIDs, format, pitch and duration preserved. Presentation policy: initial silence 10s, global and per-bot cooldown 10s; chance, eligibility and repeat suppression unchanged.

QA selected before execution from changed assets, presentation policy and their dependencies:
- Offline ffmpeg decode before/after: all 22 RMS ratios within 1.18–1.22 (MP3 encoding tolerance), no decoded peak at or above 1, identical decoded sample counts. Passed 22/22; detailed hashes, metrics and ratios: `docs/evidence/voice-level-and-frequency/audio-verification.json`.
- Python generator syntax via `ast.parse`; `git diff --check`; JSON evidence parsing. No Unity or Player build involved.
- Affected Unity command: `unity/tools.sh test-edit-filter 'BotBanterTests;BotBanterVoiceCatalogTests'`. Selected for cooldown/event/repeat behavior and imported resource catalog. **NotRun**: disk has under 1 GiB free; full worktree creation failed with no space left, and isolated Unity import cannot be provisioned. Added exact 10-second boundary coverage, unexecuted.

Full suite, Player/release builds, deployment/installed-game update and gameplay listening remain deferred. No builds or new caches created. Preserve committed evidence; sparse task checkout is removable after integration, while source and the pending QA record remain in Git.

Cleanup: removed task-owned Python bytecode. No temporary WAVs remain. The small sparse worktree and branch are retained for the pending affected Unity QA; no merge or installed-game update has been claimed. Failed managed worktree creation rolled itself back; existing unrelated `/Users/eaafonasev/.codex/worktrees/8937/star-tournament` was left untouched.
