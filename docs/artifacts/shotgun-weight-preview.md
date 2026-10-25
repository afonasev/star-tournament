# Shotgun weight preview — 2026-10-25

Baseline: main 2eb532e195a80d33ec11bb272648aa0a1d94655c. Owned branch: codex/shotgun-weight.

Request: reduce spread by 20% (6 to 4.8 degrees), darker pellets, heavier gunshot.
Runtime changes pending selection of audio A/B/C and bronze/graphite presentation.
No gameplay, profile, runtime audio or shipping assets have changed yet.

Reproduce audio: `python3 tools/preview-shotgun-weight.py` (ffmpeg required).
Reproduce palette: `python3 tools/preview-shotgun-colors.py` (Pillow required).
Outputs: owned `.local/shotgun-weight/`; two source takes in each audio preview.
These are processed versions of current shotgun bank recordings, not new licensed sources.
Audio normalization targets common RMS subject to peak ceiling; loudness is not guaranteed equal.
The palette is a diagram, not Unity runtime evidence. Human listening/selection pending.

Checks: preview generation succeeded, WAV mono PCM16 44100 Hz, peaks below full scale; palette PNG inspected. Python compilation and git diff whitespace checks passed. Unity NotRun because runtime is unchanged.
Next: selected sound/pellet direction, update immutable Lab defaults/catalog without rewriting saved revisions, reduce shot.spread, affected profile/resolver/presentation/audio checks and a silent Editor visual witness. No full suite, Player build or deploy authorized.
Keep owned worktree and preview media until selection and implementation; remove intermediate unselected media after evidence preservation. No integration performed.
