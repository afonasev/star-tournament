# graphics-quality-settings Specification

## Purpose

Capability предоставляет компактное DOM-меню графики, чтобы игрок выбирал качество renderer без влияния на матч, управление или игровое пространство.

## Requirements

### Requirement: Renderer-only графические пресеты
Browser shell SHALL предоставлять в menu компактную graphics surface с пресетами `Low`, `Balanced`, `High` и `Ultra`, показывающую выбранный preset и доступные отдельные controls render scale, texture quality, geometry LOD, decals и post-processing. Surface MUST оставаться DOM-слоем, не закрывать playfield во время running match и не менять simulation input, snapshot или replay.

#### Scenario: Выбор пресета до матча
- **WHEN** пользователь выбирает один из четырёх preset в menu
- **THEN** browser presentation применяет соответствующий renderer-only profile до следующего матча, а match configuration и arena identity остаются прежними

#### Scenario: Открытие графики во время матча
- **WHEN** пользователь открывает graphics surface из pause menu
- **THEN** pointer lock и fixed-step runner остаются paused до закрытия menu, а после resume выбранное качество не создаёт дополнительных simulation ticks

### Requirement: Доступные настройки и честный feedback
Graphics surface SHALL показывать недоступный control disabled с краткой причиной, а для каждой доступной настройки SHALL публиковать текущее effective value. Browser MUST сохранять выбор локально как presentation preference и MUST безопасно использовать `Balanced`, если preference невалиден или устройство не поддерживает запрошенный presentation tier.

#### Scenario: Недоступное качество
- **WHEN** устройство, browser или renderer не поддерживает выбранный quality option
- **THEN** control показывает недоступность без ошибки запуска, а effective profile использует ближайшее поддерживаемое значение либо `Balanced`

#### Scenario: Перезапуск browser session
- **WHEN** пользователь запускает следующий локальный матч в той же browser profile
- **THEN** menu восстанавливает сохранённый presentation preference без изменения arena seed, match settings или deterministic identity

### Requirement: Quality budget локальных теней

Active graphics profile SHALL определять effective local-light shadow budget без изменения match configuration, arena identity, input, snapshot или replay: `Low` — 0 casters, `Balanced` — 1, `High` — 2, `Ultra` — 4. Профиль MUST применять только presentation profile values для shadow-map resolution и soft radius.

#### Scenario: Смена graphics preset
- **WHEN** пользователь выбирает другой поддерживаемый graphics preset до следующего матча
- **THEN** browser presentation применяет соответствующий local-light shadow budget к следующему renderer session без изменения arena seed или simulation result
