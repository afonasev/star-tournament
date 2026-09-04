## MODIFIED Requirements

### Requirement: Clean Future Sport material kit
Arena renderer SHALL представлять validated одноуровневую `ArenaDefinition` в стиле `clean-future-sport-v1`: renderer-native architectural kit со светлыми off-white/pale-gray wall bays и buttresses, крупными двухцветными curved portal facades на существующих doorway gaps, тёмным navy-composite floor с широкими segmented route guides и cyan/lime/orange accents для route, doorway и landmark surfaces. `clean-future-sport-texture-v1` SHALL использовать бесшовные повторяемые PBR material sets для wall/floor surfaces и atlas-backed decals только для уникальных route markings, sector labels и мелких декоративных знаков. Базовый material contract MUST содержать base color, normal и ORM maps; presentation detail-tier может добавлять detail-normal и decal layers. Материалы, texture variants, decals и detail meshes MUST выводиться только из существующих semantic surface slots и MUST NOT менять collision, navigation, spawn или arena content hash.

#### Scenario: Одна spatial identity при смене стиля
- **WHEN** тот же validated ArenaDefinition отображается с style kit либо без renderer
- **THEN** все simulation, collision, navigation, spawn и replay identities остаются одинаковыми

#### Scenario: Навигационный акцент
- **WHEN** игрок смотрит на doorway, маршрут или центральный landmark
- **THEN** соответствующий architectural accent читается отдельно от базового пола и shell без назначения ему team или participant color

#### Scenario: Детальный слой не меняет игровое пространство
- **WHEN** renderer добавляет seams, frames, trims, rounded portals, верхний central landmark, textures либо decals
- **THEN** они остаются presentation-only, не образуют ложных doorways/cover и не участвуют в collision, navigation, spawn или arena hashing

#### Scenario: Округлый портал следует реальному doorway
- **WHEN** два collinear wall segments оставляют разрешённый doorway gap
- **THEN** renderer может обрамить именно этот gap двумя стойками и верхней дугой, но MUST NOT добавлять такой портал на непрерывную wall surface

#### Scenario: Силуэт читается с игровой дистанции
- **WHEN** игрок видит wall либо doorway на representative gameplay distance
- **THEN** крупные panel modules, two-tone portal shell и floor guide различимы как архитектурный ритм, а не как набор тонких технических линий

### Requirement: Shipping texture variants
Renderer SHALL выбирать GPU-compressed shipping texture variants исключительно по active graphics-quality profile: повторяемые material maps authorятся не выше 2K, unique landmark и atlas maps — не выше 4K, а texture residency MUST оставаться в budget profile 128 MiB (`Low`), 256 MiB (`Balanced`), 384 MiB (`High`) либо 512 MiB (`Ultra`). Загрузка, cache, UV/sampler state и quality selection MUST NOT входить в `ArenaDefinition`, collision, navigation, spawn, snapshot, replay, RNG или state hash.

#### Scenario: Повышение visual detail
- **WHEN** пользователь выбирает более высокий graphics-quality profile
- **THEN** renderer может выбрать более детальные shipping variants и detail layers в пределах profile budget без изменения gameplay transform, collision или replay result

#### Scenario: Ограниченный texture budget
- **WHEN** запрошенные presentation resources превышают budget активного profile
- **THEN** renderer понижает только texture/detail tier до допустимого варианта и продолжает показывать арену без изменения simulation state
