## ADDED Requirements

### Requirement: Локальные потолки помещений
Помещение SHALL иметь canonical floor elevation и ceiling elevation. Seed MUST выбирать различные высоты из profile-owned диапазонов как между генерациями, так и между помещениями одной карты. Primary walls MUST заканчиваться на собственном потолке; потолок MUST блокировать movement и projectile queries. Общий presentation-only потолок MUST NOT заменять локальные потолки новых definitions.

#### Scenario: Тоннель переходит в зал
- **WHEN** маршрут проходит из низкого широкого тоннеля в высокий зал
- **THEN** потолки имеют разные высоты, стык не создаёт щели и physical headroom validation подтверждает движение и прыжки без проникновения в потолок

#### Scenario: Закрытая рампа
- **WHEN** рампа соединяет закрытые этажи
- **THEN** потолок перехода сохраняет profile-defined headroom на всём наклоне и на обоих входах

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
