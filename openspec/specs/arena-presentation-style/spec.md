# arena-presentation-style Specification

## Purpose

Capability задаёт наблюдаемый visual language procedural arena: material kit, освещение, route accents и контраст участников, не влияющие на deterministic simulation.

## Requirements

### Requirement: Clean Future Sport material kit
Arena renderer SHALL представлять validated `ArenaDefinition` в стиле `clean-future-sport-v1`: versioned `clean-future-sport-glb-v1` architectural kit со светлыми off-white/pale-gray wall bays и buttresses, крупными двухцветными curved portal facades на существующих doorway gaps, тёмным navy-composite floor с широкими segmented route guides и cyan/lime/orange accents для route, doorway и landmark surfaces. Модули MUST разрешаться только через GLB asset manifest. Крупные wall bays и portal facades MUST совпадать с canonical collision shell wall surfaces; мелкие trims, seams, decals, floor route guides и landmark MUST оставаться presentation-only. `clean-future-sport-texture-v1` SHALL использовать бесшовные повторяемые PBR material sets для wall/floor surfaces и atlas-backed decals только для уникальных route markings, sector labels и мелких декоративных знаков. Базовый material contract MUST содержать base color, normal и ORM maps; presentation detail-tier может добавлять detail-normal и decal layers. Материалы, texture variants, decals и detail meshes MUST выводиться только из существующих semantic surface slots, Renderer MUST NOT создавать независимые blocking volumes и они MUST NOT менять collision, navigation, spawn или arena content hash.

#### Scenario: Совпадение крупной панели и collision
- **WHEN** крупная panel bay видна на route wall
- **THEN** её outer boundary совпадает с canonical collision shell, а capsule не может пройти сквозь неё

#### Scenario: Одна spatial identity при смене стиля
- **WHEN** тот же validated ArenaDefinition отображается с GLB style kit либо без renderer
- **THEN** все simulation, collision, navigation, spawn и replay identities остаются одинаковыми

#### Scenario: Навигационный акцент
- **WHEN** игрок смотрит на doorway, маршрут или центральный landmark
- **THEN** соответствующий GLB architectural accent читается отдельно от базового пола и shell без назначения ему team или participant color

#### Scenario: Детальный слой не меняет игровое пространство
- **WHEN** renderer добавляет GLB wall bays, buttresses, route guides, curved portals, seams, frames, trims, верхний central landmark, textures либо decals
- **THEN** они остаются presentation-only, не образуют ложных doorways/cover и не участвуют в collision, navigation, spawn или arena hashing

#### Scenario: Округлый портал следует реальному doorway
- **WHEN** два collinear wall segments оставляют разрешённый doorway gap
- **THEN** renderer может расположить GLB portal facade только вокруг этого gap, но MUST NOT добавить его на непрерывную wall surface

#### Scenario: Силуэт читается с игровой дистанции
- **WHEN** игрок видит wall либо doorway на representative gameplay distance
- **THEN** крупные GLB panel modules, two-tone portal shell и floor guide различимы как архитектурный ритм, а не как набор тонких технических линий

### Requirement: Shipping texture variants
Renderer SHALL выбирать GPU-compressed shipping texture variants исключительно по active graphics-quality profile: повторяемые material maps authorятся не выше 2K, unique landmark и atlas maps — не выше 4K, а texture residency MUST оставаться в budget profile 128 MiB (`Low`), 256 MiB (`Balanced`), 384 MiB (`High`) либо 512 MiB (`Ultra`). Загрузка, cache, UV/sampler state и quality selection MUST NOT входить в `ArenaDefinition`, collision, navigation, spawn, snapshot, replay, RNG или state hash.

#### Scenario: Повышение visual detail
- **WHEN** пользователь выбирает более высокий graphics-quality profile
- **THEN** renderer может выбрать более детальные shipping variants и detail layers в пределах profile budget без изменения gameplay transform, collision или replay result

#### Scenario: Ограниченный texture budget
- **WHEN** запрошенные presentation resources превышают budget активного profile
- **THEN** renderer понижает только texture/detail tier до допустимого варианта и продолжает показывать арену без изменения simulation state

### Requirement: Контраст участников и безопасная палитра
Renderer и DOM presentation SHALL резервировать team и participant colors за участниками и HUD. Team A и Team B MUST получать различимую контрастную пару, а FFA MUST назначать одновременно присутствующим участникам попарно различимые цвета из расширяемой vetted palette; palette MUST содержать больше восьми допустимых swatches, хотя roster остаётся не более восьми участников.

#### Scenario: Полный FFA roster
- **WHEN** FFA запускается с восемью участниками
- **THEN** каждый живой participant имеет цвет, различимый от остальных семи и от ближайших architectural materials на игровой дистанции

#### Scenario: Командный матч
- **WHEN** teams match отображает живых участников обеих команд
- **THEN** Team A и Team B имеют разные цвета, а team identity не зависит от названий red или blue

### Requirement: Светлая спортивная читаемость
Arena renderer SHALL использовать medium-dark profile-owned global fill, контролируемый контровой свет для participants, регулярные яркие lamp fixtures по wall rhythm и ограниченные мягкие тени от ближайших local fixtures. Presentation MUST избегать глубоких нечитаемых теней, хоррорного тона, милитаристских маркировок и графического насилия.

#### Scenario: Дальний opponent
- **WHEN** живой participant находится на representative gameplay distance в коридоре или combat region
- **THEN** его silhouette остаётся различимым на фоне arena shell и floor без HUD-only подсказки, включая область, освещённую local lamp shadow caster

#### Scenario: Возрастной тон
- **WHEN** renderer показывает arena, живых participants и corpse presentation
- **THEN** visual treatment остаётся в аркадном спортивном sci-fi тоне 10+ без gore или horror imagery

### Requirement: Recipe-directed architectural presentation
Renderer SHALL выводить presentation из semantic layout recipe и surface classes validated ArenaDefinition: большие залы, corridor loops, courtyards, arched portals, полупрозрачные windows, wall-relief niches, ramps и movement-only barriers. Лампы, локальная цветовая окраска, GLB details, decals и material variants MUST оставаться renderer-only; они MUST NOT создавать либо менять blocking, projectile occlusion, navigation, spawn или arena content hash.

#### Scenario: Читаемый recipe
- **WHEN** renderer показывает две accepted arenas с разными layout recipes
- **THEN** их room/corridor/elevation composition и architectural rhythm различимы на gameplay distance без использования participant или team colours

#### Scenario: Окно и ниша
- **WHEN** renderer отображает canonical window или wall-relief niche
- **THEN** окно визуально полупрозрачно, а ниша считывается как неглубокий рельеф без ложного прохода либо укрытия

#### Scenario: Локальный свет
- **WHEN** arena содержит renderer-owned lamp fixtures
- **THEN** local light улучшает читаемость route и silhouette, не создаёт глубоких теней и не влияет на simulation cadence или identity

#### Scenario: Совпадение видимого портала с физикой
- **WHEN** игрок направлен в стойку или дугу видимого портала
- **THEN** capsule и projectile queries встречают соответствующий canonical solid, а рендер использует тот же authored part contract и transform

### Requirement: Представление локальных потолков
Renderer SHALL отображать canonical потолки и их нижние поверхности с читаемыми материалами и локальным освещением. Новые карты MUST NOT накрываться единой декоративной коробкой вместо геометрии помещений; прежний общий потолок MAY сохраняться для historical definitions.

#### Scenario: Вид из закрытого верхнего помещения
- **WHEN** camera находится в закрытой комнате верхнего этажа
- **THEN** видны собственные стены и потолок этой комнаты, а нижний зал не виден вне объявленных проёмов

### Requirement: Semantic presentation multi-level sectors
Renderer SHALL выводить hall, gallery, balcony и basement presentation только из canonical semantic sector/support slots. Балконные guards MAY быть movement-only canonical barriers; renderer MUST NOT создавать false routes, collision или projectile occlusion.

#### Scenario: Обзор с балкона
- **WHEN** camera видит balcony над большим залом
- **THEN** surface, guard и ramp read остаются визуально различимыми, а collision и projectile behavior совпадают с canonical semantics

### Requirement: Плавная камера на ступенях
Камера SHALL смягчать вертикальные толчки лестницы без дополнительной раскачки. Сглаживание MUST оставаться presentation-only, не проникать в потолок и сохранять соответствие прицела authoritative направлению выстрела.

#### Scenario: Бег по ступеням под потолком
- **WHEN** игрок поднимается или спускается по лестнице
- **THEN** камера движется плавно, не пересекает геометрию и прицел остаётся достоверным
