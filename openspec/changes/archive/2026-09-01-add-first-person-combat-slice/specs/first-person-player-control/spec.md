## Purpose

Capability определяет первый управляемый first-person seat: сериализуемое движение и ориентацию игрока, keyboard/mouse action mapping, collision response и безопасный pointer-lock lifecycle без зависимости симуляции от browser API.

## ADDED Requirements

### Requirement: Детерминированное состояние локального игрока
Simulation SHALL хранить versioned сериализуемое состояние игрока, достаточное для движения, collision reconstruction, first-person ориентации и replay; renderer, DOM events и физическое устройство MUST NOT входить в snapshot.

#### Scenario: Одинаковые action frames
- **WHEN** два runner начинают с одинаковых profile, arena, snapshot и получают одинаковые movement/look/jump action frames
- **THEN** position, velocity, grounded, yaw, pitch и state hash совпадают после каждого tick

#### Scenario: Разная render cadence
- **WHEN** один поток action frames отображается с разной частотой кадров
- **THEN** movement и camera orientation simulation state не меняются

### Requirement: Аркадное движение и collision
Игрок SHALL двигаться с ground acceleration и strafe, испытывать profile-defined gravity, выполнять edge-triggered jump только из допустимого grounded state и сохранять ограниченный air control; итоговая position MUST приниматься симуляцией только после backend-neutral capsule collision query.

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
- **WHEN** capsule движется в стену, угол, потолок, ступень, склон или край
- **THEN** simulation использует ordered collision result, не проникает в static geometry и сохраняет только допустимое tangential movement

### Requirement: First-person orientation и камера
Normalized look actions SHALL обновлять сериализуемые yaw и pitch; yaw MUST поддерживать непрерывный поворот, pitch MUST быть ограничен profile-defined диапазоном, а renderer SHALL выводить camera transform и FOV из snapshot и активного profile.

#### Scenario: Mouse look
- **WHEN** pointer lock активен и пользователь перемещает мышь
- **THEN** следующий action frame содержит normalized look delta, а viewport поворачивается в соответствующую сторону с profile-defined sensitivity

#### Scenario: Ограничение pitch
- **WHEN** накопленный vertical look превышает верхнюю или нижнюю границу
- **THEN** pitch остаётся на соответствующей profile-defined границе без camera flip

#### Scenario: Resize
- **WHEN** viewport меняет aspect ratio
- **THEN** camera projection обновляется, сохраняя profile FOV, snapshot orientation и simulation state

### Requirement: Keyboard/mouse focus и pointer lock
Первый срез SHALL иметь один local keyboard/mouse seat. Gameplay movement, look, jump и fire MUST поступать только при активном pointer lock; потеря lock, blur или скрытие страницы SHALL очистить held actions и остановить gameplay ticks до нового явного действия пользователя.

#### Scenario: Начало игры
- **WHEN** совместимый browser runtime готов, но pointer lock ещё не получен
- **THEN** сцена и compact HUD видимы, DOM overlay предлагает кликнуть для начала, а gameplay ticks и fire не выполняются

#### Scenario: Pointer lock получен
- **WHEN** пользователь активирует start/resume overlay и браузер подтверждает pointer lock
- **THEN** overlay скрывается, cursor не управляет DOM и keyboard/mouse actions направляются единственному local seat

#### Scenario: Pointer lock потерян
- **WHEN** pointer lock пропадает из-за Escape, browser UI или смены фокуса
- **THEN** held movement/fire очищаются, runtime перестаёт выполнять gameplay ticks и показывает overlay продолжения

#### Scenario: Gamepad не участвует в slice
- **WHEN** gamepad подключается или отключается во время этого change
- **THEN** состояние единственного keyboard/mouse seat, viewport и simulation не меняется
