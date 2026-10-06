## MODIFIED Requirements

### Requirement: Аркадное движение и collision
Игрок SHALL двигаться с ground acceleration и strafe, испытывать profile-defined gravity, выполнять edge-triggered jump только из допустимого grounded state и сохранять ограниченный air control; итоговая position MUST приниматься симуляцией только после backend-neutral profile-defined body capsule collision query.

#### Scenario: Разгон и остановка на земле
- **WHEN** игрок удерживает направление на земле, а затем отпускает его
- **THEN** horizontal velocity плавно достигает ограниченной скорости и затем уменьшается по profile-defined ground deceleration

#### Scenario: Прыжок
- **WHEN** grounded игрок нажимает jump
- **THEN** симуляция один раз применяет vertical jump impulse, после чего gravity возвращает игрока на walkable surface

#### Scenario: Удерживание jump
- **WHEN** игрок удерживает jump после отрыва
- **THEN** новые jump impulses не создаются до отпускания и следующего допустимого grounded press

#### Scenario: Air control
- **WHEN** airborne игрок меняет directional input
- **THEN** horizontal trajectory изменяется слабее, чем при ground acceleration, без скрытого изменения maximum speed

#### Scenario: Столкновение с ареной
- **WHEN** body capsule движется в стену, угол, потолок, ступень, склон или край
- **THEN** simulation использует ordered collision result, body model не проникает в static geometry и сохраняет только допустимое tangential movement
