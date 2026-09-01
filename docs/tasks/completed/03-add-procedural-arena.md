# 03 · Add procedural arena

## Scope

OpenSpec change `add-procedural-arena` replaces the production collision fixture with a deterministic single-level playable arena generated from exact size, seed, generator version and `prototype-v1` identity.

Physical playtest also requires solid participant collision: all living roster capsules block movement, dead participants remain non-colliding until respawn, and stationary fixtures are not pushed by contact.

Confirmed rules:

- sizes `small`, `medium` and `large` are distinct topology recipes; `medium` is default;
- connected wall chains form rooms and paired corridor boundaries, doorway gaps are capsule-passable and the closed perimeter is an irregular size-specific silhouette rather than a four-wall box;
- every accepted arena supports FFA, teams and up to eight participants;
- geometry may be asymmetric when versioned navigation/LOS fairness gates pass;
- every visually open gap is either capsule-passable or constructively sealed; sub-capsule crevices are invalid;
- initial FFA and opposing teams satisfy preset opponent separation; teammates may use distinct nearby slots and spawn in mutual LOS;
- setup supports optional manual seed, Repeat preserves exact arena and a new auto-seed match resolves a new seed;
- generation failure is actionable and shipped fallback requires explicit confirmation;
- the slice is single-level and keeps analytical `floor`/`wall`/`accent` presentation; vertical routes and final art kit are outside scope.

## Implementation

Status: completed after iterative visual and physical playtests exposed and verified fixes for disconnected interior blocks, rectangular perimeter geometry, absent corridor/door architecture, unsafe small-map initial spacing, z-fighting, sub-capsule false gaps and missing participant collision.

- `prototype-v1` schema v5/revision 5 owns wall thickness, corridor/doorway clearance, perimeter inset, cover attachment and opponent spawn separation alongside the existing numeric generator rules and descriptor metadata for every size preset.
- `ArenaDefinition` schema v3 is the single canonical spatial source for semantic regions/links, analytical surfaces and spawn regions/slots; adapters derive collision, navigation, spawn and render projections from it.
- `broken-ring-v2` constructively generates a twelve-segment closed stepped perimeter, a connected main corridor, flared rooms with inner/outer doorway gaps and architecture-attached buttresses. Small/medium/large add two/three/four room recipes rather than scaling one box layout.
- `arena-gameplay-v2` rejects isolated architectural surfaces, open or rectangular perimeters, undersized corridors, undersized doorways and uncovered sub-capsule gaps in addition to capsule, graph, route and fairness validation. `broken-ring-v2` seals those false passages with canonical wall solids.
- FFA initial allocation is deterministic farthest-first; opposing players must satisfy the preset separation while teammates keep stable adjacent distinct slots. Initial state and production fixture respawn use the same allocator and arena profile rule.
- Match configuration schema v2 stores concrete size/seed; setup accepts manual uint32 or resolves an auto seed. `StarTournamentApp` owns the exact arena across Repeat. Failure stays in setup and exposes retry/new-seed/explicit shipped fallback actions.
- Playable replay schema v4 embeds the exact `ArenaDefinition` once and rejects definition/snapshot identity mismatch.
- Production browser bootstrap no longer uses the collision fixture as its arena; snapshot, Rapier world, renderer, diagnostics and performance report receive one exact definition.
- Browser bootstrap and every movement tick project all living roster capsules. Rapier KCC queries include static geometry and every other active participant; dead participants are disabled until respawn, while combat raycasts continue to use analytical target hit volumes.

## Verification

- `openspec validate add-procedural-arena --strict`: pass.
- `npm run typecheck`: pass.
- `npm test`: 43 files / 269 tests passed in the final full-suite run.
- `npm run build`: pass; existing Vite chunk-size warning remains informational.
- Geometry regression: all representative seeds and all three sizes contain no visually competing coplanar material faces and no uncovered gap narrower than the profile capsule diameter; removing generated seals is rejected with `SUB_CAPSULE_GAP`.
- Participant collision regression: 5 targeted files / 37 tests passed, including direct Rapier contact, simulation integration, death disable, respawn reactivation, full-roster browser bootstrap and shotgun hit-volume isolation. Rapier remains a lazy chunk and is absent from the initial main bundle.
- Production-browser gate at 1280×720 CSS / DPR 2 passed every phase (`menu`, `paused`, `running-idle`, `live-scoreboard`, `movement-jump`, `combat-burst`, `overtime`, `results`) for seed `1398030674`:
  - small `fnv1a64-v1:b276867e7e0c1576`;
  - medium `fnv1a64-v1:bba91b72cf162aaa`;
  - large `fnv1a64-v1:029c7b853dd2b88e`.

## Physical browser acceptance

- Confirmed current branch dev stand by HTTP 200 at `http://127.0.0.1:4176/`; all checks ran with game audio disabled/no active audio output. Port 4175 was occupied, so the verified current process selected 4176.
- In-app Browser at 1280×720: setup default medium, manual seed, FFA small, teams medium, FFA large, active HUD/playfield, connected corridor/door geometry, Repeat exact hash and actionable generation-error preview checked. Repeat preserved the representative medium arena identity before and after reset; its current post-fix hash is `fnv1a64-v1:bba91b72cf162aaa`.
- Current post-gap-sealing screenshots supersede the earlier arena geometry evidence. Small, medium and large show continuous wall/cover junctions; no narrow visual opening remains unless it has full capsule clearance.
- The Browser automation host cannot grant pointer lock, so automated movement/jump and combat-burst phases plus browser/runtime pointer-lock tests remain the machine evidence for that boundary.
- After the z-fighting fix, the in-app dynamic benchmark passed again on the medium arena at seed `1398030674`; multiple camera frames and the paused state show stable wall faces without the reported checkerboard flicker.
- After the participant-collision fix, the in-app benchmark started a real medium match with the full four-participant roster at auto seed `2947135941` and passed all 8/8 phases. The captured current paused frame reports `PERFORMANCE · PASS`.
- On 2026-09-02 the user physically checked the final running dev stand, including the participant-collision fix, and confirmed the playable result with «все ок».
- Evidence:
  - `docs/evidence/add-procedural-arena/setup.jpg`
  - `docs/evidence/add-procedural-arena/arena-small.jpg`
  - `docs/evidence/add-procedural-arena/arena-medium-teams.jpg`
  - `docs/evidence/add-procedural-arena/arena-large.jpg`
  - `docs/evidence/add-procedural-arena/generation-error.jpg`
  - `docs/evidence/add-procedural-arena/z-fighting-fix-frame-a.png`
  - `docs/evidence/add-procedural-arena/z-fighting-fix-frame-c.png`
  - `docs/evidence/add-procedural-arena/z-fighting-fix-paused.png`
  - `docs/evidence/add-procedural-arena/sub-capsule-gaps-sealed-small.png`
  - `docs/evidence/add-procedural-arena/sub-capsule-gaps-sealed.png`
  - `docs/evidence/add-procedural-arena/sub-capsule-gaps-sealed-large.png`
  - `docs/evidence/add-procedural-arena/sub-capsule-gaps-sealed-paused.png`
  - `docs/evidence/add-procedural-arena/participant-collision-medium-paused.png`
