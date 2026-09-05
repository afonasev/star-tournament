## MODIFIED Requirements

### Requirement: Versioned конфигурация матча
Матч SHALL запускаться только из валидной сериализуемой конфигурации с identity, roster от двух до восьми уникальных участников, одним seat-backed keyboard/mouse участником в browser либо без local seats в явно выбранном headless harness, режимом `ffa` либо `teams`, длительностью и nullable целью по очкам. FFA SHALL хранить индивидуальный participant color, назначенный из расширяемой vetted palette; `teams` MUST назначать каждого участника ровно в `team-a` либо `team-b`, содержать обе непустые команды и хранить одну контрастную team-color пару для Team A и Team B. Цвета и user-facing names MUST NOT использовать legacy identities `red` либо `blue`.

#### Scenario: Валидный FFA
- **WHEN** меню создаёт FFA с одним local seat, хотя бы одним ботом (либо stationary mannequin в diagnostic scenario), допустимыми попарно различимыми цветами, длительностью и выключенной целью
- **THEN** симуляция принимает immutable configuration и создаёт initial snapshot с тем же roster и identity

#### Scenario: Валидный командный матч
- **WHEN** roster содержит непустые Team A и Team B, одного seat-backed участника, ботов (либо stationary mannequin в diagnostic scenario) и допустимую контрастную team-color пару
- **THEN** configuration принимается, а цвет каждого участника выводится из его команды

#### Scenario: Недоступный participant contract
- **WHEN** configuration запрашивает второй local seat, gamepad, online participant, пустую команду, повторяющийся id либо число участников вне 2–8
- **THEN** validation отклоняет матч до первого tick со стабильным path и не подменяет участника mannequin без явной конфигурации

Боты SHALL хранить обязательный difficulty id `easy / normal / hard`, входящий в configuration identity и snapshot. Browser roster SHALL состоять из одного человека и 1–7 ботов; stationary fixtures сохраняются для явных diagnostic scenarios.

#### Scenario: Боты разных уровней
- **WHEN** валидный browser roster содержит ботов трёх уровней
- **THEN** configuration принимается и сохраняет отдельную сложность каждого

#### Scenario: Неверная сложность
- **WHEN** bot difficulty отсутствует или неизвестна
- **THEN** validation отклоняет configuration со стабильным path без default подмены

#### Scenario: Headless roster
- **WHEN** evaluation harness явно запускает валидные 2–8 bot-only участников
- **THEN** симуляция принимает состав без фиктивного человека, а browser setup продолжает требовать один local seat

## ADDED Requirements

### Requirement: Общий tick управляемых участников
Simulation SHALL обрабатывать actions каждого живого управляемого участника один раз за общий tick, затем атомарно применять combat events и проверять окончание матча. Решения AI SHALL получать одно исходное состояние tick; события и collision queries MUST иметь стабильный порядок. AI dead state не создаёт movement/fire, а respawn SHALL очищать память прежней жизни и выбирать свободный arena spawn по общей policy.

#### Scenario: Взаимный lethal fire
- **WHEN** два живых на combat stage участника делают допустимые смертельные выстрелы в одном tick
- **THEN** оба выстрела разрешаются из единого combat state, обе смерти учитываются до определения победителя

#### Scenario: Пауза AI
- **WHEN** browser находится в pause, hidden либо finished state
- **THEN** AI timers, RNG, actions и simulation state не продвигаются

#### Scenario: Возрождение бота
- **WHEN** завершён death delay
- **THEN** бот получает новую life и валидный свободный arena slot, сохраняя сложность и statistics и сбрасывая устаревшую боевую память
