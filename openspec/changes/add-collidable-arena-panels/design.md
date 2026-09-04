## Context

Крупные GLB bays выступают за current wall collision shell. Generator уже выводит wall thickness из versioned game-design profile, поэтому shell может стать единственным canonical proxy без renderer imports.

## Goals / Non-Goals

**Goals:** увеличить canonical wall thickness до границы крупного panel bay, включить изменение в validated definition/hash и сохранить corridor/doorway clearance.

**Non-Goals:** не делать collider из GLB, trim, decal, guide или landmark; не вводить renderer state в simulation.

## Decisions

Увеличить profile-owned wall thickness для каждого arena size на постоянный panel-shell allowance. Generator уже применяет thickness к perimeter, rooms, doors и navigation validation, поэтому replay/state hash получают новое spatial identity без отдельного mutable proxy. Renderer размещает large bays flush with revised shell; мелкие details остаются inset.

## Risks / Trade-offs

- [Более толстые walls могут закрыть route] → property corpus и spawn/navigation validators отклоняют seed.
- [Визуальный GLB drift] → renderer derives placement only from canonical surface extent.
