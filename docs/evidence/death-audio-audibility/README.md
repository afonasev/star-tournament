# Death audio audibility

Baseline: `97c42610`. Task branch: `codex/death-audio-audibility`.

`audio.deathGain` default 2 increases the selected death bank by +6 dB before shared normalization. Own and remote cues keep the existing local/remote gains, distance, pan and effects volume. Death voices replace ordinary voices under saturation and finish without being stolen by later shots; the existing pool limit remains. Cutter stops before death mixing. No assets/gameplay/replay changes.

QA: EditMode 63/63 (357.90 s XML duration), PlayMode 15/15 (67.43 s XML duration), zero failed/skipped/inconclusive. Compressed XML and logs attached; scope files name exact filters. Unity startup/import/compile and admission are outside these XML durations. `git diff --check` passed after restoring task-owned Unity-generated ProjectSettings.

Selected PCM source measurements: death-0/1/2 are 1.3 s, peaks -4.8/-4.7/-4.7 dBFS and whole-clip RMS -21.8/-22.2/-23.0 dBFS. Assets unchanged. Measurements and automated source gains are not listening acceptance.

Deferred: real gameplay listening, exact Player/device/audio acceptance, full tests, Player/release builds and deployment. No Player built or published.

Cleanup: source/evidence committed and integrated into main; managed task worktree archived after Unity exit. Reconstructible Library/Temp/Logs/UserSettings and temporary test outputs removed with the task worktree. Primary checkout and unrelated dirty ProjectSettings protected.
