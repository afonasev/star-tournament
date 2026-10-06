# 14 — Physical multi-level navigation

Date: 2026-09-08. Branch: `codex/multilevel-physical-navigation`. Base: `ad6cfbf`.
OpenSpec: `expand-multilevel-arena-variety`. Status: user requested archive, commit and merge on 2026-09-09; implementation integrated into main at 9701127 and archived with warnings; checks are recorded below. Deployment is outside this request. Seven broader acceptance items remain open.

## Current room-layout slice — 2026-09-08

This section supersedes the earlier geometry/evidence below. The overall OpenSpec remains active; main integration, archival and deployment remain outside this handoff.

- `arena-rooms-v1`, profile `prototype-v1` schema 9 / revision 10, hash `fnv1a64-v1:36d3f8d5accd2a24`. Previous full release snapshots are untouched; schema 8 uses the frozen `legacyProceduralArenaV2` and `legacyGameDesignProfileV8` path. Historical regression tests retain their existing expected hashes.
- Closed layouts occupy six of eight weighted seed slots. Each family/size has three graph and ramp-placement variants. Upper rooms occupy the full core footprint with their own walls and local physical ceilings; basement rooms are below the main spawn floor. Two enclosed side ramps connect the floors.
- Room, hall and tunnel ceiling heights vary locally and with seed. Descriptor metadata controls heights, variation, family weights, useful upper-floor ratio and structural corpus threshold.
- Validation checks actual ceiling footprint and height, continuous wall intervals with only declared door/balcony exceptions, 3D spawn clearance and projectile LOS. Negative fixtures reject missing/displaced ceilings and a missing enclosing wall. Real Rapier checks ceiling shots/jumps, every spawn/combat route, all doors across three lanes, and both ramps across three lanes with landing turns. Door-lane checks alone do not prove every possible strafing turn.
- Support lookups and vertically relevant obstacles are indexed without changing route selection or acceptance tolerances. KCC upward contacts supplement the narrow support probe while the independent feet-height and penetration bounds remain enforced.

Verified final implementation:

- `npm test`: **65 files / 393 tests passed**, 144.51 s. Includes 72 maps (3 sizes × seeds 0–23), both FFA/teams allocations, three graph/transition variants per family/size, legacy hashes, replay, collision, profile and renderer regressions.
- Guided AI: five families × three sizes (seeds 0, 2, 3, 4, 6); normal simulation reaches the other floor with zero recovery. Each runner first passes full physical route acceptance. This does not establish universal autonomous combat layer coverage.
- `npm run build`, typecheck, `git diff --check`, strict OpenSpec validation: pass. Existing Vite config/chunk warnings remain.
- Muted in-app Browser: all five families inspected with the shipping renderer. Large seed 4, Balanced, 1280×720 CSS / 1920×1080 buffer: **performance gate PASS**. Report: `artifacts/multilevel-navigation/rooms-performance-balanced.json`, arena hash `fnv1a64-v1:c84fe6de23cb5305`. This is one quality/viewport, not full split-screen or all-tier acceptance.
- Manual Start game still reports denied pointer lock. Physical keyboard/mouse traversal and aiming remain unverified. No input bypass was added.

Current dev origin: http://127.0.0.1:4196/. All current PNGs below use revision 10 and muted output:

| State | Size / seed | File in `artifacts/multilevel-navigation/` |
| --- | --- | --- |
| Closed lower tunnel | small / 0 | `rooms-tunnel.png` |
| Enclosed upper room | small / 0 | `rooms-upper.png` |
| Enclosed ramp | small / 0 | `rooms-ramp.png` |
| Gallery opening | small / 2 | `rooms-gallery.png` |
| Balcony opening | small / 3 | `rooms-balcony.png` |
| Full upper floor plan | large / 4 | `rooms-dual-plan.png` |
| Basement below main floor | medium / 6 | `rooms-basement.png` |

Remaining overall-change gates: autonomous combat layer coverage, material/lighting acceptance across all sectors, all-tier/split-screen performance, and manual keyboard/mouse playtest. The current spatial revision is ready for review on the dev stand.

## Earlier physical-navigation slice (`a82a048`)

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

## Physical stairs and climbing tempo (2026-09-09)

`arena-rooms-v2` / ArenaDefinition schema 6 uses explicit ramp or ordered stair transitions. The local release is `prototype-v1` schema 10 / revision 11, `fnv1a64-v1:175b900d6f85f32c`; revisions 8–10 remain unchanged. Each transition independently chooses stairs with profile probability 0.5. Stairs are real floor boxes shared by rendering, collision and navigation, without a hidden ramp. Tread/riser constraints and camera smoothing have validated Balance Lab descriptors.

Climb intent follows the contacted ramp plane. A grounded stair approach may use capsule sweeps up, across the exact requested XZ and down; all movement blockers remain active. Descending stairs retain ordinary KCC XZ and sweep down only onto the same or previous declared tread. Both fallbacks require real source contact and remain inside the stair corridor. Wall sliding, jumping and historical movement paths are preserved. Camera smoothing stays inside capsule headroom; the HUD projects the same central hit resolved by combat, without changing aim or simulation.

Verification covers measured flat versus uphill/downhill tempo at full and low analog input, diagonal motion, stop/jump, every-tick stair grounding, ceiling/wall/participant/side-entry blockers, ordered replanning from every tread, historical generator hash, 128-seed transition distribution and the existing three-size structural/physical/AI/replay corpus. The old profile still compiles small/0 as `fnv1a64-v1:6a6e4bea2c7dd9d3`.

Muted browser simulation walkthrough on small/0: ascent reaches the upper landing at tick 79 (feet 5.510 m), descent reaches the lower landing at tick 73 (feet 0.010 m). Mid-ascent and mid-descent use the shipping simulation, collision, renderer and aim query; these are deterministic diagnostic actions, not physical keyboard/mouse evidence. Inspected PNGs: `stairs-up.png`, `stairs-down.png`, `stairs-moving-up.png`, `stairs-moving-down.png`. The moving frames show the projected reticle while camera smoothing is active.

Normal Start was tried again in the in-app Browser. It reports “Не удалось получить управление мышью”; evidence is `stairs-pointer-lock.txt`. Manual keyboard/mouse acceptance remains open. Large/5 Balanced performance evidence is saved in `stairs-large-performance.json`; this is the existing cadence/lifecycle reference gate, not a claim of 60 rendered FPS or all-tier/split-screen acceptance.

Dev URL verified HTTP 200: http://127.0.0.1:4196/?muted=1. No main integration, archive or deploy.

The full-suite regression on basement seed 103 also passes after retaining KCC contact when no extra downward snap is needed. The AI visibility/extension test now allows 30 seconds for its complete physical initialization (previous generic five-second timeout); its behavioral assertions and all gameplay/performance bounds are unchanged.

Final verification: `npm test` passed all 414 tests across 70 files (177.60 seconds). After the legacy navigation guard, the compatibility/parser/stair/replay subset passed all 18 tests. `npm run build`, strict OpenSpec validation and `git diff --check` passed.

## Merge reconciliation — 2026-09-09

- Release `prototype-v1@12`, schema 10, hash `fnv1a64-v1:5edb6318d1ffaa45`: stairs/rooms plus main wall-clearance capsule and passage widths. Every previously published main snapshot is unchanged. Conflicting branch @9 is preserved in `balance/history` without renumbering; published main @9 uses a frozen compiler/validator. Eighteen map hashes match main f8d4daa. Collision-contract-v2 follows main; no v1 replay migration is claimed.
- Physical routes on large seeds 2, 5 and 6 pass after rejecting shallow floor undershoot. Current rooms use the guarded repeat query; historical geometry keeps its previous path. No physical acceptance thresholds were relaxed.
- Muted browser: small/0 stair ascent finishes at tick 75, feet 5.510; both directions photographed with the current renderer. Large/5 Balanced reference cadence/lifecycle gate passes (`merge-performance.json`). Manual pointer-lock acceptance remains open.
- Seven delta capabilities synchronized while retaining wall-clearance requirements. Strict validation: 25 items passed.

- Final full suite exercised 418 tests: 415 passed; two timeout failures and one historical floor-path regression were then resolved. Sequential matrix/traversal recheck plus the final seven historical tests passed, including all 18 published map hashes. Build and strict OpenSpec checks passed. This is combined full-suite plus targeted verification, not a single all-green full-run log.

Archive: `openspec/changes/archive/2026-09-09-expand-multilevel-arena-variety/`. This completed implementation handoff preserves seven unaccepted broader tasks in the archived checklist. Final collision/transition subset: 17 passed; zero-snap regression suite: 10 passed. No deployment.
