# Combat sounds over music

Baseline: `ccfd8c17`. Task branch: `codex/combat-music-mix`.

Own and nearby gunfire gets `audio.weaponGain` default 2 (+6 dB before shared normalization), including the sustained cutter. Rifle/shotgun/rocket recordings and banks are unchanged. Shots, explosions and active cutter sound temporarily duck music: floor .25, full-duck reference .3 in effective effects gain, release .3 s. Distance and user effects volume weight the amount; inaudible sources leave music alone. Release advances on the music clock, including suspension semantics, without restarting music or shifting pitch. Stopping effects/mute/Effects=0/restore clears ducking. A floor >=.1 and the unchanged global normalized budget preserve some music under saturation. All four controls have named Lab descriptors, bounds/step/labels/descriptions and historical-release fallbacks. Saved user Music/Effects and music balance settings/hashes are preserved.

Diagnostic source evidence: `source-levels.json` samples the unchanged PCM files for up to five seconds, using peak and loudest 50 ms RMS. Rifle is -21.3 dBFS on the latter measure; Reactor reaches -10.9 dBFS before runtime gains. This explains a masking risk but is not a listening acceptance result.

QA: EditMode 15/15 (43.30 s XML duration); initial PlayMode 23/24 (113.38 s). One new assertion included the configured volume of stopped pool slots in the audible sum. Corrected it to playing sources, kept the <=1.0001 cap and added the exact one-playing-voice assertion. The complete own+near/all-three-guns scenario passed on rerun. All 24 distinct affected cases closed. Initial compile-only failure was NotRun and corrected before tests; no runtime change between passing runs. See compressed XML/logs, exact filter scope files and `qa-summary.json`; startup/import/compile/admission are separate from XML duration.

Verified source behavior: own and nearby gun source volumes increase >1.4x versus former gain; music stays positive and below 40% of its former level, source-to-music gain ratio improves >4x, distance/pan/range remain. Nearby sustained cutter lowers music, crowded combat stays within the shared budget, music release respects time/pause, zero-effects/restore clear it, historical profiles work without being rewritten, and prior death/ready/routing/music lifecycle tests pass. This is automatic gain/routing evidence, not real-game perceptual acceptance.

Deferred: exact Player/device/audio listening, full suite, Player/release builds, publication and installed-game update. No Player built or deployed.

Cleanup: source/specs/tests and compressed evidence preserved in Git/main. Managed worktree archived after owned Unity processes exited; its Library/Logs/UserSettings and temporary test/compile outputs removed. Merged task branch deleted safely. Primary checkout and unrelated dirty ProjectSettings retained.
