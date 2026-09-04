## MODIFIED Requirements

### Requirement: Clean Future Sport material kit
Arena renderer SHALL представлять validated одноуровневую `ArenaDefinition` в стиле `clean-future-sport-v1`: versioned `clean-future-sport-glb-v1` architectural kit со светлыми off-white/pale-gray wall bays и buttresses, крупными двухцветными curved portal facades на существующих doorway gaps, тёмным navy-composite floor с широкими segmented route guides и cyan/lime/orange accents для route, doorway и landmark surfaces. Модули MUST разрешаться только через GLB asset manifest, выводиться только из существующих semantic surface slots и MUST NOT менять collision, navigation, spawn или arena content hash.

#### Scenario: Одна spatial identity при смене стиля
- **WHEN** тот же validated ArenaDefinition отображается с GLB style kit либо без renderer
- **THEN** все simulation, collision, navigation, spawn и replay identities остаются одинаковыми

#### Scenario: Навигационный акцент
- **WHEN** игрок смотрит на doorway, маршрут или центральный landmark
- **THEN** соответствующий GLB architectural accent читается отдельно от базового пола и shell без назначения ему team или participant color

#### Scenario: Детальный слой не меняет игровое пространство
- **WHEN** renderer добавляет GLB wall bays, buttresses, route guides, curved portals и верхний central landmark
- **THEN** они остаются presentation-only, не образуют ложных doorways/cover и не участвуют в collision, navigation, spawn или arena hashing

#### Scenario: Округлый портал следует реальному doorway
- **WHEN** два collinear wall segments оставляют разрешённый doorway gap
- **THEN** renderer может расположить GLB portal facade только вокруг этого gap, но MUST NOT добавить его на непрерывную wall surface

#### Scenario: Силуэт читается с игровой дистанции
- **WHEN** игрок видит wall либо doorway на representative gameplay distance
- **THEN** крупные GLB panel modules, two-tone portal shell и floor guide различимы как архитектурный ритм, а не как набор тонких технических линий
