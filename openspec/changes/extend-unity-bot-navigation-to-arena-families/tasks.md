## 1. Definition-owned navigation contract

- [x] 1.1 Extend the native provider/context so supports, transitions and route endpoints are read from the active `ArenaDefinition`; verify foreign context and semantic transition negative tests reject routes.
- [x] 1.2 Validate every complete native path against ordered physical transition feet, support sequence and headroom without introducing family branches; verify EditMode identity/profile/state tests pass.

## 2. Physical bot traversal matrix

- [x] 2.1 Replace fixture-only navigation fixtures with definition-derived family endpoints and lanes; verify PlayMode executes 5 families × 2 transitions × 2 directions × 3 lanes without jump, teleport or free-route recovery.
- [x] 2.2 Add overlapping-XZ, broken/headroom, foreign arena, live blocker, replan/restore and lifecycle regressions; verify failures remain bounded and physical.

## 3. Native evidence and handoff

- [x] 3.1 Run full Unity EditMode/PlayMode, strict OpenSpec validation and Mac Development build; save XML/log/build identity and diagnostic workload JSON.
- [x] 3.2 Run muted native Player FHD/4K journeys on representative family layouts, inspect PNG/JSON, update GAME_SPEC/matrix/handoff and leave physical/art/performance acceptance open; verify `git diff --check` and commit the coherent change.
