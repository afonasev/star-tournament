## Context

ARENA-1/2 already own immutable arena identity and explicit family selection; ARENA-4 validates the definition before Unity objects exist. ARENA-3 constructs an owned navigation context after projection. See proposal.md and the new ARENA-5 requirements for the externally observable contract.

## Goals / Non-Goals

**Goals:** bind successful generation, validation, Unity projection and owned navigation to one immutable definition/profile freeze snapshot; expose stable mismatch diagnostics; prove the five family paths repeat in EditMode and remain coherent through one PlayMode projection/navigation boundary.

**Non-Goals:** no generator/editor redesign, no family-selection algorithm, mutable match lifecycle state, retry/fallback, Repeat UI, replay, route/fairness scoring, visual/content changes, integration, archive, deploy or native Player acceptance.

## Decisions

- Introduce a small immutable freeze value that owns canonical definition, resolved profile identity and arena identity. Consumers take this value rather than parallel definition/profile arguments. This makes the data boundary explicit; comparing loosely related strings at each call site would allow an accepted definition to drift after validation.
- Compute repeatability from the existing canonical definition identity, not Unity scene object state or native NavMesh handles. Unity-native runtime need not promise bitwise physics determinism, while the pre-session definition hand-off remains pure and testable.
- Reuse the ARENA-4 validation result before a freeze value is issued. A failed validation never becomes a frozen candidate; a mismatch in a consumer returns a stable contract diagnostic rather than silently regenerating or selecting a replacement family.
- Keep the snapshot temporary to arena construction/navigation ownership and do not serialize it into gameplay/replay state. ARENA-5 establishes a pre-session contract only; replay and Repeat lifecycle policy require separate approved changes.

## Risks / Trade-offs

- [Existing callers construct projection/navigation from independent arguments] → migrate the bounded native arena construction path to the freeze value and retain a defensive mismatch check at public seams.
- [Identity omits a profile-relevant field] → tests mutate the resolved profile identity and assert rejection; retain the existing canonical identity source instead of adding parallel hashes.
- [EditMode proof misses scene-owned behavior] → add a focused PlayMode case that builds a valid frozen family and ensures a mismatched hand-off leaves no owned projection/navigation context.

## Migration Plan

1. Add the freeze boundary and diagnostics around accepted native family generation.
2. Route validator, projection and navigation construction through it; remove only redundant parallel arguments in this bounded path.
3. Add focused EditMode/PlayMode tests and strict OpenSpec validation. No data migration, deployment or rollback action is required because no persisted format changes.
