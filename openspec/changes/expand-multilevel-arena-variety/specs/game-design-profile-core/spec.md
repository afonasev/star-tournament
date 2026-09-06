## ADDED Requirements

### Requirement: Профильные budgets multi-level arena
Immutable generator profile SHALL содержать descriptor-backed bounds для hall dimensions, layer height, slab thickness, headroom, tunnel clearance, ramp width/length и bypass length. Cross-field validation MUST отклонять profile, нарушающий controller slope, capsule headroom либо три effective capsule diameters.

#### Scenario: Изменён capsule
- **WHEN** effective capsule diameter меняется и minimum tunnel clearance становится меньше трёх диаметров
- **THEN** profile validation отклоняет configuration до generation
