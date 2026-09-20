## Context

ARENA-1 owns immutable arena identity, ARENA-2 selects one of five families, and ARENA-3 proves a projected physical route. Those layers intentionally do not diagnose a malformed definition until scene projection/NavMesh work. ARENA-4 adds a pure structural gate while preserving the existing definition as the only spatial source.

## Goals / Non-Goals

**Goals:** deterministic pre-runtime diagnostics for geometry/collision semantics, supports, spawns and declared transitions; validation invoked by generation; EditMode mutation fixtures and one projection-level PlayMode guard.

**Non-Goals:** generator/editor pipeline, route/fairness scoring, NavMesh/bot policy, retry/fallback/Repeat/replay, assets, player-visible topology, integration/archive/deploy, Player physical or hardware acceptance.

## Decisions

- Introduce an `ArenaValidationResult` value rather than exception-string matching. It has stable code, family ID, element ID and explanatory message; generator converts a failed result into one deterministic `ArgumentException`.
- Validate only canonical definition data. Geometry rules use `ArenaSolid`, named supports are derived from walkable solids and transition supports, and projection does not read parallel scene data.
- Treat profile capsule geometry as the traversal bound: ordered feet must be monotonic in vertical direction and each consecutive segment is limited to the profile's ramp length plus height delta. This catches broken lists without reimplementing runtime NavMesh routing.
- Preserve `ArenaGenerator.Validate` as a compatibility entrypoint, but make it call ARENA-4 and reject the result. `ProvingArena.Build` retains its defensive validation.

## Validation flow

`ArenaFamilyCatalog.Build` -> immutable definition -> identity/fingerprint assignment -> `ArenaDefinitionValidator.Validate(definition, profile)` -> accept or diagnostic -> `ProvingArena.Build` -> colliders/NavMesh.

## Risks / Alternatives

Runtime-only validation would leave authoring failures dependent on Unity scene state and generic NavMesh errors. A full geometry/fairness solver would exceed ARENA-4; this change validates declared contracts and leaves fairness/retry policy for later approved work.

## Verification

EditMode covers all five valid families and targeted mutations for solids, layers, supports, spawns, regions and transitions, asserting codes and element IDs. PlayMode confirms accepted family projection remains buildable and a rejected definition produces no projection. No native Player QA is required because no player-visible behavior changes.
