## 1. Canonical decisions and profile

- [x] 1.1 Update `docs/GAME_SPEC.md` and create the active handoff task with the approved single-level, all-mode, size/seed, asymmetric fairness, spawn-group and explicit fallback decisions; verify narrative numbers remain in named profile/configuration.
- [x] 1.2 Extend `prototype-v1` with complete connected-wall, corridor/doorway, perimeter and spawn-separation rules and descriptor metadata for `small`, `medium`, `large`; verify schema, range/step, cross-field and descriptor coverage tests.

## 2. Semantic arena and generation

- [x] 2.1 Upgrade `ArenaDefinition` to the canonical semantic v3 model with normalized regions, links, surfaces, spawn regions/slots and generator profile identity; verify round-trip, hash, immutability, reference and ordering tests.
- [x] 2.2 Replace the disconnected `broken-ring-v1` layout with deterministic `broken-ring-v2` connected wall chains, rooms, corridors, doorway gaps and a closed irregular perimeter for all three sizes; verify golden identities and a repeated multi-seed corpus.
- [x] 2.3 Extend gameplay validation for architectural connectivity, non-rectangular perimeter, corridor/door capsule clearance and size-specific route structure; verify positive and negative fixtures.

## 3. Simulation, collision and replay integration

- [x] 3.1 Update deterministic arena spawn allocation so initial FFA and opposing teams satisfy preset minimum separation while teammates may use adjacent slots; verify eight-participant small placements, occupied-slot fallback and pre-input capsule clearance.
- [x] 3.2 Embed exact `ArenaDefinition` in playable replay and enforce snapshot/definition identity compatibility; verify serialize/parse, tamper rejection and reconstructed collision replay hashes.
- [x] 3.3 Replace browser fixture hardcoding with one validated generated definition shared by snapshot, collision, renderer and diagnostics; verify runtime integration and lifecycle tests.

## 4. Match setup and playable flow

- [x] 4.1 Add `small`/`medium`/`large` and concrete seed to versioned match configuration; verify canonical hash, validation and default-medium tests.
- [x] 4.2 Add setup controls for size and optional manual seed, resolve auto seed before session creation, preserve exact arena on Repeat and create a new auto seed after returning to setup; verify React flow tests.
- [x] 4.3 Add actionable generation error with retry/new-seed/explicit-fallback actions and actual fallback identity; verify no partial runtime starts on failure.

## 5. Verification and handoff

- [x] 5.0 Fix participant collision projection so every living participant blocks movement, dead participants do not collide, and respawn reactivates the capsule; cover the behavior with collision and simulation integration tests.

- [x] 5.1 Run strict OpenSpec validation, typecheck, full automated tests and production build; record exact results in the handoff task.
- [x] 5.2 Run the collision and production-browser performance gates for representative small/medium/large seeds with game audio disabled; record portable gates and device-sensitive evidence.
- [x] 5.3 Run in-app Browser physical playtests for representative small/medium/large arenas, manual seed, FFA/team setup, Repeat and fallback/error surfaces with game audio disabled; replace screenshots with current connected-architecture evidence for every changed state.
- [x] 5.4 Complete and move the handoff task, mark all OpenSpec tasks done only after evidence passes, review the final diff and amend the dedicated unintegrated feature commit.
