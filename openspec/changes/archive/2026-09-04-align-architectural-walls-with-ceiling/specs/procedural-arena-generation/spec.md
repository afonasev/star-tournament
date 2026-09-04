## MODIFIED Requirements

### Requirement: Интересная маршрутная структура
Accepted arena SHALL иметь центральный ориентир, внешний обход, более быстрый открытый маршрут и более длинный защищённый маршрут. Внутренние primary walls MUST образовывать связные архитектурные цепочки, ограничивать комнаты и коридоры и иметь profile-defined полную высоту до потолка; только explicitly semantic buttress/cover obstacles MAY быть ниже. Переходы между регионами MUST быть представлены читаемыми дверными проёмами с profile-defined capsule clearance. Любой визуально открытый промежуток между collision solids MUST либо сохранять полный clearance профильной капсулы, либо быть закрыт непрерывной геометрией; непроходимые узкие щели и ложные маршруты MUST NOT присутствовать в accepted definition. Замкнутый внешний контур MUST использовать size-specific неровный ортогональный силуэт и MUST NOT состоять только из четырёх сторон одного прямоугольника. Каждая gameplay region и spawn region MUST иметь не менее двух выходов, а navigation graph MUST сохранять связь spawn regions при удалении одной link, чтобы один chokepoint не блокировал арену.

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
