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

## Revision 4 — duration diagnosis

User says revision 3 still does not resemble the reference and may be too fast. Measurement of v3 first takes: 95% energy in 19.0/19.2ms, 99% in 25.1/25.7ms; intended envelope differences did not materially change the heard-body energy distribution. Do not claim the previews met Quake resemblance.
New diagnostic pair: restored recorded body at original pitch, then the same result at 0.7 tempo with pitch preserved. Model 12 recorded body uses bounded RMS gain, not added tonal rumble. First/second take 95% energy at 102.6/142.4ms, 99% at 217.9/220.6ms. These are measured energy durations, not human sound-quality judgments.
Reproduce after v3: `PYTHONDONTWRITEBYTECODE=1 python3 tools/preview-shotgun-duration.py`. Output `.local/shotgun-duration-v4/`, hashes and parameters in manifest. Readback PCM/peak checks passed; no clipping. Listen/selection pending. No gameplay/default/runtime changes or integration. Original spread/color request remains open.

## Revision 5 — heavier explosive impact

User asks for greater weight and a convincing powerful lethal close-range blast. New single direction adds a short band-limited explosive body to the restored recorded shotgun, retains a high-mid crack, and uses soft saturation for density. No global slowdown; no added sinusoidal bass. Explosion source is the repository's approved short explosion with provenance in `docs/audio/approved-2026-10-03/explosion/manifest.json`; original shotgun provenance is unchanged. This is an authored sketch, not sampled Quake/DOOM audio.
Reproduce after v4: `PYTHONDONTWRITEBYTECODE=1 python3 tools/preview-shotgun-heavy.py`. Outputs `.local/shotgun-heavy-v5/`; generator and manifest preserve source hashes and parameters. Each take 0.55s; 95% energy by approximately 148ms, 99% by 200ms. Preview contains two isolated shots, then three at 0.7s cadence. PCM readback and peak checks passed (48kHz mono PCM16, max approximately 0.90). These measurements do not establish artistic acceptance. Runtime, spread and pellets remain unchanged pending preview acceptance/color direction; no integration or build.
Keep worktree and current media for the ongoing sound iteration and original gameplay/color request. Prior rejected versions remain reproducible from committed scripts.

## Authorized implementation and focused QA plan

Approved v5 audio direction in chat ("ок, вроде сильно лучше"). Implementation baseline rebased onto main 03eed6b3. Only shotgun runtime WAVs (byte-identical selected takes), bronze tracer RGB descriptors/rendering and an appended Default Lab tuning revision (spread 6 to 4.8) change. Existing factory metadata and historical Lab values/hashes remain preserved; authored tuning goes through LabProjectStorage and LabReleaseStaging. No damage/pellet/cadence change.
Commands: `tools/unity.sh shared -batchmode -noaudio -muted -nographics -quit -executeMethod StarTournament.ProvingGround.Editor.ShotgunWeightUpdate.Stage -logFile <owned stage log>`; `unity/tools.sh test-edit-filter 'ShotgunWeightTests;ShotgunResolverTests;WeaponRebalanceTests;LabReleaseCatalogTests;LabAuthoringTests'`; graphical muted PlayMode `ShotgunWeightPresentationTests;ApprovedSpatialAudioTests;NativeAudioStartupMuteTests` with a dedicated screenshot. These cover profile history, actual spread resolver, sound routing/mute and real pellet rendering. No full suite, natural matches, Player build or deploy.

### Staging blocker (preserved FAIL)

Stage compiled and stopped at the existing BuildReleaseCatalog guard. Diagnosis: old Default v1 expected `detail.ringRadius=1.65`, packaged execution resolves `1.35`. This is an existing author/runtime pickup-radius migration disagreement, not shotgun tuning. Temporary diagnostics were removed. An intermediate diagnostic run had CS0103 from unqualified Debug; corrected diagnostic run reproduced the exact mismatch. No catalogue/scene was saved; own author history has a pending newly authored snapshot, old immutable source rows remain intact. User asked whether to authorize reconciliation of author Default to current game radius or limit this iteration to audio. No compatibility guard or assertion weakened.
Independent verification continues for the color/audio source and migration code; the packaged-spread assertion and staging depend on resolving that question.

User approved authoring Default ring migration to gameplay 1.35 m on 2026-10-25; immutable source revisions and named profiles must remain exact. Baseline 03eed6b3 reproduced WeaponRebalance historical test cutter.hitRadius failure (focused-editmode.QdEjwT). Affected follow-up: UniformPickupCircleProfileTests, LabAuthoringTests, LabReleaseCatalogTests and ShotgunWeightTests; muted shared Stage then focused EditMode. No full suite/build/deploy.

Corrected staging PASS: appended Default revision 9, hash be98595a9fe50199ef7b97e0203d99555e7674b38e76102013fa9be4431e130e, preserved both published entries exactly. Focused EditMode Fc12mi passed all 61 tests including author/runtime 1.35 preservation. Discarded automatic scene serialization (unrelated descriptor refreshes) and Editor ProjectSettings whitespace; registry preparation supplies added descriptors at load. Added complete effective-value comparison against prior published Default and loaded-session spread assertion for final source check.

Final comparison R9zZgU: 6/7 PASS, failure well.daylight (new source froze historical .8 vs existing runtime 2). Correct Stage to copy all editable current gameplay values before applying shotgun delta; preserve original author records. Regenerate own unpublished snapshot/catalog, rerun ShotgunWeightTests full-value parity and rendered-session test. No test weakened.

Rebased on main 966da02f (FORGE). Stage diagnostic found existing author/package disagreement for Default v1: well.daylight .8->2, well.innerAngle 100->95, well.fill .42->.32, well.lampRange 12->18, well.daylightShadow .45->.85. Original packaged v1/v2 do not contain a Well profile; legacy author counterparts contain prior Well lighting. Author guard correctly blocks the new gameplay-preserving Default. User asked separately to authorize aligning author Default execution with existing gameplay Well lighting, preserving original snapshots/custom profiles. Awaiting answer; no such migration implemented. Removed temporary diagnostics. Restored published catalogue and original author source to pre-shotgun state; provisional generated versions preserved in raw evidence. Audio/color and approved radius migration remain implemented, tighter published Default remains blocked. Do not integrate this partial snapshot.

User explicitly approved preserving current gameplay Well lighting in author Default execution. Reuse existing version/value-gated Well migration for author Default only; source snapshots and named profiles untouched. Rebased main a565af73; retained rifle steel migration when resolving textual conflict. Affected checks: Stage, ShotgunWeightTests (all effective values), UniformPickupCircleProfileTests including Well preservation, LabAuthoringTests and LabReleaseCatalogTests; graphical muted ShotgunWeightPresentationTests. No full/build/deploy.

Well migration resolved. Fresh main rifle-steel migration had the equivalent author Default mismatch (3,.65,1,.08 vs 1.5,.56,.55,.5); reuse its existing version/value-gated migration for author Default to preserve main gameplay visuals. Named old rifle profiles remain exact; added regression. Removed diagnostic logging; reset only own provisional author output before staging.

Final stage PASS: Default 9 hash 925c0cd30ae1c4f3fb797bd7acb936b0248133b2e33b22d450d244cad1facdf1. Qj2ORh 59/63: two regressions repaired (scope author Well migration to daylight predecessor v4; prepare scene registry in catalog fixture exactly as runtime). Two historical-schema failures reproduced on pinned a565af73 baseline VydyxN (5/7); remain FAIL, not caused by shotgun. Moving-main baseline 9BGOyN was NotRun (incomplete source swap); corrected by pinning. Final repaired affected set NkJSE5 10/10 PASS including every effective Default value, immutable catalog entries, custom profiles, circle/Well/rifle presentation. Earlier independent audio/mute/render 18/18 PASS. Final integration: current main Forge view-scale update is a small added profile predecessor and presentation change; rerun Default parity and rendered shotgun on combined source.

## Final source result

Combined source 71c6723c on main 27cc9d14: 74AyRN effective Default comparison 1/1 PASS; final graphical muted PlayMode 1/1 PASS, loaded spread4.8 and all18 bronze traces, actual 1280x720 camera screenshot. NkJSE5 repaired affected set10/10 PASS, earlier audio/startup/render18/18 PASS. Other unaffected successes retained from Qj2ORh; two pre-existing historical-schema failures remain explicitly recorded and baseline-reproduced in VydyxN. Original four author source hashes and both packaged entries preserved.

Approved heavy-v5 WAVs match runtime hashes, duration .55s; no damage/pellet/cadence increase. User approved audio preview, not final in-game mix. No Player build, full suite, install or deployment. Durable evidence: shared planning docs/evidence/shotgun-weight-2026-10-25 (initial and blocker evidence already committed; final checks/screenshot retained there for closure). Source-only integration and owned worktree cleanup recorded in that evidence README.
