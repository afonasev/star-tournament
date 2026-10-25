# Shotgun weight preview — 2026-10-25

Baseline: main 2eb532e195a80d33ec11bb272648aa0a1d94655c. Owned branch: codex/shotgun-weight.

Request: reduce spread by 20% (6 to 4.8 degrees), darker pellets, heavier gunshot.
Audio direction selected: Quake or between Quake and DOOM. Exact revision-3 preview and bronze/graphite presentation remain pending. Earlier A/B/C candidates are rejected.
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

## Revision 2 — original firearm recordings

User rejected all A/B/C processed runtime variants as unlike a powerful shotgun or real firearm. Do not integrate those candidates.
The historical runtime recipe already pitch-shifted the sources to 0.74 and lower, layered rumble and repeated grains; further filtering compounded that direction.
New previews use original Model 12, Mossberg, Nova recordings already retained in the repository; original pitch and transients, no synthetic bass, added echo or layering. Each preview contains two real takes, peak normalized per bank (not loudness matched).
Source/license verified: https://opengameart.org/content/the-free-firearm-sound-library — CC0, Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney.
Reproduce: `python3 tools/preview-shotgun-recorded.py`. Outputs and source/output hashes: `.local/shotgun-recorded-v2/manifest.json`.
Validation: all three previews generated and reopened as 48 kHz mono PCM16; no full-scale clipping. Human listening/selection pending. Runtime and gameplay still unchanged; requested spread 6 to 4.8 degrees and darker pellets remain part of the pending implementation.

## Revision 3 — Quake / between Quake and DOOM

User selected Quake or an intermediate Quake/DOOM direction after reviewing game references. New authored CC0-based sketches: 1 arena-dry, 2 arena-full. Do not use game-extracted sounds in runtime.
Recipe preserves the original-pitch attack, blends a band-limited low-mid recorded layer at 0.88 playback rate and one 9ms recorded snap; short envelope, parallel saturation after the attack. No synthetic rumble or added reverb. Recipe values are offline preview authoring parameters, not runtime gameplay/Lab settings.
`python3 tools/preview-shotgun-arena.py` generates two bank takes each, plus two five-second previews (two isolated shots and three at 0.7-second cadence). Results and hashes in `.local/shotgun-arena-v3/manifest.json`.
Generation/readback checks passed: mono PCM16 48 kHz; peaks below full scale. Human listening and resemblance judgment pending. Runtime/gameplay unchanged; no Unity or build was necessary for this preview-only revision. Keep this worktree/media for the selected sound implementation and the original spread/color request.
