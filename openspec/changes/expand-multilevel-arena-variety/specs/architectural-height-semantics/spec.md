## ADDED Requirements

### Requirement: Canonical multi-level supports and slabs
ArenaDefinition SHALL явно описывать floor support, overhead slab и headroom для каждого gameplay layer. Slab MUST блокировать movement и projectile queries, а renderer MUST NOT заменять его presentation-only ceiling.

#### Scenario: Верхняя арена над подвалом
- **WHEN** upper combat floor расположен над basement route
- **THEN** обе зоны имеют независимые support surfaces, достаточный profile-defined headroom и canonical slab, блокирующий межэтажный ray

### Requirement: Выраженные переходы высоты
Profile SHALL задавать height, length и width главных ramps; эти параметры MUST проходить slope, headroom и triple-capsule-width validation.

#### Scenario: Заметная рампа
- **WHEN** recipe создаёт primary transition
- **THEN** его высота и длина отличимы от малого legacy elevation и он остаётся двусторонне проходимым
