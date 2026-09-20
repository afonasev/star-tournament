## Purpose

Подключает утверждённые команды к native Unity Player для полного human-only матча с2–4 local seats на существующей фиксированной арене.

## ADDED Requirements

### Requirement: Setup и неизменная конфигурация
Player SHALL позволять выбрать FFA либо Team A/B, назначить каждому активному месту команду и поменять ориентацию существующей контрастной пары. Teams SHALL требовать обе непустые команды; invalid draft блокирует обычный и diagnostic start с пояснением. Repeat SHALL сохранять состав, цвета, устройства и frozen profiles, очищая lifecycle.
#### Scenario: Невалидный состав
- **WHEN** все активные места назначены одной команде
- **THEN** старт недоступен, причина показана, автоматического перераспределения нет
#### Scenario: Repeat
- **WHEN** командный матч повторён из pause или results
- **THEN** команды/цвета сохраняются, health/ammo/score/time/input/corpses начинаются заново

### Requirement: Союзный огонь и spawn
Живой союзник SHALL блокировать дробь без damage/kill/assist. Противник за союзником не получает эту дробину. Respawn SHALL оценивать LOS/navigation только до противников, сохраняя физическую занятость всех живых capsules. Initial teams SHALL получать distinct physically valid slots в стабильном порядке с профильным минимумом opponent separation.
#### Scenario: Союзник перед противником
- **WHEN** ближайший hit volume на пути дробины принадлежит союзнику
- **THEN** оба остаются без урона от этой дробины, ammo/cooldown выстрела применены
#### Scenario: Initial и respawn
- **WHEN** матч начинается либо несколько жизней готовы к respawn
- **THEN** slots не совпадают и не заняты; initial opponents соблюдают profile separation, видимый союзник не считается угрозой

### Requirement: Командная презентация результата
Live, permanent third-seat table и results SHALL группировать участников по Team A/B и показывать суммы и личные stats. Winner SHALL называться Team A/B. Identity colors SHALL совпадать в HUD/world weapon/viewmodel и сохраняться после respawn/Repeat. FFA SHALL сохранять индивидуальный winner и palette.
#### Scenario: Командная победа
- **WHEN** reducer завершает матч командной победой
- **THEN** native results показывают верную команду и суммы; Repeat/menu доступны
