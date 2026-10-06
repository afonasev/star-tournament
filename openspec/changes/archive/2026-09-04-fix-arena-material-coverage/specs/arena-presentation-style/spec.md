## MODIFIED Requirements

### Requirement: Clean Future Sport material kit
Arena renderer SHALL представлять validated одноуровневую `ArenaDefinition` в стиле `clean-future-sport-v1`: renderer-native architectural kit со светлыми off-white/pale-gray wall bays, shell и ceiling-facing architecture, крупными двухцветными curved portal facades на существующих doorway gaps, тёмным navy-composite floor с широкими segmented route guides и cyan/lime/orange accents для route, doorway и landmark surfaces. Все видимые крупные architectural surface classes MUST получать этот material language и MUST NOT оставаться однотонными fallback planes. Материалы и detail meshes MUST выводиться только из существующих semantic surface slots, иметь однозначное presentation depth separation и MUST NOT менять collision, navigation, spawn или arena content hash.

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
