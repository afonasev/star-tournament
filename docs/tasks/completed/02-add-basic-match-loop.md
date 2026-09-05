# 02 · Add basic match loop

Статус: завершено и интегрировано в main; OpenSpec архивирован в `openspec/changes/archive/2026-09-01-add-basic-match-loop/`. Ниже сохранены результаты исходной проверки; указанные dev URL — исторические, не текущие стенды. Деплой не выполнен.

## Scope

OpenSpec change `add-basic-match-loop` adds a configurable local FFA/team match around the existing single keyboard/mouse combat slice. Stationary mannequins are participants with damage, death, corpse and respawn lifecycle; they still have no movement, attacks or AI.

Confirmed rules:

- a match never ends in a draw: a tied score/time trigger enters overtime until one contender leads;
- team limits use the team score sum and a complete tick is evaluated atomically;
- the optional score target uses default 3000, range 1000–20000 and step 100;
- one keyboard/mouse local seat remains the only playable seat.

## Implementation

- Immutable `MatchConfiguration` v1, profile v3, scenario v2, participant-centric snapshot/replay v3.
- Deterministic damage ledger, statistics, assists, cumulative kill-chain scoring, match timer, team totals, score/time triggers, overtime and immutable result.
- Independent corpse presentation and fixture respawn with a new life identity.
- Local-seat initial spawn moved from the `corner-north` boundary at `z=8` to the clear feet-anchor at `z=6`; a real Rapier overlap test now permits only floor contact before the first input tick.
- Pre-match FFA/team configuration, simulation timer, Tab standings, full pause menu, FFA/team results and exact repeat/menu session lifecycle.
- Live/results standings use one fixed column grid; leaders inherit participant color, while local-seat humans receive a restrained background accent and `ИГРОК` badge. Team winners are localized as `Красная команда` or `Синяя команда` instead of exposing the internal team id.
- Damage dealt/received stays exact in simulation projections and is rounded to the nearest integer only in standings presentation.
- Living mannequins use their configured participant/team colors instead of one renderer constant; respawn restores the same authoritative color.
- Renderer/debug/performance projections consume authoritative participant and match state.
- Debug-only `uiPreview` states exist solely for deterministic visual QA of live standings, overtime and results; they do not alter gameplay simulation.

## Verification

- `npm test`: 42 files / 250 tests passed in the final gate.
- `npm run build`: passed; existing Vite large-chunk warning remains.
- `openspec validate add-basic-match-loop --strict`: passed.
- `npm run perf:browser`: passed all eight phases. Menu/pause/results recorded zero callbacks, simulation steps, WebGL submissions and HUD publications. Running phases held 59.94–59.99 simulation steps/s and no more than 10.50 HUD publications/s. Long-task and thermal metrics are unavailable in this environment; heap was available.
- Dev stand: `http://127.0.0.1:4177/`, HTTP 200.
- In-app Browser: FFA/team menu, WebGL HUD, live standings, pause, repeat/menu teardown, debug identities, overtime and FFA/team results verified with clean console after the teardown fix. Benchmark movement/combat advanced the timer and ammo 20→17.
- Follow-up visual verification: aligned FFA/team columns, participant-color leaders, restrained human marker and `Победитель: Красная команда` were rechecked in the in-app Browser without console or WebGL errors. Fresh screenshots: `live-scoreboard-aligned.png` and `results-teams-aligned.png`.
- Spawn regression verification: initial snapshot reports position `0.00, 0.90, 6.00`; the first WebGL frame shows the arena and center mannequin without wall clipping before movement. No console/WebGL errors. Screenshot: `initial-spawn-debug.png`.
- Final visual follow-up: fractional QA damage values render as rounded integers, and the first WebGL frame shows distinct configured orange/green mannequin colors. Screenshots: `live-scoreboard-rounded-damage.png` and `initial-spawn-colored-mannequins.png`.

## Physical browser acceptance

The in-app Browser automation rejects Pointer Lock on semantic clicks, while its retryable error UI remains correct. The user completed the physical checklist on the same dev stand and confirmed start position, keyboard movement, jump, mouse look, fire, mannequin death/respawn, held Tab, Escape/pause/resume, repeat and menu flow. Gamepad, split-screen, bot AI and online play remain outside this change and are not claimed.
