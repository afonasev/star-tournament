## Purpose

Capability гарантирует, что видимое тело живого участника не пересекает canonical static geometry арены, сохраняя отдельными combat hit volumes и renderer-only оружие.

## ADDED Requirements

### Requirement: Wall-clearance живого participant body
Каждый живой participant SHALL иметь profile-defined body capsule, радиус которой покрывает approved visible robot silhouette. Его видимое тело MUST оставаться вне canonical wall, corner, portal, barrier, ramp и slab при movement, collision response, spawn и respawn; capsule MUST NOT менять participant damage volumes, projectile query либо collision трупа.

#### Scenario: Движение в стену и угол
- **WHEN** живой participant идёт или strafe-ится в прямую wall либо внутренний угол
- **THEN** body остаётся вне static geometry, допустимое касательное движение сохраняется, а contacts воспроизводимы

#### Scenario: Initial placement
- **WHEN** initial spawn либо respawn находится у static geometry
- **THEN** allocation отвергает placement, в котором body capsule пересекает static geometry, до первого tick

### Requirement: Оружие отводится только в presentation
World weapon и first-person viewmodel SHALL визуально retract только когда renderer forward query под прицелом пересекает static wall либо renderer-owned conservative envelopes wall-attached presentation decor. Боковая близость к такому obstacle MUST NOT менять weapon pose или блокировать shot. При недостаточной глубине retraction MUST сохранить минимальную видимую сложенную позу перед camera near plane и MUST NOT скрывать weapon через scale, opacity либо уход за camera. Retraction MUST быть derived из renderer-visible collision-safe distance и MUST NOT менять simulation position, yaw, pitch, shot origin, damage, ammo, cooldown, snapshot, replay или state hash.

#### Scenario: Look у стены
- **WHEN** local participant свободно меняет yaw либо pitch рядом со static wall
- **THEN** gameplay orientation не блокируется и не сдвигает participant, а presentation weapon остаётся снаружи wall и сохраняет видимую сложенную позу

#### Scenario: Декорированная стена
- **WHEN** weapon приближается к выступающему wall-attached presentation decor
- **THEN** renderer учитывает его conservative envelope, не допускает visual clipping и не добавляет obstacle в simulation collision
