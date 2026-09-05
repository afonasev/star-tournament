# Bot AI v1 — implementation verification

Date: 2026-09-05. Branch: `codex/add-extensible-bot-ai`.
Dev origin: `http://127.0.0.1:4193/`, HTTP 200, worktree
`/private/tmp/star-tournament-bot-ai`. All tests muted.

Status: implemented and automated checks passed; physical acceptance pending.
No claim of main integration, archive, deployment or physical input approval.

## Automated evidence

- `npm test`: 53 files, 327 tests PASS.
- `npm run build`: typecheck and production build PASS. Existing large-chunk
  advisory remains; Rapier stays a separate lazy chunk.
- `openspec validate add-extensible-bot-ai --strict`: PASS.
- `npm run measure:collision-bundle`: backend absent from initial main chunk.
- Profile: `prototype-v1@6`, `fnv1a64-v1:b7ca910122e09e91`.
- Snapshot v4 / configuration v4 / replay v5 / AI `bot-ai-v1`.

## Final independent AI suite

`/private/tmp/star-ai-evaluation-heldout/{suite,report}.json`,
`ai-evaluation-v1`, heldout seeds starting at 20001. Suite/jobs written before
execution. Earlier failure seeds 10036/10060 moved to regression/calibration.

| Comparison | Stronger wins | 95% Wilson interval |
| --- | --- | --- |
| Боец vs Салага | 199/200 | 0.9722–0.9991 |
| Ветеран vs Боец | 193/200 | 0.9295–0.9829 |

418/418 finished, zero timeout/stall. Includes mirrored identities/spawn sides
and 18 FFA/team, 2/4/8 participant scenarios across all sizes. Median measured
speedup 77.2x; this is this machine/run, not a universal performance promise.
Maximum no-progress span 315 ticks; per-bot jumps 0–8; maximum airborne share
9.12%; longest close-allied span 303 ticks. A bot need not jump every match.

Fixed genuine defects: rear-damage scan without hidden attacker coordinates;
wall contact skin offset incorrectly blocking navigation; cross-platform
trigonometry accumulating into different AI decisions. No gate was loosened.

## Browser and replay

In-app Browser, muted, balanced, 1280×720 CSS / 1920×1080 backing buffer.
Menu add/remove, independent difficulties, eight-participant limit, teams,
real AI combat/death/respawn and result suffixes observed.

Eight-participant reference report:
`/private/tmp/star-ai-browser-precision-perf.json`: all gates PASS, ~60 ticks/s,
simulation p95 up to 1.6 ms, callback p95 up to 16.6 ms, peak 3560 draw calls.
Menu/paused/results settled phases zero simulation/render/HUD work. Hardware
thermal/long-task measurements unavailable, not silently treated as zero.

Final small/team seed 101, full 3600-tick browser replay:
`/private/tmp/star-ai-browser-final-team-replay.json`.
Browser and Node exact hash: `fnv1a64-v1:01907807f91bf4af`.
Reproduce with `npm run ai:simulate -- --replay <path>`.
The browser Repeat of the identical configuration also produced exactly
`fnv1a64-v1:01907807f91bf4af` after 3600 ticks.

Normal start was attempted through semantic and coordinate clicks. Both showed
the retryable pointer-lock error. The performance harness intentionally uses
synthetic human input and is not a substitute for physical move/look/jump/fire
or normal pointer-lock pause/resume. These checks remain explicitly open.
Native fallback through the application was also unavailable: computer-use
access to the Codex app was refused by the tool's safety boundary. No bypass
or alternative input injection mechanism was attempted.

Screenshots saved alongside this report: `setup.png`, `team-setup.png`,
`team-live.png`, `ffa-results.png`, `team-results.png`. All use seed 101 and
profile revision 6; setup/live use medium, final match results use small.
Missing physical combat/killcam screenshot acceptance stays open with task 5.3.

The exploratory 65-second soak of a 60-second match correctly stopped gameplay
at results but failed running cadence over the extra idle seconds. The separate
reference gate and correctly sized 60-second full-match soak passed.
