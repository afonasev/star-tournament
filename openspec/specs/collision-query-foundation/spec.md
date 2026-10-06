## Purpose

Capability задаёт проверяемый контракт collision/query backend, который обслуживает до восьми участников одной детерминированной симуляции, восстанавливается из сериализуемого состояния и не зависит от renderer, viewport или физических устройств.

## Requirements

### Requirement: Versioned collision compatibility identity
Collision foundation SHALL иметь versioned identity, включающую semantic contract version, backend package identity и exact backend version; replay и checkpoint MUST отклоняться до выполнения, если их collision identity несовместима с runtime.

#### Scenario: Совместимая identity
- **WHEN** два runner используют одинаковые contract version, backend identity, arena identity и профиль fixture
- **THEN** они создают collision world с одинаковой compatibility identity

#### Scenario: Несовместимый replay
- **WHEN** replay или checkpoint содержит другую collision contract либо backend version
- **THEN** runtime сообщает детерминированную compatibility error до первого simulation tick

### Requirement: Collision state без скрытого authority
Сериализуемый simulation snapshot MUST содержать либо достаточный collision checkpoint, либо все данные для полной детерминированной реконструкции collision world; mutable backend state MUST NOT оставаться незахешированным источником gameplay-результата.

#### Scenario: Восстановление checkpoint
- **WHEN** workload продолжается после serialize/parse поддерживаемого checkpoint
- **THEN** каждый последующий gameplay hash и collision checkpoint hash совпадает с непрерывным запуском

#### Scenario: Независимая реконструкция
- **WHEN** два collision world независимо создаются из одного snapshot, arena и compatibility identity
- **THEN** одинаковая последовательность команд даёт одинаковые ordered query results и итоговые hashes

### Requirement: Канонический static world из ArenaDefinition
Static collision geometry SHALL выводиться только из валидированного `ArenaDefinition` в стабильном semantic-ID порядке, включая geometry крупных collidable architectural panels, и MUST NOT зависеть от Three.js objects, DOM или renderer transforms.

#### Scenario: Изменён порядок descriptors
- **WHEN** эквивалентные arena descriptors перечислены в разном порядке
- **THEN** canonical construction создаёт одинаковую collision identity и одинаковые результаты fixture queries

#### Scenario: Renderer не влияет на collision
- **WHEN** renderer отсутствует, работает с другой cadence либо отображает один, два или четыре viewport
- **THEN** collision world, ordered query results и simulation hashes не меняются

#### Scenario: Крупный panel proxy
- **WHEN** один и тот же canonical wall surface содержит collidable panel thickness
- **THEN** independently reconstructed worlds блокируют capsule одинаково и дают равные checkpoint hashes

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

### Requirement: Канонические ray и shape queries
Ray/shape queries MUST возвращать только сериализуемые результаты, применять явные filters и стабильно сортировать равные кандидаты по semantic ID; ближайшее допустимое projectile-blocking препятствие SHALL перекрывать более дальний hit. Movement capsule query MUST использовать movement-blocking filter отдельно от projectile occlusion, так что canonical movement-only barriers блокируют capsule, но не попадают в hitscan ray result.

#### Scenario: Hitscan occlusion
- **WHEN** ray пересекает arena cover раньше диагностической цели
- **THEN** query возвращает projectile-blocking cover как ближайший hit, и дальняя цель считается перекрытой

#### Scenario: Movement-only barrier
- **WHEN** ray и movement capsule одновременно пересекают canonical movement-only barrier
- **THEN** ray не возвращает barrier как occluder, а capsule query возвращает stable blocking contact

#### Scenario: Равная дистанция
- **WHEN** несколько допустимых hits имеют одинаковую distance в пределах versioned epsilon
- **THEN** результат выбирается по документированному semantic-ID tie-break и одинаков во всех повторных запусках

### Requirement: Управляемые initialization и lifecycle
WASM/backend initialization SHALL выполняться один раз до запуска collision ticks, иметь наблюдаемые ready/error состояния и освобождать каждый созданный world ровно один раз.

#### Scenario: Ошибка initialization
- **WHEN** backend не может инициализироваться
- **THEN** browser shell показывает понятную collision initialization error и не запускает частичную симуляцию

#### Scenario: Повторный lifecycle
- **WHEN** diagnostic workload многократно создаёт, уничтожает и снова создаёт world
- **THEN** active worlds, bodies, colliders и controllers возвращаются к baseline без последующих ticks или роста ресурсов

### Requirement: Воспроизводимый determinism workload
Spike MUST выполнить versioned workload восьми participants в течение 10 000 ticks с canonical create/remove/query order и доказать одинаковые gameplay hashes и checkpoint hashes в независимых запусках на reference host.

#### Scenario: Два независимых запуска
- **WHEN** workload дважды запускается с одинаковыми seed, arena, identity и командами
- **THEN** hashes совпадают на каждом versioned checkpoint и на финальном tick

#### Scenario: Разная render cadence
- **WHEN** тот же workload наблюдается с разной render cadence и количеством viewport
- **THEN** ни один gameplay или checkpoint hash не меняется

### Requirement: Измеримые prototype go/no-go gates
Candidate SHALL считаться `go` для локального прототипа только при прохождении functional, determinism, lifecycle, build и browser gates, а reference browser workload MUST показать p95 не более 2.5 ms/tick, p99 не более 4.2 ms/tick и ни одного tick более 8.3 ms; bundle evidence SHALL отдельно фиксировать compressed lazy chunk и init latency.

#### Scenario: Все prototype gates пройдены
- **WHEN** strict checks, headless replay, browser fixtures, lifecycle и reference benchmark удовлетворяют порогам
- **THEN** evidence фиксирует provisional `go`, exact compatibility identity и ограничения подтверждённой среды

#### Scenario: Любой обязательный gate не пройден
- **WHEN** хотя бы один functional, determinism, lifecycle, build или performance gate нарушен
- **THEN** candidate получает `no-go`, игровой movement change не связывается с ним, а evidence сохраняет причину и измерения

#### Scenario: Нет cross-platform evidence
- **WHEN** hashes доказаны только на одном reference host и Chromium
- **THEN** результат MUST NOT заявлять сетевую или cross-platform совместимость, а проверка на другой ОС/браузере остаётся обязательным будущим gate
