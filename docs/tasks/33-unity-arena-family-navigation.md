# ARENA-3 — Unity navigation across arena families

Дата: 2026-09-20. Change: `extend-unity-bot-navigation-to-arena-families`.

## Delivered

- `NativeNavigationProvider` validates the active owned `ArenaDefinition` route against declared ordered transition feet, named supports and capsule headroom.
- PlayMode matrix executes all five ARENA-2 families, both declared transitions, both directions and three lanes with ordinary `CharacterMotor` actions; it rejects jump, teleport and recovery on free traversals.
- Development Player review derives its route endpoints from the generated definition. Its macOS QA build pins family and resolution at build time, because macOS replaces the launcher process and drops its arguments/environment. The review class is development-only, preserved for its reflection bootstrap, and its handoff parser is EditMode-covered.

## Verification

- Unity EditMode: 94/94 passed (`.local/unity-evidence/editmode.xml`).
- Unity PlayMode: 67/67 passed (`.local/unity-evidence/playmode.xml`).
- `openspec validate extend-unity-bot-navigation-to-arena-families --strict`: passed before native evidence capture.
- macOS Development Player build: passed. Native review was foreground and muted in both runs.

| Native journey | Generated family | Evidence | Result |
| --- | --- | --- | --- |
| FHD 1920×1080 | `wide-hall-circuit-v1` | `docs/evidence/unity-arena-family-navigation-2026-09-20/fhd-wide/` | Complete; stairs/ramp both directions, pause/resume/repeat/menu; 42.09 s diagnostic capture. |
| 4K 3840×2160 | `lower-basement-v1` | `docs/evidence/unity-arena-family-navigation-2026-09-20/4k-basement/` | Complete; stairs/ramp both directions, pause/resume/repeat/menu; 38.55 s diagnostic capture. |

The JSON records focused/muted state, real family identity and resolution. `navigation-performance.json` is explicitly a diagnostic, not a 60 FPS or reference-hardware acceptance result. The 4K PNG was visually inspected at `ramp-east-upper.png`.

## Still open

- Physical device/TV play, artistic acceptance and reference-hardware performance acceptance.
- ARENA-4 fairness/full route validators and ARENA-5 retry/fallback/replay lifecycle.
- No integration, archive, deploy or new arena authoring pipeline is part of this change.
