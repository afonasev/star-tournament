## Purpose

Предоставить отдельный native полигон для проверки четырёх локальных игроков, многоэтажного движения и навигации перед переходом полного Star Tournament на Unity.

## ADDED Requirements

### Requirement: Four independent local players
Полигон SHALL поддерживать четыре управляемых local seats одновременно, отдельные камеры/HUD и уникальное назначение четырёх gamepads либо одной keyboard/mouse пары и трёх gamepads.

#### Scenario: Four seats start
- **WHEN** назначены четыре доступных уникальных устройства и пользователь начинает игру
- **THEN** четыре равных viewport образуют сетку 2×2, а команды каждого устройства действуют только на его игрока и камеру

#### Scenario: Missing or duplicate device
- **WHEN** хотя бы один slot не имеет доступного уникального устройства
- **THEN** обычный четырёхместный старт недоступен и причина видна в setup

### Requirement: Focus and reconnect safety
Полигон SHALL ставить общий runtime на паузу при потере focus/назначенного устройства, очищать held input и возобновляться только явно с восстановленными устройствами; rebind SHALL выполняться в setup.

#### Scenario: Disconnect while moving
- **WHEN** назначенный gamepad отключён при движении
- **THEN** все gameplay ticks остановлены, cursor освобождён, причина показана, сам reconnect не возобновляет движение

### Requirement: Physical multilevel fixture
Полигон SHALL содержать два уровня с overlapping XZ, рампу и настоящую лестницу, headroom препятствие и различимые movement/projectile queries. Живые игроки SHALL блокировать движение друг друга.

#### Scenario: Floor separation and transitions
- **WHEN** игрок или route probe переходит между уровнями
- **THEN** маршрут идёт через физический переход, не сквозь slab; оба направления рампы и лестницы доступны без обязательного прыжка

#### Scenario: Contact and ceiling
- **WHEN** игрок прыгает под потолком либо движется в стену или живого игрока
- **THEN** collider препятствует прохождению, а presentation не изменяет игровой результат

### Requirement: Profile and presentation separation
Полигон SHALL хранить регулируемые числа движения/камер/читаемости в именованном профиле с единым metadata registry и range validation; gameplay state SHALL быть сериализуемым отдельно от input/render objects. Existing robot/shotgun assets SHALL сохранять semantic orientation/mounts и быть видимыми в Player.

#### Scenario: Invalid profile value
- **WHEN** настройка выходит за жёсткий диапазон descriptor
- **THEN** профиль отклоняется с конкретным path, а UI показывает тот же диапазон

#### Scenario: Imported presentation
- **WHEN** native Player показывает участников и их оружие
- **THEN** модели имеют корректный размер/ориентацию/материалы, не меняют collider и не заменяются молча диагностическими примитивами

### Requirement: Honest native acceptance
Срез SHALL иметь воспроизводимые автоматические проверки, Player build и muted native playtest evidence. Цель SHALL составлять минимум 60 FPS для четырёх игроков при Full HD–4K; неподтверждённые hardware/quality/physical conditions SHALL оставаться открытыми.

#### Scenario: Synthetic camera benchmark
- **WHEN** измерение выполнено четырьмя камерами или синтетическим input без четырёх реальных устройств
- **THEN** evidence содержит build/profile/device/resolution/quality/frame percentiles и пометку diagnostic; physical acceptance не объявляется выполненной

#### Scenario: Historical baseline
- **WHEN** сравниваются Unity и browser baseline 8844e40
- **THEN** сравниваются поведение и измеренные показатели с точными identities, без обещания идентичного command replay или seed layout
