## MODIFIED Requirements

### Requirement: Три содержательных size preset
Generator SHALL поддерживать `small`, `medium` и `large` как разные topology budgets набора именованных layout recipes, а не как равномерное масштабирование координат либо вариации одного «Разорванного кольца». Каждый preset MUST выбирать воспроизводимый recipe из seed, поддерживать FFA, teams и до восьми участников; `medium` SHALL быть default. Accepted recipe MUST включать читаемые большие rooms, corridors, route loops либо courtyards в соответствии со своим composition contract и MAY включать малые elevation zones, соединённые валидными ramps.

#### Scenario: Выбран размер
- **WHEN** пользователь запускает один seed отдельно с каждым size preset
- **THEN** definitions сохраняют общие route-choice принципы, но отличаются topology budget, combat regions, composition и arena identity

#### Scenario: Максимальный roster
- **WHEN** любой preset валидируется для roster из восьми участников в FFA и teams
- **THEN** он предоставляет допустимый одновременный placement и проходит mode fairness gates

### Requirement: Интересная маршрутная структура
Accepted arena SHALL иметь центральный ориентир, внешний обход, более быстрый открытый маршрут и более длинный защищённый маршрут. Внутренние primary walls MUST образовывать связные архитектурные цепочки, ограничивать комнаты и коридоры и иметь profile-defined полную высоту до потолка; только explicitly semantic barriers MAY быть ниже. Переходы между регионами MUST быть представлены читаемыми дверными проёмами с profile-defined capsule clearance. Малые высотные зоны MUST быть соединены только валидируемыми широкими ramps; визуально достижимая зона MUST иметь capsule route, а stair-step, cliff или false upper route MUST NOT присутствовать в accepted definition. Любой визуально открытый промежуток между collision solids MUST либо сохранять полный clearance профильной капсулы, либо быть закрыт непрерывной геометрией; непроходимые узкие щели и ложные маршруты MUST NOT присутствовать в accepted definition. Замкнутый внешний контур MUST использовать size-specific неровный ортогональный силуэт и MUST NOT состоять только из четырёх сторон одного прямоугольника. Каждая gameplay region и spawn region MUST иметь не менее двух выходов, а navigation graph MUST сохранять связь spawn regions при удалении одной link, чтобы один chokepoint не блокировал арену.

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

#### Scenario: Рампа между уровнями
- **WHEN** route проходит из нижней в верхнюю elevation zone
- **THEN** validator на canonical collision geometry подтверждает capsule traversal в обоих направлениях без принудительного прыжка

#### Scenario: Узкий промежуток между solids
- **WHEN** validator находит визуально открытый промежуток между collision surfaces уже полного диаметра профильной капсулы
- **THEN** definition отклоняется как содержащая непроходимую щель; constructive generator вместо неё создаёт непрерывный solid

#### Scenario: Неровный внешний контур
- **WHEN** сравниваются perimeter surfaces и bounding rectangle arena
- **THEN** периметр остаётся замкнутым, но содержит size-specific уступы или ниши и не совпадает с четырьмя сторонами bounding rectangle

## ADDED Requirements

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
