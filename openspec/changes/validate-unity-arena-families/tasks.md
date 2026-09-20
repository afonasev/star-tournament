## 1. ARENA-4 contract

- [x] 1.1 Add the pure validation result/diagnostic and definition validation of geometry, supports, collision layers, spawn data and transitions.
- [x] 1.2 Gate `ArenaGenerator.Generate` and defensive projection validation through the same validator without retry/fallback.

## 2. Evidence

- [x] 2.1 Add focused EditMode mutation coverage for every validator category and all five accepted families.
- [x] 2.2 Add focused PlayMode coverage that accepted definitions project and rejected definitions do not create an arena projection.
- [x] 2.3 Run strict OpenSpec validation and the relevant Unity EditMode/PlayMode tests; record exact evidence and remaining non-goals/gates.
