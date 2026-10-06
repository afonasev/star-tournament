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
Profile SHALL задавать height, length и width переходов; ramps MUST проходить slope, headroom и triple-capsule-width validation, stairs MUST дополнительно проходить проверки autostep и размеров проступи.

#### Scenario: Заметная рампа
- **WHEN** recipe создаёт primary transition
- **THEN** его высота и длина отличимы от малого legacy elevation и он остаётся двусторонне проходимым

### Requirement: Настоящие лестничные переходы
Лестница SHALL состоять из canonical box-ступеней, одинаковых для renderer и collision. Каждая проступь MUST иметь собственную опору; declared transition MUST перечислять упорядоченные ступени, вход и выход. Autostep MUST обеспечивать подъём и спуск без обязательного прыжка.

#### Scenario: Проход лестницы
- **WHEN** персонаж бежит между этажами по лестнице
- **THEN** он проходит настоящие ступени без скрытой collision-рампы, проникновений и обязательных прыжков
