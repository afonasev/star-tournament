## Purpose

Capability определяет воспроизводимую одноуровневую процедурную арену, её semantic spatial model, режимные validation gates и проекции для simulation, collision, navigation, spawn и renderer.

## ADDED Requirements

### Requirement: Каноническая детерминированная ArenaDefinition
Генератор SHALL из конкретных `seed`, `generatorVersion`, size preset и identity валидированного generator profile создавать одну immutable `ArenaDefinition` с canonical semantic-ID order и content hash. Arena RNG MUST быть отделён от match simulation RNG и завершаться до initial tick.

#### Scenario: Одинаковые входы
- **WHEN** два независимых запуска используют одинаковые seed, generator version, size и profile identity
- **THEN** они создают byte-identical canonical JSON и одинаковый arena content hash

#### Scenario: Изменён generation input
- **WHEN** меняется seed, size, generator version либо generator profile content hash
- **THEN** generation identity и effective arena content hash отражают новый результат

### Requirement: Три содержательных size preset
Generator SHALL поддерживать `small`, `medium` и `large` как разные topology recipes семейства «Разорванное кольцо», а не как равномерное масштабирование координат. Каждый preset MUST быть одноуровневым, поддерживать FFA, teams и до восьми участников; `medium` SHALL быть default.

#### Scenario: Выбран размер
- **WHEN** пользователь запускает один seed отдельно с каждым size preset
- **THEN** definitions сохраняют общие route-choice принципы, но отличаются topology budget, combat regions и arena identity

#### Scenario: Максимальный roster
- **WHEN** любой preset валидируется для roster из восьми участников в FFA и teams
- **THEN** он предоставляет допустимый одновременный placement и проходит mode fairness gates

### Requirement: Единая semantic spatial model
`ArenaDefinition` SHALL описывать regions, links, collision solids, spawn regions/slots и renderer material slots как pure serializable data. Collision static world, navigation graph, spawn catalog и renderer description MUST выводиться только из неё и MUST NOT содержать взаимно независимые spatial transforms.

#### Scenario: Derived projections
- **WHEN** validated definition передаётся каждому adapter
- **THEN** все projections ссылаются на существующие semantic IDs, используют одну arena identity и не требуют `CombatSliceScenario` для positions

#### Scenario: Renderer cadence
- **WHEN** та же definition отображается с другой render cadence либо без renderer
- **THEN** navigation, collision queries, spawn allocation и simulation hashes не меняются

### Requirement: Интересная маршрутная структура
Accepted arena SHALL иметь центральный ориентир, внешний обход, более быстрый открытый маршрут и более длинный защищённый маршрут. Внутренние стены MUST образовывать связные архитектурные цепочки, которые ограничивают комнаты и коридоры, а переходы между ними MUST быть представлены читаемыми дверными проёмами с profile-defined capsule clearance. Любой визуально открытый промежуток между collision solids MUST либо сохранять полный clearance профильной капсулы, либо быть закрыт непрерывной геометрией; непроходимые узкие щели и ложные маршруты MUST NOT присутствовать в accepted definition. Замкнутый внешний контур MUST использовать size-specific неровный ортогональный силуэт и MUST NOT состоять только из четырёх сторон одного прямоугольника. Каждая gameplay region и spawn region MUST иметь не менее двух выходов, а navigation graph MUST сохранять связь spawn regions при удалении одной link, чтобы один chokepoint не блокировал арену.

#### Scenario: Отказ единственной связи
- **WHEN** validator по очереди исключает каждую navigation link
- **THEN** остальные spawn regions остаются взаимно достижимыми

#### Scenario: Route choice
- **WHEN** анализируется путь от spawn region к основной combat region
- **THEN** существует не менее двух различающихся по exposure и длине допустимых маршрутов

#### Scenario: Связная архитектура
- **WHEN** validator анализирует interior wall surfaces accepted arena
- **THEN** каждая основная стеновая цепочка соединена через углы или примыкания, ограничивает заявленную комнату либо коридор и не является случайно изолированным блоком

#### Scenario: Дверной проём и коридор
- **WHEN** participant capsule проходит между связанными gameplay regions
- **THEN** геометрия предоставляет читаемый дверной проём и непрерывный коридор без collision/render разрыва

#### Scenario: Узкий промежуток между solids
- **WHEN** validator находит визуально открытый промежуток между collision surfaces уже полного диаметра профильной капсулы
- **THEN** definition отклоняется как содержащая непроходимую щель; constructive generator вместо неё создаёт непрерывный solid

#### Scenario: Неровный внешний контур
- **WHEN** сравниваются perimeter surfaces и bounding rectangle arena
- **THEN** периметр остаётся замкнутым, но содержит size-specific уступы или ниши и не совпадает с четырьмя сторонами bounding rectangle

### Requirement: Spawn safety и distinct-slot allocation
Каждая accepted arena MUST содержать минимум 12 отдельных spawn slots, среди которых validator находит восемь одновременно допустимых для profile-defined capsule. Initial FFA allocation и initial allocations противостоящих команд MUST соблюдать profile-defined minimum opponent separation; союзная группа MAY появляться рядом и в LOS друг друга. Одновременный respawn SHALL детерминированно назначать участникам разные slots, и участники MUST NOT делить одну position.

#### Scenario: Восемь участников на small
- **WHEN** small arena создаёт initial FFA placement для roster из восьми участников
- **THEN** allocator выбирает spatially spread distinct slots, удовлетворяющие profile-defined minimum opponent separation

#### Scenario: Одновременное появление команды
- **WHEN** несколько союзников respawn на одном tick и выбранная spawn region имеет достаточно slots
- **THEN** они получают соседние distinct slots в стабильном participant-ID порядке

#### Scenario: В region не хватает slots
- **WHEN** безопасная region не вмещает весь batch
- **THEN** остаток детерминированно распределяется по следующим совместимым regions без position overlap

#### Scenario: Static или participant overlap
- **WHEN** slot пересекает static geometry либо живую participant capsule
- **THEN** slot исключается до allocation без mutation simulation state

### Requirement: Проходимость и режимная честность
До старта матча validator MUST проверить profile-defined capsule clearance, region connectivity, spawn placement, FFA fairness и team fairness. Геометрическая симметрия MUST NOT быть обязательной; fairness SHALL оцениваться versioned navigation-distance, route-redundancy и enemy-LOS metrics.

#### Scenario: Асимметричная честная arena
- **WHEN** geometry не зеркальна, но обе team allocations и FFA spawn sets удовлетворяют versioned metrics
- **THEN** arena принимается для обоих режимов

#### Scenario: Непроходимая или нечестная arena
- **WHEN** отсутствует capsule route, безопасный roster placement либо одна сторона систематически получает недопустимое преимущество
- **THEN** generation завершается стабильным validation report и матч не получает initial snapshot

### Requirement: Явный generation failure и fallback
Generation SHALL иметь bounded deterministic attempts. После исчерпания attempts runtime MUST показать seed и стабильные причины; shipped fallback arena MAY запускаться только после явного действия пользователя и MUST иметь собственные identity и content hash.

#### Scenario: Все attempts отклонены
- **WHEN** generator не создаёт accepted arena в установленный bound
- **THEN** gameplay не стартует, UI предлагает retry, новый seed либо явный fallback

#### Scenario: Выбран fallback
- **WHEN** пользователь явно запускает fallback arena
- **THEN** snapshot, replay, diagnostics и renderer показывают fallback identity, не маскируя её исходным seed

### Requirement: Seed-corpus и browser acceptance
Procedural arena change MUST проходить property-based проверку versioned seed corpus для каждого size/mode, deterministic replay/collision reconstruction, production-browser performance gate и физический in-app Browser playtest с выключенным звуком. Формальная валидность MUST NOT считаться доказательством читаемости или интересности.

#### Scenario: Автоматический corpus
- **WHEN** versioned corpus генерируется повторно
- **THEN** каждый accepted seed воспроизводит definition/hash, проходит validators и не меняет результаты collision reconstruction

#### Scenario: Игровая проверка representative seeds
- **WHEN** tester физически проходит representative small, medium и large arenas
- **THEN** evidence подтверждает свободный initial spawn, доступность critical routes, отсутствие collision/render divergence, console errors и недопустимого performance regression
