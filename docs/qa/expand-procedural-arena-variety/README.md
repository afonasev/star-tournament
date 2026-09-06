# Procedural arena variety — acceptance evidence

**Superseded for acceptance:** the user subsequently reported missing texture coverage and pass-through geometry. See [the repair report](repair/README.md) for current evidence. The results below describe the earlier implementation only.

Date: 2026-09-06. Branch: `codex/expand-procedural-arena-variety`, based on `7589f94`.

Implementation is committed for review. Physical keyboard/mouse acceptance (OpenSpec 5.3) remains open; do not integrate or archive this change on this evidence alone.

## Contracts and automated checks

- Four recipes: grand-hall, split-atrium, service-loop, flanked-courtyard; all three sizes, mirrored seed variants.
- Arena schema 4 / generator `arena-recipes-v3` / game-design profile revision 7. Canonical schema 3 definitions and profile revision 6 remain readable without rewriting historical identity.
- Game profile hash: `fnv1a64-v1:7e228fc2c51d50b4`; presentation revision 2 hash: `fnv1a64-v1:30cb0b96ac4a99ff`.
- Full suite: **57 files / 338 tests PASS**. Includes 96 seed/size definitions; real Rapier capsule paths from every spawn to center and back for 24 recipe/size/mirror combinations; bidirectional ramps without jumping; projectile/capsule masks; historical definitions; deterministic checkpoint reconstruction; AI combat and replay (seed 101 selects split-atrium); renderer identity/resource and asset/LOD audits.
- After the final pane-frame change: production build PASS, renderer suite **16/16 PASS**, strict OpenSpec validation PASS. After visual-fixture camera adjustment: TypeScript check PASS. The fixture only chooses review cameras and does not modify gameplay.
- All testing muted. No physical gamepad or network acceptance claimed.

## Browser performance

Existing `scripts/run-browser-performance-gate.mjs` against the production build, Chromium 152, macOS, DPR 2. All eight phases passed for each report; menu, paused and results had zero simulation/WebGL/HUD work. Active phases maintained approximately 60 simulation steps/s. These are automated cadence/budget checks, not a new thermal or 10-minute soak claim.

| Report | Recipe / size / seed | CSS viewport | Result |
| --- | --- | --- | --- |
| [Low](perf-low.json) | split-atrium / medium / 1 | 1280×720 | PASS |
| [Balanced](perf-balanced.json) | split-atrium / medium / 1 | 1280×720 | PASS |
| [High](perf-high.json) | split-atrium / medium / 1 | 1280×720 | PASS |
| [Ultra](perf-ultra.json) | split-atrium / medium / 1 | 1280×720 | PASS |
| [Ultra 4K](perf-ultra-4k-large.json) | flanked-courtyard / large / 3 | 3840×2160 | PASS |

Every quality tier reported arena hash `fnv1a64-v1:d3c91098e9320f77`; large courtyard reported `fnv1a64-v1:1c21d4e5b074f2c8`. Rendering does not mutate canonical simulation inputs (renderer unit coverage). These reports do not compare end-of-match simulation hashes across quality tiers.

Chromium profile cleanup briefly raced process shutdown on the first High run. Retried temporary-profile removal in the runner; reran High. No product behavior was changed for this repair.

## In-app Browser evidence and open gate

Dev origin verified HTTP 200: `http://127.0.0.1:5180/?muted=1`, served from this worktree. Visual review uses `arena-review.html?seed=1&size=medium&muted=1` and seed 3 / large. The fixture renders canonical definitions through the shipping renderer and labels itself **VISUAL FIXTURE**. Reviewed overview, window, both barrier heights, niche, ramp and second-recipe overview; no fixture console errors. It never runs physical input.

PNG files are in `/Users/eaafonasev/.codex/visualizations/2026/09/05/01a071b7-e49d-7442-8741-35812a2e83a8/`:

- `atrium-overview.png`: medium, seed 1, overview and lamp accents.
- `atrium-window.png`: medium, seed 1, framed window.
- `atrium-low-barrier.png`, `atrium-high-barrier.png`: medium, seed 1, movement-only barriers.
- `atrium-niche.png`: medium, seed 1, shallow wall relief.
- `atrium-ramp.png`: medium, seed 1, raised belt approach.
- `courtyard-overview.png`: large, seed 3, second composition.
- `pointer-lock-blocker.png`: normal match, medium, seed 1; failed input capture, timer 05:00.

A stale Vite optimized-module URL initially prevented the Rapier dynamic import after a diagnostic module read. Restarting the dev server with `--force` resolved the import; the actual match then initialized successfully. Temporary diagnostic logging was removed.

Ordinary AX button click and direct coordinate click both produced “Не удалось получить управление мышью”; pressing W did not advance the timer or establish gameplay. Therefore walking ramps, colliding with each barrier height, shooting through barriers/relief and into windows still require a working physical-input browser session. Automated Rapier tests and benchmark action frames do not close this gate.
