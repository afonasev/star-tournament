## MODIFIED Requirements

### Requirement: Kinematic capsule query contract
Collision foundation SHALL вычислять допустимое перемещение profile-defined body capsule и возвращать сериализуемые position, grounded state и ordered contacts без владения acceleration, gravity, jump или air-control правилами. Capsule MUST покрывать approved robot body, применять movement-blocking static geometry и living participants и MUST NOT менять projectile filters либо combat hit volumes.

#### Scenario: Стена и скольжение
- **WHEN** participant движется по диагонали в статическую стену
- **THEN** body capsule не пересекает geometry, результат сохраняет допустимую касательную составляющую и возвращает стабильную contact normal

#### Scenario: Угол и низкий потолок
- **WHEN** participant входит в угол либо под препятствие с недостаточной высотой
- **THEN** результат остаётся вне geometry, не создаёт tunnelling или ошибочный upward displacement и сохраняет существующую вертикальную capsule семантику

#### Scenario: Земля, склон, ступень и край
- **WHEN** fixture последовательно проверяет landing, slope threshold, допустимую ступень, слишком высокую ступень и сход с края
- **THEN** grounded/contact результаты соответствуют versioned fixture expectations

#### Scenario: Spawn overlap
- **WHEN** participant проверяется в занятой или пересекающей static geometry точке
- **THEN** overlap query детерминированно сообщает invalid placement для body capsule без ожидания и без mutation gameplay state
