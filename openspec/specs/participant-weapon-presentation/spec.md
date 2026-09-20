# participant-weapon-presentation Specification

## Purpose

Capability задаёт читаемое renderer-only представление спортивных роботов и их оружия, чтобы участник и его экипировка были различимы в бою без влияния на детерминированные правила матча.

## Requirements

### Requirement: Лёгкий робот с цветовой идентичностью участника
Renderer SHALL показывать каждого живого non-local участника как локальный manifest-addressed LOD0/LOD1 GLB лёгкого человекоподобного спортивного робота с fixed meter/axis/pivot contract. LOD0 MUST иметь materially dense layered hard-surface form: отдельные off-white/pale-gray shell panels, dark-navy articulated joints with collars, layered chest/abdomen armour, vents, cable/energy details, articulated hands with visible fingers, chest core, visor, узкие округлые плечевые панели и rear beacon. LOD1 MUST сохранять каждую role-defining форму с меньшей вторичной плотностью. Authoritative participant color MUST быть одновременно виден как минимум на emissive core, visor, плечевых панелях и rear beacon; renderer MUST NOT подменять его архитектурным accent-цветом или hardcoded общим цветом. Модель и texture maps MUST использовать уже поставленный presentation-owned GLB/template cache, texture lifecycle и graphics-quality tiers, не создавая второго loader или residency policy. `light-sport-robot-animation-v1` MUST поставлять одинаковую semantic hierarchy жёстких joint groups для обоих LOD: pelvis/spine/head, shoulder–elbow–wrist–hand и hip–knee–ankle–foot по обеим сторонам, а также weapon/hand mount. Нейтральная поза MUST сохранять принятые silhouette, material, identity-зоны и пространственный attachment; skin deformation MUST NOT использоваться.

#### Scenario: Два живых участника с разными цветами
- **WHEN** snapshot содержит двух живых non-local участников с разными participant colors
- **THEN** renderer показывает два отличимых робота с layered athletic silhouette, articulated hands и читаемыми panel/joint contrast, а цвет каждого виден на core, visor, плечевых панелях и rear beacon

#### Scenario: Уничтожение и возрождение участника
- **WHEN** health участника достигает нуля, а затем его следующая жизнь становится alive
- **THEN** renderer показывает визуальное уничтожение без gameplay collision и восстанавливает ту же authoritative color identity на высокодетализированной модели

#### Scenario: Нейтральная поза обоих LOD
- **WHEN** renderer создаёт LOD0 либо LOD1 робота без активного animation state
- **THEN** semantic joints, weapon mount и все role-defining формы находятся в принятой neutral pose, а simulation state не меняется

### Requirement: Анимация non-local робота derived из presentation state
Renderer SHALL производить полный `light-sport-robot-animation-v1` набор из presentation snapshot/event данных: спортивный idle, walk/run/strafe locomotion, take-off/airborne/landing jump, aiming, firing recoil, hit reaction и destruction. Animation controller MUST плавно смешивать текущую и целевую presentation-only позы при смене supported state и формировать непрерывные idle/locomotion циклы без заметного скачка на границе цикла. Он MUST поворачивать только semantic joint groups и presentation-owned weapon mount; он MUST NOT записывать или изменять position, velocity, collision, hit volumes, combat result, snapshot, replay либо state hash. Existing first-person viewmodel, camera и recoil MUST сохранять принятый отдельный contract.

#### Scenario: Locomotion и прыжок
- **WHEN** presentation snapshot описывает non-local участника с направлением движения либо фазой прыжка
- **THEN** renderer показывает согласованную walk/run/strafe или take-off/airborne/landing позу без изменения authoritative transform и collision

#### Scenario: Combat feedback
- **WHEN** presentation получает supported firing или damage event для non-local участника
- **THEN** соответствующий robot показывает краткую aiming/firing recoil или hit reaction через суставы и weapon mount, не создавая дополнительный shot, damage либо ammo change

#### Scenario: Destruction
- **WHEN** non-local participant становится destroyed
- **THEN** renderer проигрывает механическое destruction движение, сохраняет существующий corpse lifecycle и не превращает animation timing в simulation state

#### Scenario: Плавная смена presentation state
- **WHEN** renderer переключает non-local робота между двумя supported animation states
- **THEN** поза плавно приходит к следующему состоянию без одиночного кадра с резким joint displacement и без изменения authoritative state

### Requirement: Third-person оружие участника
Renderer SHALL показывать у каждого живого non-local участника его поддерживаемое локальное manifest-addressed GLB оружие, визуально закреплённое в обеих руках и направленное по participant yaw. У static wall и wall-attached presentation decor presentation MUST retract weapon до пересечения, сохраняя минимальную видимую сложенную позу и не меняя participant simulation transform либо gameplay collision. Для `double-barrel-shotgun` LOD0 MUST иметь два отчётливо разнесённых multi-part barrel shrouds с recessed muzzle openings и inner bores, центральную visible energy chamber/coil assembly, heat-sink vents, top/side rails, distinct receiver/grip silhouette и owner-color indicator; LOD1 MUST сохранять разнесённые стволы, chamber, grip и indicator. LOD0/LOD1 и textures MUST следовать существующему `clean-future-sport-glb-v1` asset contract.

#### Scenario: Видимое оружие stationary fixture
- **WHEN** живой stationary fixture с `double-barrel-shotgun` виден в игровом viewport
- **THEN** рядом с руками робота виден ориентированный вместе с участником энергодробовик с двумя recessed-bore стволами, energy coil chamber, heat-sink/rail detail и owner-color indicator

#### Scenario: Оружие у static wall
- **WHEN** world weapon приближается к static wall либо выступающему wall-attached decor
- **THEN** renderer retracts видимое оружие до visual boundary в видимую сложенную позу без изменения simulation state

#### Scenario: Неподдерживаемая presentation identity
- **WHEN** renderer получает participant либо weapon identity без поддерживаемой presentation model
- **THEN** он не меняет simulation state и показывает стабильный нейтральный fallback без runtime error

### Requirement: First-person видимое оружие и руки
В active first-person viewport renderer SHALL показывать camera-attached LOD0/LOD1 GLB viewmodel с двумя layered роботизированными предплечьями, articulated кистями/пальцами, кабельными и joint-collar details, поддерживаемым оружием и читаемыми раздельными recessed muzzle openings в нижней правой части кадра, не закрывая центральный прицел. Только когда forward query под прицелом пересекает static wall либо выступающий wall-attached presentation decor, viewmodel MUST retract до пересечения, сохраняя свободный yaw/pitch, минимальную видимую сложенную позу перед camera near plane и отсутствие изменения simulation. Боковая близость к стене MUST NOT менять pose либо блокировать shot. Viewmodel MUST использовать тот же weapon semantic identity, что и local participant, и повторять off-white shell/dark-navy joint language world модели, но MUST NOT входить в snapshot, replay либо simulation hash.

#### Scenario: Начальный first-person кадр
- **WHEN** local participant начинает матч с поддерживаемым `double-barrel-shotgun`
- **THEN** первый WebGL кадр показывает две articulated роботизированные руки и читаемый dense-detail двухствольный дробовик с bore/chamber/rail forms в нижней правой части viewport, сохраняя центральный прицел открытым

#### Scenario: Оружие рядом со стеной
- **WHEN** local participant смотрит на static wall с активным pointer lock
- **THEN** viewmodel не пересекает wall, не исчезает из кадра и yaw/pitch, shot result и simulation position не меняются от retraction

#### Scenario: Resize без нового tick
- **WHEN** paused first-person viewport меняет размер и presentation invalidated
- **THEN** renderer делает один redraw с корректно расположенной viewmodel и не меняет simulation state

### Requirement: Presentation-only реакция выстрела
После versioned `shotgun-shot` event renderer SHALL показывать двойной кинетический muzzle burst на концах обоих стволов, быстро рассеивающийся дым, короткие видимые пути отдельных дробин до resolved stopping point, читаемый material-aware impact и краткую умеренную отдачу first-person viewmodel. Продолжительность, каденс, пути и видимость эффекта MUST быть derived из существующего presentation tick/event contract и MUST NOT менять ammo, hit result, cooldown, damage, collision, replay state или persistent world geometry.

#### Scenario: Допустимый выстрел
- **WHEN** simulation публикует `shotgun-shot` event для local participant
- **THEN** оба ствола first-person оружия дают краткий kinetic burst и дым, дробины видимо следуют от muzzle mounts к authoritative stopping points, viewmodel кратко смещается отдачей, а HUD ammo берёт значение из simulation snapshot

#### Scenario: Impact по миру или участнику
- **WHEN** resolved дробина останавливается на world или participant hit volume
- **THEN** renderer показывает один краткий material-aware impact в resolved точке без дополнительного damage, hit volume или persistent decal

#### Scenario: Dry fire
- **WHEN** fire press не создаёт `shotgun-shot` event из-за пустого боезапаса
- **THEN** renderer не показывает muzzle burst, дым, пути дробин, impact или отдачу как успешный выстрел

### Requirement: Authored equipment surface detail

Робот, world shotgun и first-person shotgun SHALL показывать фактуру в масштабе оборудования: светлое покрытие, металлические узлы и тёмные рукояти, фаски и конструктивно сгруппированные детали. Основные оболочки SHALL использовать сужающиеся многогранные или округлые формы вместо крупных прямоугольных плоскостей. Identity-зоны MUST оставаться читаемыми; визуальная детализация MUST NOT менять simulation state.

#### Scenario: Close equipment inspection
- **WHEN** игрок видит робота или дробовик вблизи
- **THEN** различимы фаски, крепёж, панели и фактура поверхностей без наложенной текстуры архитектурной стены

#### Scenario: Reduced detail
- **WHEN** renderer использует LOD1
- **THEN** сохраняются основные панели, два ствола и цветовая идентификация с сокращёнными вторичными деталями

#### Scenario: Resource lifecycle
- **WHEN** матч закрывается после загрузки моделей
- **THEN** renderer освобождает принадлежащие GLB текстуры и допускает повторный запуск

### Requirement: Slender robot and connected first-person grip

Робот SHALL иметь узкую талию, компактные плечи и тонкие броневые секции конечностей. First-person модель SHALL показывать две различимые кисти, обхватывающие рукоять и цевьё, с непрерывными предплечьями за нижней границей кадра. Геометрия MUST сохранять контакт при отдаче и не менять hit volumes.

#### Scenario: First-person idle and recoil
- **WHEN** игрок видит оружие в покое или при отдаче
- **THEN** обе кисти касаются своих точек хвата, их запястья соединены с предплечьями и обрубленные концы не висят внутри viewport

#### Scenario: Slender silhouette at reduced detail
- **WHEN** renderer выбирает LOD1
- **THEN** остаются стройные пропорции робота и обе связные цепи локоть-запястье-кисть

#### Scenario: Persistent first-person closeup
- **WHEN** руки постоянно видны с камеры игрока
- **THEN** предплечья SHALL показывать стыки панелей, уплотнения, утопленный крепёж и направленную металлическую фактуру; LOD1 сохраняет основные швы

#### Scenario: Robot detailing with accepted weapon preserved
- **WHEN** отображается детализированный робот
- **THEN** SHALL быть видны швы нагрудной/ножной брони, крепёж и металлические обоймы; основные швы сохраняются в LOD1, принятые first-person и weapon GLB остаются неизменными
