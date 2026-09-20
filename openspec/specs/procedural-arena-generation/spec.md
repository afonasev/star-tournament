# procedural-arena-generation Specification

## Purpose

Capability определяет воспроизводимую одноуровневую процедурную арену, её semantic spatial model, режимные validation gates и проекции для simulation, collision, navigation, spawn и renderer.

## Requirements

### Requirement: Каноническая детерминированная ArenaDefinition
Генератор SHALL из конкретных `seed`, `generatorVersion`, size preset и identity валидированного generator profile создавать одну immutable `ArenaDefinition` с canonical semantic-ID order и content hash. Arena RNG MUST быть отделён от match simulation RNG и завершаться до initial tick. Для native registered arena family explicit selected family, canonical profile identity и accepted definition SHALL образовывать frozen hand-off snapshot: повторная generation с теми же входами MUST возвращать ту же arena identity, а derived validator/projection/navigation consumers MUST получать именно этот immutable snapshot без повторного выбора family или подмены profile.

#### Scenario: Одинаковые входы
- **WHEN** два независимых запуска используют одинаковые seed, generator version, size и profile identity
- **THEN** они создают byte-identical canonical JSON и одинаковый arena content hash

#### Scenario: Изменён generation input
- **WHEN** меняется seed, size, generator version либо generator profile content hash
- **THEN** generation identity и effective arena content hash отражают новый результат

#### Scenario: Native family freeze
- **WHEN** два native запуска используют одинаковые seed, generator version, canonical profile identity и явно выбранный registered family
- **THEN** они создают definitions с одной arena identity, а projection и navigation получают тот же accepted frozen snapshot

### Requirement: Три содержательных size preset
Generator SHALL поддерживать `small`, `medium` и `large` как разные topology budgets набора именованных layout recipes, а не как равномерное масштабирование координат либо вариации одного «Разорванного кольца». Каждый preset MUST выбирать воспроизводимый recipe из seed, поддерживать FFA, teams и до восьми участников; `medium` SHALL быть default. Accepted recipe MUST включать читаемые большие rooms, corridors, route loops либо courtyards в соответствии со своим composition contract и MUST выбирать ровно одну выраженную spatial role: широкий зал с длинным обходом, верхняя обходная галерея, открытые балконы, две самостоятельные боевые арены по ярусам либо основной этаж с подвальным обходом.

#### Scenario: Выбран размер
- **WHEN** пользователь запускает один seed отдельно с каждым size preset
- **THEN** definitions сохраняют общие route-choice принципы, но отличаются topology budget, combat regions, composition и arena identity

#### Scenario: Максимальный roster
- **WHEN** любой preset валидируется для roster из восьми участников в FFA и teams
- **THEN** он предоставляет допустимый одновременный placement и проходит mode fairness gates

#### Scenario: Пять семейств
- **WHEN** versioned seed corpus покрывает recipes
- **THEN** каждое из пяти spatial families встречается хотя бы раз и не объединяется в одну обязательную трёхъярусную карту

### Requirement: Единая semantic spatial model
`ArenaDefinition` SHALL описывать regions, links, collision solids, spawn regions/slots и renderer material slots как pure serializable data. Collision static world, navigation graph, spawn catalog и renderer description MUST выводиться только из неё и MUST NOT содержать взаимно независимые spatial transforms.

#### Scenario: Derived projections
- **WHEN** validated definition передаётся каждому adapter
- **THEN** все projections ссылаются на существующие semantic IDs, используют одну arena identity и не требуют `CombatSliceScenario` для positions

#### Scenario: Renderer cadence
- **WHEN** та же definition отображается с другой render cadence либо без renderer
- **THEN** navigation, collision queries, spawn allocation и simulation hashes не меняются

### Requirement: Интересная маршрутная структура
Accepted arena SHALL иметь центральный ориентир, внешний обход, более быстрый открытый маршрут и более длинный защищённый маршрут. Внутренние primary walls MUST образовывать связные архитектурные цепочки, ограничивать комнаты и коридоры и иметь profile-defined полную высоту до потолка; только explicitly semantic barriers MAY быть ниже. Multi-level recipe MUST иметь минимум два пространственно различных двусторонних перехода между связанными ярусами; никакая визуально достижимая зона не может зависеть от jump-only, cliff или false upper route. Любой combat tunnel, doorway, ramp и stairs MUST сохранять чистую ширину не менее трёх effective participant capsule diameters во всех сечениях и поворотах. Каждая gameplay region и spawn region MUST иметь не менее двух выходов, а navigation graph MUST сохранять связь spawn regions при удалении одной link.
Любой визуально открытый промежуток между collision solids MUST либо сохранять полный clearance профильной капсулы, либо быть закрыт непрерывной геометрией; непроходимые узкие щели и ложные маршруты MUST NOT присутствовать в accepted definition. Замкнутый внешний контур MUST использовать size-specific неровный ортогональный силуэт и MUST NOT состоять только из четырёх сторон одного прямоугольника.

#### Scenario: Тоннель для стрейфа
- **WHEN** validator проверяет тоннель, doorway или рампу
- **THEN** три боковых capsule lanes и corner sweep проходят без пересечения canonical geometry

#### Scenario: Рампа между уровнями
- **WHEN** route проходит между declared support layers
- **THEN** validator и physical collision подтверждают capsule traversal в обоих направлениях без принудительного прыжка

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

### Requirement: Semantic barriers, windows и wall-relief niches
ArenaDefinition SHALL явно классифицировать полную стену, непростреливаемое полупрозрачное окно, movement-only barrier и простреливаемую wall-relief niche. Window MUST оставаться частью непроходимого и непростреливаемого wall shell. Barrier MUST блокировать только movement capsule и MUST NOT участвовать в projectile occlusion. Wall-relief niche MUST быть presentation-only неглубоким рельефом стены, MUST NOT создавать доступную participant capsule область и MUST NOT становиться полной hiding place.

#### Scenario: Полупрозрачное окно
- **WHEN** hitscan ray и participant capsule направлены в окно
- **THEN** оба получают блокировку на canonical wall boundary, а renderer сохраняет полупрозрачный вид без изменения collision identity

#### Scenario: Барьер с прострелом
- **WHEN** participant пытается пройти через barrier, а hitscan ray проходит через его volume
- **THEN** movement query блокирует capsule, а ray продолжает поиск цели за barrier

#### Scenario: Ниша в стене
- **WHEN** renderer отображает wall-relief niche
- **THEN** participant capsule не может войти в её объём, а ray не получает artificial occluder от presentation detail

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
До старта матча validator MUST проверить profile-defined body capsule, region connectivity, spawn placement, FFA fairness и team fairness по canonical surfaces, включающим collision thickness крупных architectural panels. Мелкие presentation-only details MUST NOT входить в validation. Геометрическая симметрия MUST NOT быть обязательной; fairness SHALL оцениваться versioned navigation-distance, route-redundancy и enemy-LOS metrics.

#### Scenario: Асимметричная честная arena
- **WHEN** geometry не зеркальна, но обе team allocations и FFA spawn sets удовлетворяют versioned metrics
- **THEN** arena принимается для обоих режимов

#### Scenario: Непроходимая или нечестная arena
- **WHEN** отсутствует route, безопасный roster placement либо одна сторона систематически получает недопустимое преимущество для effective player clearance
- **THEN** generation завершается стабильным validation report и матч не получает initial snapshot

#### Scenario: Панель закрывает маршрут
- **WHEN** collidable panel уменьшает route ниже effective player clearance либо исключает required spawn placement
- **THEN** definition отклоняется до initial snapshot со стабильным validation report

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

### Requirement: Структурно разные закрытые этажи
Generator SHALL сначала выбирать граф помещений и проёмов из ограниченного каталога структурных вариантов, затем компилировать canonical surfaces. Закрытые самостоятельные этажи MUST преобладать в profile-owned распределении карт; открытые галереи, балконы и атриумы MUST оставаться доступными. Закрытый верхний этаж MUST иметь собственную сеть комнат и коридоров, стены и потолок, полезную боевую площадь по профилю и минимум два разнесённых перехода. Общая открытая платформа MUST NOT удовлетворять этому контракту.

#### Scenario: Разные seeds одного семейства
- **WHEN** проверяется фиксированный corpus одного family и size
- **THEN** он содержит разные графы помещений, расположения переходов и распределения высот; зеркала, повороты и разные hashes сами по себе не доказывают разнообразие

#### Scenario: Преобладание закрытых этажей
- **WHEN** проверяется versioned corpus распределения release profile
- **THEN** более половины карт содержат самостоятельный закрытый этаж, а corpus также содержит открытые композиции

#### Scenario: Самостоятельный верхний этаж
- **WHEN** игрок поднимается на закрытый верхний этаж
- **THEN** он может перемещаться между несколькими помещениями и выходами под собственным потолком, не попадая в общий объём нижнего зала вне объявленных проёмов

### Requirement: Настоящий тоннельный обход
Тоннель SHALL соединять различные входы и иметь боковые стены и непрерывный canonical ceiling по всей длине. Пространство под открытой плитой без этих границ MUST NOT учитываться как тоннель.

#### Scenario: Закрытый обход
- **WHEN** проверяется declared tunnel route
- **THEN** его стены и потолок непрерывны, входы различны, и три боковых capsule lanes проходят все сечения и повороты

### Requirement: Смешанные лестницы и рампы
Каждый переход SHALL детерминированно выбирать ramp либо stairs по seed и stable transition ID. Профильная вероятность stairs MUST по умолчанию давать примерно половину переходов; все три сочетания двух переходов MUST встречаться в corpus.

#### Scenario: Повторная генерация
- **WHEN** одинаковые seed, size и profile генерируются повторно
- **THEN** типы переходов и геометрия ступеней совпадают, а RNG матча не используется
