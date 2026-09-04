## MODIFIED Requirements

### Requirement: Clean Future Sport material kit
Arena renderer SHALL представлять validated `ArenaDefinition` в стиле `clean-future-sport-v1`. Крупные wall bays и portal facades MUST совпадать с canonical collision shell wall surfaces; мелкие trims, seams, decals, floor route guides и landmark MUST оставаться presentation-only. Renderer MUST NOT создавать независимые blocking volumes.

#### Scenario: Совпадение крупной панели и collision
- **WHEN** крупная panel bay видна на route wall
- **THEN** её outer boundary совпадает с canonical collision shell, а capsule не может пройти сквозь неё

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
