# 14 — Physical multi-level navigation

Date: 2026-09-08. Branch: `codex/multilevel-physical-navigation`. Base: `ad6cfbf`.
OpenSpec: `expand-multilevel-arena-variety`. Status: physical/navigation slice verified; overall change remains active pending acceptance below. Main integration, archival and deployment are not authorized in this task.

## Result

- `arena-layers-v2` compiles five recipe families with explicit supports, continuous ramps, thin slabs and headroom. Basement uses one main slab above a lower bypass and two external descents.
- Ordinary navigation stays on one support. Only declared ramp links cross layers, using explicit landing/ramp/landing waypoints. Overlapping XZ does not imply arrival or connectivity. Lower routes avoid ramp footprints; targets inside ramps remain reachable.
- Bot planner uses capsule feet for navigation, body coordinates for aiming, and retains the transition through the landing. Telemetry records supports, transitions and recoveries.
- Real Rapier acceptance checks support contact, feet elevation, grounded movement, headroom, every spawn/combat route, and both directions of each transition across three lateral lanes plus corner sweeps. Existing fairness budgets remain unchanged.
- Narrow deterministic retry handles Rapier snap overshoot through an existing flat support only for the new generator identities. It repeats collision computation from the original pose with snap disabled for that computation, restores snap, and rejects remaining penetration; no Y clamping or validation bypass.
- Profile schema 8 / revision 9, hash `fnv1a64-v1:70d8cf43303bb009`. Slab/headroom values have descriptors and cross-field validation. Revision 8 remains immutable, with legacy generator and validator paths preserving historical representative hashes.

## Verification

- `npm test`: 64 files, 386 tests passed (45.74 seconds).
- Deterministic recipe corpus: 3 sizes × seeds 0–31 = 96 arenas, all five families; FFA and teams allocate eight participants per arena with existing separation constraints.
- Real physical corpus: 3 sizes × seeds 0–9 = 30 arenas (two seeds per family/size), every declared ramp in both directions and three effective-capsule lanes, including corner entry/exit sweeps.
- Guided normal-planner goals: all 15 family/size combinations reach the declared layer goal with zero recovery. Existing AI, collision and replay regressions pass. This proves traversal for selected goals, not universal autonomous combat exploration.
- Slabs block inter-floor shots; an open balcony/ramp sightline permits shots. Missing links, overlapping XZ, incorrect feet Y and low ceilings have negative fixtures.
- `npm run build` (includes `tsc -b`): passed. Existing Vite chunk-size/config-loader warnings remain.
- `git diff --check` and `openspec validate expand-multilevel-arena-variety --strict`: passed.

## Browser evidence and remaining acceptance

Dev server from this worktree: http://127.0.0.1:4196/ (HTTP 200 verified). Muted visual fixture: `/arena-review.html?size=small&seed=1&view=ramp&muted=1`.

The real game opened, but its Start action reported denied pointer lock. Keyboard/mouse gameplay, manual ramp traversal/strafe and aiming are therefore **not verified**. Do not treat fixture screenshots or automated physical movement as proof of physical input feel.

Saved PNGs were inspected locally; all use revision 9 and the shipping renderer. They expose a presentation issue: slab undersides are excessively dark and visually flat. Semantic material/lighting acceptance and the refreshed dense-large split-screen performance gate remain open. Earlier performance evidence predates this geometry and is not reused.

| Family | Size / seed | States / PNG files in `artifacts/multilevel-navigation/` |
| --- | --- | --- |
| wide-bypass-hall | medium / 0 | `wide-hall.png` |
| bypass-gallery | small / 1 | `gallery-upper.png`, `gallery-lower.png`, `gallery-ramp.png` |
| fire-balconies | small / 2 | `balcony-upper.png`, `balcony-lower.png` |
| dual-combat-decks | large / 3 | `dual-upper.png`, `dual-lower.png` |
| basement-service-loop | medium / 4 | `basement-main.png`, `basement-lower.png` |

Next acceptance: autonomous combat layer coverage, material/underside readability, refreshed performance gate, and muted manual keyboard/mouse browser playtest. Keep the OpenSpec change open and do not merge/archive/deploy before those gates and separate authorization.
