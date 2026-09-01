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
