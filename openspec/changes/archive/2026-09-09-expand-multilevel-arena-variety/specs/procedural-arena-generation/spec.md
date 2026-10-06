## ADDED Requirements

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

## MODIFIED Requirements

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

### Requirement: Интересная маршрутная структура
Accepted arena SHALL иметь центральный ориентир, внешний обход, более быстрый открытый маршрут и более длинный защищённый маршрут. Внутренние primary walls MUST образовывать связные архитектурные цепочки, ограничивать комнаты и коридоры и иметь profile-defined полную высоту до потолка; только explicitly semantic barriers MAY быть ниже. Multi-level recipe MUST иметь минимум два пространственно различных двусторонних перехода между связанными ярусами; никакая визуально достижимая зона не может зависеть от jump-only, cliff или false upper route. Любой combat tunnel, doorway, ramp и stairs MUST сохранять чистую ширину не менее трёх effective participant capsule diameters во всех сечениях и поворотах. Каждая gameplay region и spawn region MUST иметь не менее двух выходов, а navigation graph MUST сохранять связь spawn regions при удалении одной link.

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

## ADDED Requirements

### Requirement: Смешанные лестницы и рампы
Каждый переход SHALL детерминированно выбирать ramp либо stairs по seed и stable transition ID. Профильная вероятность stairs MUST по умолчанию давать примерно половину переходов; все три сочетания двух переходов MUST встречаться в corpus.

#### Scenario: Повторная генерация
- **WHEN** одинаковые seed, size и profile генерируются повторно
- **THEN** типы переходов и геометрия ступеней совпадают, а RNG матча не используется
