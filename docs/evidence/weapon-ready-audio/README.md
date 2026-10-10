# Weapon ready audio

Baseline: `6dc4662a`. Task branch: `codex/weapon-ready-audio`.

User request: a clear sound of weapon switching so the moment shooting becomes possible is audible. A deterministic original mechanical latch, .16 s / 32 kHz mono PCM16, replaces no existing asset. Peak -1.50 dBFS, whole-clip RMS -19.16 dBFS. SHA256 `3a9f8c263d8db3a482b2a954d1ac4024291b13ea93bea58488fae511f7cb1df7`. `tools/prepare-weapon-ready-audio.py` reproduces the asset byte-for-byte; preload enabled.

Local readiness is observed after authoritative selection, before fire consumes ammunition/resets cooldown. One cue after completed switch + target cooldown, only with ammunition/energy. Same-slot selection, empty weapons, death/respawn, restored selected state and unmute do not create false readiness. Ordinary held-trigger/edge/release rules are unchanged. No opponent/bot broadcasts; one shared cue for the switching local seat. `audio.weaponReadyGain` default 2, 0–4, step .05; user Effects/mute and shared mix budget still apply. Voice protection is shared with death feedback, without expanding the pool.

Checks: EditMode 18/18 in 33.50 s XML duration. Initial affected PlayMode: 20/22 in 83.13 s, two new test-fixture defects corrected (float-tick expectation and selected-ammo snapshot consistency). Runtime source unchanged between runs. Both corrected tests passed in 23.44 s; all 22 distinct affected cases are closed. See `qa-summary.json`, compressed XML/logs and exact filter scope files. Startup/import/compile/admission are separate from XML duration. No assertions, weapon coverage or production contracts weakened. Reproducible generator execution and `git diff --check` passed after restoring task-owned Unity-generated settings.

Integration: source and selected contracts are unchanged when merged into main; changes in main since baseline are checked before integration. Preserve unrelated primary ProjectSettings.

Deferred: exact Player gameplay listening, timbre/physical audio acceptance, full tests, Player/release builds, publication and installed game update. No Player built or deployed.

Cleanup: source, generation recipe, waveform and compressed evidence preserved in Git/main. Managed task worktree archived after owned Unity processes exited; reconstructible Library/Logs/UserSettings and temporary test outputs removed with it. Task branch deleted only after integration. Primary checkout and its unrelated dirty ProjectSettings retained.
