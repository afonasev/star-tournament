# Полигон — affected QA

Baseline: `main@887439e03dd2a6c0a576b36fe04ff8ba0a1ec439`; owned branch `codex/add-firing-range`.
Scope: authored single-floor hall, 1–4 local humans plus passive training targets, full loadout, same-position ordinary-delay respawn. Dependencies: roster/composition, catalog/validator, session lifecycle, audio capacity, map picker and Lab authoring registry.

Affected checks planned: `unity/tools.sh test-edit-filter 'FiringRangeTests;CombatLifeTests'`; `unity/tools.sh test-play-filter 'FiringRangePlayTests;NativeParticipantRosterTests;DefaultRosterTests'`; historical Lab methods for the additive profile. Shared/silent Editor, no Player build or full suite. Real Editor frames and exact before/after render comparison verify the map/optimization; physical devices, Player, FPS and human acceptance remain separate.

Durable evidence: primary planning `docs/evidence/add-firing-range/`. Initial import run: FiringRangeTests 5/5 PASS; other two filter names matched no fixtures, so no evidence for those nonexistent names. Subsequent affected runs use actual fixture names.

## Results before integration

- EditMode: 16/16 PASS (`FiringRangeTests;CombatLifeTests`).
- PlayMode candidate: 4/4 PASS (three range scenarios plus existing nonlocal participant input/killcam).
- Asset optimization: 71 → 17 active decoration renderers, exact before/after pixels equal; collision unchanged; no new hall texture assets/lights; low-poly shell/detail LOD not applicable. Existing trooper/weapon assets retain their shared LOD paths. Menu image is 960×540, non-readable, compressed, no mipmaps, max 1024.
- First PlayMode attempt: 5 PASS / 11 FAIL retained. Range menu return bug fixed; semi-auto fixture now supplies repeated fire edges without changing weapon contracts. Nine other failures concern existing lobby expectations. A clean-baseline run of the two representative methods reproduced the same 1-vs-0 bot mismatch on `887439e0`; no claim that all old lobby tests are green.
- Actual Editor frames caught mirrored wall signs after increasing their size; their facing is corrected for final combined validation.
- Full tests, Player/build/release/deploy and physical/human acceptance remain NotRun/pending.

## Combined source verification

Against main `2f9d9b5c`: 6/6 range EditMode checks and 5/5 PlayMode checks passed (range combat/restore/respawn, local 1+4/pause/repeat, identical optimization render, mapped participant input, new hit-pose-cache byte-preservation contract). Range wall text is readable in the final actual Editor frames. A final narrow menu scenario additionally exercises the ordinary map card.
