## ADDED Requirements

### Requirement: Arena-bound match session
Match session SHALL владеть exact validated `ArenaDefinition`, выбранным size и resolved seed вместе с configuration/profile identities. Initial participant transforms и stationary fixture respawn transforms MUST назначаться из arena spawn slots, а combat scenario SHALL владеть только opponent/hit-volume contract.

#### Scenario: Initial snapshot
- **WHEN** accepted arena запускается с валидным roster
- **THEN** каждый participant получает distinct свободный arena slot, FFA opponents и противостоящие команды удовлетворяют profile-defined minimum separation, союзники могут занимать соседние slots, а local-seat capsule свободна до первого input tick

#### Scenario: Fixture respawn
- **WHEN** stationary participant завершает death delay
- **THEN** simulation детерминированно выбирает допустимый arena slot и создаёт новую life без обращения к scenario position

### Requirement: Живые participants блокируют движение
Перед каждым movement query collision adapter SHALL проецировать из simulation state капсулы всех живых participants. Капсула движущегося participant MUST сталкиваться с другими живыми participant capsules без проталкивания stationary fixtures. Неживой participant MUST быть исключён из collision queries до respawn.

#### Scenario: Контакт с живым participant
- **WHEN** local-seat движется в сторону живого stationary participant
- **THEN** character controller останавливается либо скользит по его капсуле и не проходит сквозь неё

#### Scenario: Смерть и respawn
- **WHEN** participant умирает, а затем получает новую life
- **THEN** его capsule перестаёт блокировать движение после смерти и снова проецируется в respawn position до следующего movement query

### Requirement: Repeat сохраняет arena, новый матч разрешает новый seed
Repeat SHALL повторно использовать exact definition, size, seed, generator/profile identities и configuration. Новый матч с auto seed SHALL разрешать новое concrete значение; новый матч с ручным seed SHALL использовать введённое значение.

#### Scenario: Repeat
- **WHEN** пользователь выбирает Repeat из pause либо results
- **THEN** новый initial snapshot имеет прежнюю arena identity/hash и сброшенные match/life state

#### Scenario: Новый auto-seed матч
- **WHEN** пользователь выходит в setup и снова запускает матч с пустым seed
- **THEN** shell создаёт новый resolved seed и новую arena identity без изменения предыдущего replay
