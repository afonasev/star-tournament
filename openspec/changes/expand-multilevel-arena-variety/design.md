## Context

See proposal.md. Current arena elevation resolves the highest floor at XZ, navigation projects obstacles to XZ, and physical validation accepts waypoint arrival by XZ. Those paths cannot represent a basement under a gallery or an independent upper combat floor.

## Goals / Non-Goals

**Goals:**
- Compile five user-approved spatial families into a deterministic canonical arena with real layer identity, support surfaces, slabs and declared transitions.
- Preserve a single simulation-owned spatial truth while making renderer material variation derived and quality-bounded.
- Keep historical arena/profile identities runnable without rewriting replay data.

**Non-Goals:**
- A manual editor, arbitrary 3D navmesh, stairs, jump routes, new weapons, input mapping, split-screen layout or online authority are out of scope.

## Decisions

### 1. Support-layer graph precedes geometry

Recipe planning resolves rooms, halls, bypasses, supports and portal-linked transitions before canonical surfaces are compiled. Regions and anchors name their support; a link is either intra-support or an explicit transition. This is selected over increasing the old raised strip because overlapping XZ floors otherwise collapse into one highest floor.

### 2. Layer-aware sparse navigation

Navigation creates candidates within a support layer and crosses layers only through declared portal/ramp links. Stable semantic ordering and arena/profile keyed caching retain determinism. Horizontal aim distance remains separate from support-aware route and arrival distance; replacing every distance calculation with 3D would corrupt aiming behaviour.

### 3. Physical validation owns proof of transitions

Уточнение 2026-09-08: узел содержит `supportSurfaceId` и XYZ; одинаковые XZ не дедуплицируются между опорами. Footprint рампы блокирует обычный нижний маршрут. Входы и waypoint перехода выводятся только из указанной `transitionSurfaceId`; глобальные X-breakpoints запрещены. AI проверяет достижение по опоре и высоте, сохраняя горизонтальную дистанцию прицеливания. На наклоне physical validator учитывает геометрический контакт вертикальной capsule с плоскостью; на плоской площадке требуется фактическая высота стоп и declared support.

Derived Rapier worlds validate every declared transition, three lateral lanes and corner sweep in both directions, checking feet Y, grounded contact and final support identity. Existing XZ-only success is insufficient.

### 4. Profile-owned combat clearance

The generator computes required width as `3 * 2 * (radius + skinWidth)`, currently 2.46 m, and rounds release presets to at least 2.5 m. Hall/tunnel/ramp dimensions, headroom and slab thickness are descriptors with strict cross-field validation. This is selected over a static metre literal so a future capsule change cannot silently invalidate the player promise.

### 5. Canonical gameplay versus presentation texture variants

Floors, slabs, guards and ramps remain canonical when they affect collision or shots. Sector textures/decals, light arrangement and quality residency remain renderer-owned and select only from semantic slots. Existing manifest/cache lifecycle is retained; a second loader is rejected.

### 6. Compatibility and promotion

Schema/generator/profile identities advance only for new multi-level definitions. Legacy definitions retain their historical elevation/navigation compatibility path. A recipe promotion requires deterministic corpus, physical route, bot, fairness/LOS, renderer/performance and muted browser evidence for every family.

## Risks / Trade-offs

### Physical-navigation implementation slice, 2026-09-08

New generation uses `arena-layers-v2`, profile schema 8 / revision 9. Revision 8 retains its immutable release and legacy generator/validation behavior. Ordinary navigation edges retain support identity; declared continuous ramps provide explicit landing/ramp/landing waypoints. AI navigation converts capsule centers to feet without changing aim coordinates and preserves transitions until the landing is reached. Ramp-interior targets also attach through declared transition entries.

The basement has one continuous main slab and a lower floor extended to contain two external descents. Canonical portal lintels follow the relevant support elevation. Physical acceptance walks all spawn/combat routes and each ramp in both directions, including three lateral capsule lanes and corner sweeps; grounded support and headroom come from the derived Rapier world.

Rapier 0.20 can occasionally return snap movement through an already contacted flat floor. Only the new layered generators retry that exact original pose and translation with snap temporarily disabled when the candidate crosses that floor. Collision filtering remains active, configured snap is restored immediately, and a retry still crossing support fails explicitly. No feet clamping or physical-validator exemption is used.

Guided AI goals verify reachability across every family/size with zero recovery; this does not claim autonomous combat exploration covers every layer. Presentation, performance promotion, and keyboard/mouse browser acceptance remain separate gates.

- [Layer graph may increase nav nodes/edges] → use local support candidates and declared sparse portals; record node/edge and generation-time metrics.
- [Upper fire may bias spawn fairness] → assess actual FFA/team allocations and LOS/routes to every combat region; do not relax existing budgets.
- [Slabs and sector textures may exceed split-screen budget] → preserve quality tiers and run the most dense large recipe through existing performance gates.
- [Historic replay regression] → retain versioned parser and legacy route/elevation behavior under historical identities.

## Migration Plan

1. Add schema/profile compatibility readers and support-aware validators behind a new generator identity.
2. Compile and validate the five recipe families, then connect AI/collision/spawn projections.
3. Add presentation variants and assets without changing canonical hashes.
4. Run automated corpus and physical gates, then muted browser playtests and screenshots.
5. Roll back by selecting the previous generator/profile identity; do not rewrite stored definitions.
