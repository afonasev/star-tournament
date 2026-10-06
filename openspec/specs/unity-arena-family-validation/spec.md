# unity-arena-family-validation Specification

## Purpose
Определяет ARENA-4 pre-runtime validation для immutable native Unity `ArenaDefinition` пяти registered arena families.

## Requirements

### Requirement: Pre-runtime definition gate
Каждая generated native family definition SHALL пройти pure ARENA-4 validation до Unity projection, NavMesh bake и создания gameplay session. Validator MUST return stable diagnostics containing a machine-readable code, family ID and affected semantic element when known; invalid definition MUST be rejected without retry, fallback or mutation.

#### Scenario: Accepted registered family
- **WHEN** any of the five registered profiles builds its canonical definition
- **THEN** validation accepts it before `ProvingArena.Build` and preserves its immutable identity

#### Scenario: Invalid semantic data
- **WHEN** definition contains non-finite/degenerate geometry, duplicate semantic IDs or an unknown family
- **THEN** validation rejects it with a stable actionable diagnostic before Unity objects exist

### Requirement: Geometry and projection contract
Validator SHALL require finite positive solid bounds, a valid support name for every walkable floor/transition solid, and only the canonical world or movement-only collision layers. A floor, transition, spawn region and route anchor MUST resolve to an owned declared support; renderer-only data MUST NOT become a validator spatial source.

#### Scenario: Invalid collider semantic
- **WHEN** a floor has no support or a solid uses an unsupported collision layer
- **THEN** validation rejects the definition and names that solid

### Requirement: Spawn and transition safety
Validator SHALL require finite spawn and route points on declared supports, finite positive spawn regions with unique IDs and valid supports, plus at least two unique bidirectional transitions. Every transition MUST connect two distinct declared supports and have ordered finite feet beginning/ending on its declared supports, without discontinuity larger than the profile's conservative capsule traversal bound.

#### Scenario: Broken route
- **WHEN** a transition endpoint, ordered foot or support is missing or inconsistent
- **THEN** validation rejects the definition before NavMesh/runtime navigation can report a generic failure

### Requirement: Five distinguishable canonical layouts
Each registered family SHALL produce an immutable `ArenaDefinition` that passes `unity-arena-family-validation` before its Unity projection. Rejection MUST report its pre-runtime diagnostic and MUST NOT silently substitute another family, seed, topology or fallback.

#### Scenario: Invalid family definition
- **WHEN** a registered family builder emits a definition failing ARENA-4 validation
- **THEN** generation fails before collision, navigation, spawn or presentation projection
