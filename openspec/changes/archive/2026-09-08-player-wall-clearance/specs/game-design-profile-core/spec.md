## MODIFIED Requirements

### Requirement: Полный профиль первого playable combat slice
`prototype-v1` SHALL содержать обязательные числовые параметры body capsule, ground/air movement, jump/gravity, first-person camera и double-barrel shotgun pellets/spread/range/cadence; runtime MUST потреблять эти значения из валидированного profile snapshot без дублирующих gameplay constants.

#### Scenario: Полное descriptor coverage
- **WHEN** schema `prototype-v1` расширена параметрами playable slice
- **THEN** каждое новое числовое поле имеет ровно один descriptor с path, группой, подписью, описанием, единицей, minimum, maximum и step

#### Scenario: Shipped defaults
- **WHEN** runtime запускает первый playable slice
- **THEN** movement, camera, body capsule и shotgun используют значения одной immutable shipped revision и её content hash

#### Scenario: Повреждённый profile
- **WHEN** отсутствует новое обязательное поле, значение вне range либо нарушено cross-field отношение body capsule/camera/weapon
- **THEN** validation останавливает gameplay startup со стабильным code и paths до первого gameplay tick

#### Scenario: Replay compatibility
- **WHEN** replay или snapshot создан с другой revision либо content hash combat profile
- **THEN** runtime отклоняет его до применения movement или shot action frame
