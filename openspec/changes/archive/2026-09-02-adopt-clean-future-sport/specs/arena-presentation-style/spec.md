## Purpose

Capability задаёт наблюдаемый visual language procedural arena: material kit, освещение, route accents и контраст участников, не влияющие на deterministic simulation.

## ADDED Requirements

### Requirement: Clean Future Sport material kit
Arena renderer SHALL представлять validated одноуровневую `ArenaDefinition` в стиле `clean-future-sport-v1`: renderer-native architectural kit со светлыми off-white/pale-gray wall bays и buttresses, крупными двухцветными curved portal facades на существующих doorway gaps, тёмным navy-composite floor с широкими segmented route guides и cyan/lime/orange accents для route, doorway и landmark surfaces. Материалы и detail meshes MUST выводиться только из существующих semantic surface slots и MUST NOT менять collision, navigation, spawn или arena content hash.

#### Scenario: Одна spatial identity при смене стиля
- **WHEN** тот же validated ArenaDefinition отображается с style kit либо без renderer
- **THEN** все simulation, collision, navigation, spawn и replay identities остаются одинаковыми

#### Scenario: Навигационный акцент
- **WHEN** игрок смотрит на doorway, маршрут или центральный landmark
- **THEN** соответствующий architectural accent читается отдельно от базового пола и shell без назначения ему team или participant color

#### Scenario: Детальный слой не меняет игровое пространство
- **WHEN** renderer добавляет seams, frames, trims, rounded portals и верхний central landmark
- **THEN** они остаются presentation-only, не образуют ложных doorways/cover и не участвуют в collision, navigation, spawn или arena hashing

#### Scenario: Округлый портал следует реальному doorway
- **WHEN** два collinear wall segments оставляют разрешённый doorway gap
- **THEN** renderer может обрамить именно этот gap двумя стойками и верхней дугой, но MUST NOT добавлять такой портал на непрерывную wall surface

#### Scenario: Силуэт читается с игровой дистанции
- **WHEN** игрок видит wall либо doorway на representative gameplay distance
- **THEN** крупные panel modules, two-tone portal shell и floor guide различимы как архитектурный ритм, а не как набор тонких технических линий

### Requirement: Контраст участников и безопасная палитра
Renderer и DOM presentation SHALL резервировать team и participant colors за участниками и HUD. Team A и Team B MUST получать различимую контрастную пару, а FFA MUST назначать одновременно присутствующим участникам попарно различимые цвета из расширяемой vetted palette; palette MUST содержать больше восьми допустимых swatches, хотя roster остаётся не более восьми участников.

#### Scenario: Полный FFA roster
- **WHEN** FFA запускается с восемью участниками
- **THEN** каждый живой participant имеет цвет, различимый от остальных семи и от ближайших architectural materials на игровой дистанции

#### Scenario: Командный матч
- **WHEN** teams match отображает живых участников обеих команд
- **THEN** Team A и Team B имеют разные цвета, а team identity не зависит от названий red или blue

### Requirement: Светлая спортивная читаемость
Arena renderer SHALL использовать яркое мягкое заполняющее освещение, контролируемый контровой свет для participants и минимальный haze. Presentation MUST избегать глубоких нечитаемых теней, хоррорного тона, милитаристских маркировок и графического насилия.

#### Scenario: Дальний opponent
- **WHEN** живой participant находится на representative gameplay distance в коридоре или combat region
- **THEN** его silhouette остаётся различимым на фоне arena shell и floor без HUD-only подсказки

#### Scenario: Возрастной тон
- **WHEN** renderer показывает arena, живых participants и corpse presentation
- **THEN** visual treatment остаётся в аркадном спортивном sci-fi тоне 10+ без gore или horror imagery
