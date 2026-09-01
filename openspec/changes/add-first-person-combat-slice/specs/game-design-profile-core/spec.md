## ADDED Requirements

### Requirement: Полный профиль первого playable combat slice
`prototype-v1` SHALL содержать обязательные числовые параметры capsule, ground/air movement, jump/gravity, first-person camera и double-barrel shotgun pellets/spread/range/cadence; runtime MUST потреблять эти значения из валидированного profile snapshot без дублирующих gameplay constants.

#### Scenario: Полное descriptor coverage
- **WHEN** schema `prototype-v1` расширена параметрами playable slice
- **THEN** каждое новое числовое поле имеет ровно один descriptor с path, группой, подписью, описанием, единицей, minimum, maximum и step

#### Scenario: Shipped defaults
- **WHEN** runtime запускает первый playable slice
- **THEN** movement, camera, capsule и shotgun используют значения одной immutable shipped revision и её content hash

#### Scenario: Повреждённый profile
- **WHEN** отсутствует новое обязательное поле, значение вне range либо нарушено cross-field отношение capsule/camera/weapon
- **THEN** validation останавливает gameplay startup со стабильным code и paths до первого gameplay tick

#### Scenario: Replay compatibility
- **WHEN** replay или snapshot создан с другой revision либо content hash combat profile
- **THEN** runtime отклоняет его до применения movement или shot action frame

### Requirement: Отдельный presentation-профиль производительности
Browser presentation SHALL использовать immutable named profile с числовыми параметрами backing-buffer pixel budget и maximum HUD publication cadence. Каждый параметр MUST иметь стабильный path, группу, подпись, описание влияния, единицу, minimum, maximum и step; profile identity MUST быть отделена от gameplay profile и MUST NOT входить в simulation snapshot, replay или state hash.

#### Scenario: Descriptor coverage presentation-параметров
- **WHEN** shipped presentation profile загружается для browser runtime
- **THEN** pixel budget и HUD cadence имеют ровно по одному descriptor и проходят общую range/step validation до создания WebGL renderer

#### Scenario: Один матч с разными presentation-профилями
- **WHEN** два runner получают одинаковые gameplay snapshot/actions, но renderer использует разные валидные presentation profiles
- **THEN** их per-tick simulation snapshots, replay data и state hashes совпадают

#### Scenario: Повреждённый presentation-профиль
- **WHEN** обязательный parameter отсутствует, имеет неверный тип, выходит за range или не соответствует step
- **THEN** browser startup завершается стабильной validation error до первого gameplay tick и WebGL submission
