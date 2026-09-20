## Purpose

Даёт native Unity матчу пять утверждённых пространственных семейств с явным canonical profile selection и стабильной arena identity поверх ARENA-1.

## ADDED Requirements

### Requirement: Явный выбор зарегистрированного семейства
Canonical arena profile SHALL явно выбирать ровно одно зарегистрированное семейство из `wide-hall-circuit-v1`, `perimeter-gallery-v1`, `split-balconies-v1`, `stacked-combat-v1` и `lower-basement-v1`. Seed MUST NOT выбирать или подменять family, а неизвестное значение MUST отклоняться до создания gameplay session.

#### Scenario: Profile выбирает семейство
- **WHEN** два generation request используют одинаковые seed и generator version, но разные допустимые family values в canonical profile
- **THEN** они создают definitions с соответствующими разными family IDs и identities

#### Scenario: Недопустимый family value
- **WHEN** canonical profile содержит значение вне зарегистрированных пяти семейств
- **THEN** generation отклоняется со стабильной причиной до Unity projection и session creation

### Requirement: Пять различимых канонических layouts
Каждое зарегистрированное family SHALL создавать immutable `ArenaDefinition` с собственными named supports, solids, spawn regions, route anchors и минимум двумя declared bidirectional transitions. Native collision, navigation, spawn и presentation SHALL выводиться только из этой accepted definition.

#### Scenario: Представительские definitions
- **WHEN** генерируются все пять family profiles с одним seed
- **THEN** каждая definition имеет свой stable family ID, отличимую content identity, нижнюю и верхнюю либо подвальную support topology, spawn catalog и не менее двух declared transitions

#### Scenario: Unity projection
- **WHEN** accepted definition любого из пяти семейств строится в native Player
- **THEN** её colliders, NavMesh inputs, support identity, spawn positions и visible geometry соответствуют named elements этой definition без второго spatial source

### Requirement: Ограниченная совместимость ARENA-2
ARENA-2 SHALL сохранить независимость arena RNG от match RNG и стабильность identical requests из ARENA-1. Этот change MUST NOT вводить size presets, generated-map navigation, complete route/fairness validators, Repeat freeze, retry/fallback, replay schema или delivery lifecycle.

#### Scenario: Повторяемый family request
- **WHEN** два независимых запуска используют одинаковые seed, generator version и canonical family profile
- **THEN** они получают semantically identical definitions и identities до initial tick
