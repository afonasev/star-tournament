## Context

См. [proposal.md](proposal.md). Current `broken-ring-v2` encodes one planar floor and one wall/floor surface treatment. `ArenaDefinition` is the shared deterministic source for collision, navigation, spawn and renderer, but current static collision and hitscan occlusion both consume the same box list. Renderer derives a ceiling, details and lights separately.

## Goals / Non-Goals

**Goals:**

- Сохранить одну canonical, serializable spatial truth while expanding it from a flat ring to seed-selected layout recipes.
- Развести movement, projectile occlusion и presentation semantics explicitly and deterministically.
- Сделать высоты, ramps, barriers и recipe budgets profile-owned fields with descriptor coverage.
- Preserve fair, legible local FPS combat and the existing GLB/texture-quality lifecycle.

**Non-Goals:**

- Не добавлять destructible geometry, vertical jump puzzles, teleporters, pickups, weapon changes, online authority, split-screen UX changes или пользовательский map editor.
- Не превращать windows/niches в доступные помещения и не использовать rendering objects as physics truth.
- Не менять historical definitions or replays; they retain their generator/profile identities.

## Decisions

### 1. Recipe graph before geometry compilation

Generator first resolves one named recipe from `(size, seed, generatorVersion, profile)` and creates a semantic plan of rooms, corridors, elevation zones, ramps, portals, windows, barriers and relief-niche attachments. A compiler turns this plan into a canonical, semantic-ID-sorted `ArenaDefinition`; every adapter derives only from it.

This supports meaningful composition differences while retaining deterministic generation and constructive validation. Randomly scattering boxes after the existing ring was rejected: it cannot guarantee room/corridor composition, route redundancy or readable landmarks.

### 2. Separate query masks in canonical surfaces

Canonical surfaces gain explicit gameplay classification/masks: primary walls and windows block movement and projectile rays; movement-only barriers block only the capsule; floor/ramp surfaces support navigation; wall-relief niches remain presentation attachments. Collision-world construction creates deterministic query groups from the same semantic IDs, and hitscan uses only projectile-blocking groups.

This is preferred to identifying blockers by height or material: a low barrier is gameplay-semantic rather than a renderer convention, and profile/validation can audit it. It avoids the current accidental coupling where a new barrier would also occlude shots.

### 3. Small verticality through ramp-connected elevation zones

Each recipe has a bounded number of small raised/lowered floor zones. All cross-level gameplay links compile to wide, profile-defined ramps that pass the same capsule controller queries used in gameplay; no stairs, cliffs, jump-only access or one-way drops are emitted. Navigation becomes 3D enough to use compiled route costs and waypoints, rather than assuming `y = 0`.

Full multi-storey arenas were rejected for this slice: they would substantially change sightline, camera, AI and split-screen performance requirements before the ramp contract has physical evidence.

### 4. Windows and niches preserve combat readability

Windows compile as canonical wall segments with a glass presentation attachment: they visually reveal the adjacent space but block movement and shots. Wall-relief niches are shallow presentation details bound to a continuous canonical wall; they are never emitted as region links, floor surfaces or capsule destinations.

Open, enterable niche rooms were rejected because they enable full hiding and reduce the fast arena-combat tempo the user explicitly wants to preserve.

### 5. Presentation receives semantic slots, not free placement

The renderer maps recipe, sector and surface semantic slots to existing manifest-addressed modules plus new local assets where required. It uses wall/floor material variants, lamp fixtures and restrained local lights to distinguish sectors and elevation transitions. Quality tiers can reduce decorative instances, light count, LOD and texture resolution only; they cannot alter canonical geometry or player visibility rules.

This keeps lights, glass transmission and niche dressings presentation-only while every gameplay-relevant wall, ramp and barrier stays in the deterministic definition.

### 6. Validation expands before recipe promotion

The acceptance report gains versioned checks for recipe diversity, ramp traversability, reachable elevation regions, movement-only ray behavior, window collision/occlusion, niche non-reachability, spawn safety at level transitions, 3D route distance and initial LOS. Property tests sample each recipe/size/seed; collision and bot tests exercise all query masks and ramp routes. Promotion additionally requires performance evidence and a muted in-app Browser playtest with keyboard/mouse physical movement, shooting and screenshots.

## Risks / Trade-offs

- [3D nav and controller edge cases can strand bots or capsules on ramps] → construct ramps from profile bounds, reuse physical collision queries for validation, add seed regressions and exercise bot routes in headless evaluation.
- [Movement-only barriers can feel visually inconsistent] → use an explicit visual language and browser-test both walking into and shooting through each height variant.
- [More local lights/materials can exceed split-screen GPU budget] → instance fixtures, cap lights per sector, make quality degradation presentation-only, and require existing browser performance gate at all relevant viewport counts.
- [Recipe variety can make one mode unfair] → evaluate every accepted recipe for FFA and teams, including 3D route-cost and LOS metrics, before it reaches the match.
- [New schema fields invalidate older definitions] → bump generator/arena schema identity only where necessary and retain the complete old definition for replay reconstruction.

## Migration Plan

1. Add the new semantic schema and profile revision behind a new generator version; preserve readers/fixtures for the current version.
2. Implement recipe compiler, masks, ramps and validators with deterministic/property/collision regressions before enabling recipes in match setup.
3. Integrate presentation assets/materials/lights and run quality/performance gates.
4. Perform muted browser playtests on representative recipes, then update `docs/GAME_SPEC.md`, commit the change and preserve exact generator/profile identities in replay data.
5. Roll back by selecting the shipped `broken-ring-v2` generator identity; no migration rewrites existing match/replay data.

### 7. Исправление collision/render alignment после playtest

Стойки и дуги portal-facade компилируются в canonical box surfaces из общего authored part contract, используемого генератором GLB. Renderer использует ту же привязку и uniform scale по реальной ширине проёма; старые definitions сохраняют прежнее поведение. Wall-bay целиком вписывается в host wall solid по измеренным bounds. UV строятся по отдельным граням с учётом геометрического масштаба; шаблоны и их материалы не мутируют.
