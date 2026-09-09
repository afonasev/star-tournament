# architectural-height-semantics Specification

## Purpose

Capability задаёт проверяемую семантику высоты архитектуры, чтобы читаемые стены образовывали непрерывный объём до потолка, а низкие объекты были однозначно укрытиями.

## Requirements

### Requirement: Full-height primary architecture
Accepted procedural arena SHALL строить primary corridor, room и perimeter walls от локального floor до profile-defined потолка соответствующего помещения; historical generators MAY использовать общий `wallHeightMeters`. Эти surfaces MUST иметь canonical collision geometry и входить в navigation, spawn safety и arena content hash.

#### Scenario: Центральная стена
- **WHEN** generator создаёт primary wall в combat либо route architecture
- **THEN** её верхняя грань совпадает с profile-defined ceiling height и не оставляет случайный вертикальный разрыв

### Requirement: Intentional low obstacles
Only explicitly identified movement-only barriers MAY быть ниже primary wall height. Они MUST сохранять canonical collision geometry, явную movement/projectile filter semantics и не маскироваться как полная wall chain. Их высота MUST быть выбрана из versioned profile range, а validator MUST подтвердить, что барьер не создаёт narrow gap, непроходимый маршрут либо недопустимое spawn advantage.

#### Scenario: Низкое укрытие
- **WHEN** arena содержит movement-only barrier
- **THEN** его высота меньше потолка, capsule movement блокируется его фактическим canonical extent, а hitscan/LOS не получают от него occlusion

#### Scenario: Перепад пола
- **WHEN** canonical floor surface образует elevation zone и ramp
- **THEN** validator использует её фактические heights для capsule route, spawn safety и navigation distance

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
