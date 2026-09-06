# game-design-profile-core Specification

## Purpose

Capability определяет единый versioned контракт балансных данных и метаданных, чтобы каждый следующий игровой параметр одновременно становился валидируемым полем будущей Game Design Lab и воспроизводимой частью матча.

## Requirements

### Requirement: Полный versioned Game Design Profile
Система SHALL представлять активный `GameDesignProfile` как полный сериализуемый snapshot с identity, schema version, revision, source и content hash, а не как набор частичных overrides.

#### Scenario: Загрузка поставляемого prototype profile
- **WHEN** runtime запускается без локально выбранной ревизии
- **THEN** он загружает неизменяемый поставляемый `prototype-v1`, валидирует полный snapshot и показывает его identity и content hash в диагностике

#### Scenario: Отсутствующее обязательное поле
- **WHEN** profile snapshot не содержит обязательного поля текущей schema version
- **THEN** profile validation завершается ошибкой до запуска симуляции и сообщает стабильный path отсутствующего поля

### Requirement: Единый descriptor registry
Каждое числовое балансируемое поле MUST иметь ровно один descriptor со стабильным path, группой, подписью, описанием влияния, единицей измерения, жёсткими minimum/maximum и шагом ввода; UI и range validation MUST использовать тот же registry.

#### Scenario: Полное покрытие числовых полей
- **WHEN** выполняется автоматическая проверка schema и descriptor registry
- **THEN** каждое числовое балансируемое поле имеет один descriptor, а orphan и duplicate paths отсутствуют

#### Scenario: Значение вне диапазона
- **WHEN** числовое значение меньше minimum, больше maximum или не соответствует допустимому шагу
- **THEN** validation отклоняет профиль с ошибкой, содержащей стабильный path и допустимый диапазон

### Requirement: Cross-field validation
Profile validation SHALL проверять не только отдельные диапазоны, но и именованные отношения полей, необходимые для непротиворечивой конфигурации.

#### Scenario: Нарушено отношение полей
- **WHEN** профиль проходит range validation, но нарушает зарегистрированный cross-field invariant
- **THEN** профиль отклоняется с детерминированным кодом ошибки и paths всех связанных полей

### Requirement: Детерминированная identity содержимого
Система MUST вычислять content hash из canonical serialization полного эффективного профиля; порядок ключей объекта или форматирование файла MUST NOT менять hash.

#### Scenario: Эквивалентные snapshots
- **WHEN** два profile snapshot содержат одинаковые effective values и identity-поля, но их JSON-ключи перечислены в разном порядке
- **THEN** canonical serialization и content hash совпадают

#### Scenario: Изменено значение
- **WHEN** меняется любое effective profile value
- **THEN** content hash изменяется

### Requirement: Неизменяемый shipped profile
Поставляемый `prototype-v1` SHALL быть доступен только для чтения; будущий editor MUST создавать пользовательскую ревизию вместо изменения shipped snapshot.

#### Scenario: Попытка изменения shipped snapshot
- **WHEN** вызывающий код пытается применить mutation к загруженному `prototype-v1`
- **THEN** исходный snapshot остаётся неизменным, а изменение возможно только через создание нового полного snapshot

### Requirement: Независимые immutable profile histories и release catalog
Profile subsystem SHALL хранить локально созданные полные валидные `GameDesignProfile` revisions в отдельной append-only history для каждого stable profile id. Номер revision MUST инкрементироваться только внутри собственного profile id; отдельную сохранённую revision MUST NOT изменять или удалять. Checked-in `balance/releases.json` SHALL хранить опубликованные exact snapshots и единственный `releaseRef`; releaseRef MUST указывать на snapshot из этого каталога.

#### Scenario: Версии двух профилей независимы
- **WHEN** дизайнер создаёт revision для профиля A после сохранения профиля B
- **THEN** следующая revision получает номер относительно истории A, а history B и её revision numbers не меняются

#### Scenario: Продолжение поставляемого профиля
- **WHEN** дизайнер сохраняет valid draft, созданный от поставляемой revision `prototype-v1 v6`
- **THEN** исходный snapshot остаётся неизменным, а новая local revision получает тот же profile id `prototype-v1` и следующий номер `v7`

#### Scenario: Удаление нерелизного профиля
- **WHEN** дизайнер удаляет local profile, не содержащий current release revision
- **THEN** удаляется вся local history этого profile id, отдельные revisions не получают самостоятельного delete action, а selected profile возвращается к current release при необходимости

#### Scenario: Публикация release revision
- **WHEN** дизайнер на dev-стенде помечает valid saved revision как релизную
- **THEN** endpoint атомарно сохраняет exact immutable snapshot в `balance/releases.json` и переключает единственный `releaseRef` на эту identity

#### Scenario: Production publication недоступна
- **WHEN** лаборатория открыта в production build
- **THEN** она отображает repository release catalog read-only и не предлагает записать release в файловую систему

### Requirement: Startup selection exact revision
Profile subsystem SHALL выдавать только exact complete snapshot по выбранной identity/hash. Выбор local revision MUST быть проверен до нового match startup; при отсутствии явного local selection новый матч MUST использовать repository `releaseRef`. Отсутствие, повреждение либо hash mismatch selected/release revision MUST отклонять старт без fallback на другой профиль.

#### Scenario: Выбор сохранённой revision
- **WHEN** меню передаёт сохранённую local profile identity для нового матча
- **THEN** loader возвращает exact immutable snapshot с совпадающим content hash и runtime использует его во всех profile consumers

#### Scenario: Повреждённая revision
- **WHEN** выбранная local либо repository release revision отсутствует, не проходит validation либо не совпадает с ожидаемым hash
- **THEN** новый матч не стартует, UI получает actionable validation error, а shipped profile не подставляется молча

### Requirement: Технические инварианты вне профиля
Значения, признанные техническими инвариантами runtime, MUST быть явно перечислены с обоснованием и MUST NOT появляться как редактируемые descriptors.

#### Scenario: Аудит технического инварианта
- **WHEN** автоматическая проверка сопоставляет список runtime invariants с descriptor registry
- **THEN** ни один технический инвариант не доступен как поле Game Design Lab, а каждый имеет краткое обоснование рядом с определением

### Requirement: Полный профиль первого playable combat slice
`prototype-v1` SHALL содержать обязательные числовые параметры capsule, ground/air movement, jump/gravity, first-person camera и double-barrel shotgun pellets/spread/range/cadence; runtime MUST потреблять эти значения из валидированного profile snapshot без дублирующих gameplay constants.

#### Scenario: Полное descriptor coverage
- **WHEN** schema `prototype-v1` расширена параметрами playable slice
- **THEN** каждое новое числовое поле имеет ровно один descriptor с path, группой, подписью, описанием, единицей, minimum, maximum и step

#### Scenario: Shipped defaults
- **WHEN** runtime запускает первый playable slice
- **THEN** movement, camera, capsule и shotgun используют значения одной immutable shipped revision и её content hash

#### Scenario: Повреждённый profile
- **WHEN** отсутствует новое обязательное поле, значение вне range либо нарушено cross-field отношение capsule/camera/weapon
- **THEN** validation останавливает gameplay startup со стабильным code и paths до первого gameplay tick

#### Scenario: Replay compatibility
- **WHEN** replay или snapshot создан с другой revision либо content hash combat profile
- **THEN** runtime отклоняет его до применения movement или shot action frame

### Requirement: Отдельный presentation-профиль производительности
Browser presentation SHALL использовать immutable named profile с числовыми параметрами backing-buffer pixel budget и maximum HUD publication cadence. Каждый параметр MUST иметь стабильный path, группу, подпись, описание влияния, единицу, minimum, maximum и step; profile identity MUST быть отделена от gameplay profile и MUST NOT входить в simulation snapshot, replay или state hash.

#### Scenario: Descriptor coverage presentation-параметров
- **WHEN** shipped presentation profile загружается для browser runtime
- **THEN** pixel budget и HUD cadence имеют ровно по одному descriptor и проходят общую range/step validation до создания WebGL renderer

#### Scenario: Один матч с разными presentation-профилями
- **WHEN** два runner получают одинаковые gameplay snapshot/actions, но renderer использует разные валидные presentation profiles
- **THEN** их per-tick simulation snapshots, replay data и state hashes совпадают

#### Scenario: Повреждённый presentation-профиль
- **WHEN** обязательный parameter отсутствует, имеет неверный тип, выходит за range или не соответствует step
- **THEN** browser startup завершается стабильной validation error до первого gameplay tick и WebGL submission

### Requirement: Полный профиль basic match loop
`prototype-v1` SHALL содержать числовые параметры duration bounds/default, score-target bounds/default/step, assist window/points, kill-chain gap/cumulative totals/post-five increment и death/respawn timing. Каждое поле MUST иметь ровно один descriptor со стабильным path, группой, подписью, описанием, unit, hard minimum/maximum и input step; match configuration validation MUST использовать эти же metadata.

#### Scenario: Score target descriptors
- **WHEN** shipped profile загружен
- **THEN** target default равен 3000, configuration minimum 1000, maximum 20 000 и step 100 доступны через registry, а nullable match target по умолчанию остаётся выключенной настройкой configuration

#### Scenario: Неверная цель
- **WHEN** configuration включает target ниже 1000, выше 20 000 либо не кратный шагу 100 от minimum
- **THEN** validation отклоняет configuration до первого tick со stable path цели

#### Scenario: Полное match coverage
- **WHEN** schema и descriptor registry аудируются автоматически
- **THEN** все числовые match/scoring/death/respawn fields имеют один descriptor, orphan и duplicate paths отсутствуют

#### Scenario: Cross-field ranges
- **WHEN** minimum/default/maximum duration или target нарушают порядок либо kill-chain totals не возрастают
- **THEN** profile validation возвращает deterministic cross-field code и все связанные paths

### Requirement: Полный профиль procedural arena generator
`prototype-v1` SHALL содержать обязательные числовые generator settings для `small`, `medium` и `large`, включая topology, dimensions, connected-wall architecture, corridor/doorway clearance, perimeter complexity, cover, spawn separation и fairness budgets. Каждое числовое поле MUST иметь ровно один descriptor со стабильным path, группой, подписью, описанием, unit, hard minimum/maximum и step; generator и validator MUST потреблять одну валидированную immutable revision.

#### Scenario: Generator descriptor coverage
- **WHEN** schema и descriptor registry аудируются
- **THEN** все numeric arena-generator leaves трёх presets имеют ровно один descriptor без orphan, duplicate или runtime constants

#### Scenario: Неверный generator profile
- **WHEN** preset нарушает range, step либо cross-field invariant для размеров, routes, cover, slots или fairness
- **THEN** profile validation возвращает deterministic code и связанные paths до generation

#### Scenario: Изменение после generation
- **WHEN** пользователь изменяет generator draft после создания arena
- **THEN** текущая arena и match identity не меняются, а новые значения применяются только к следующей сохранённой revision и следующей generation

### Requirement: Полные профили AI и непрерывного боезапаса
GameDesignProfile SHALL включать полные `rules.bots.easy`, `rules.bots.normal`, `rules.bots.hard`, общие navigation/cooperation параметры и `rules.weapons.doubleBarrelShotgun.emptyRefillAmmo`. Последнее SHALL иметь shipped значение 20 для `continuous-combat-v1`. Каждое числовое поле MUST иметь единый descriptor path/group/label/description/unit/min/max/step и участвовать в profile hash; refill MUST быть положительным целым. Никакие AI overrides MUST NOT менять damage, health, ammo или физическую скорость отдельного уровня.

#### Scenario: Полный профиль
- **WHEN** загружается новая shipped revision
- **THEN** все AI/refill numeric leaves покрыты metadata, а validation проверяет диапазоны и cross-field отношения

#### Scenario: Повреждённое пополнение
- **WHEN** refill равен нулю, отрицательный либо дробный
- **THEN** профиль отклоняется до матча со стабильным path

#### Scenario: Изменение поведения
- **WHEN** меняется AI numeric parameter
- **THEN** изменяется profile identity/hash и старый несовместимый replay отклоняется явно
