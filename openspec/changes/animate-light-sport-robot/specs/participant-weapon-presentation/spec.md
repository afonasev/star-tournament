## MODIFIED Requirements

### Requirement: Лёгкий робот с цветовой идентичностью участника
Renderer SHALL показывать каждого живого non-local участника как локальный manifest-addressed GLB лёгкого человекоподобного спортивного робота с LOD0/LOD1, fixed meter/axis/pivot contract, читаемым силуэтом, chest core, visor, плечевыми панелями и rear beacon. Authoritative participant color MUST быть одновременно виден как минимум на core, visor и плечевых панелях; renderer MUST NOT подменять его архитектурным accent-цветом или hardcoded общим цветом. Модель и texture maps MUST использовать уже поставленный presentation-owned GLB/template cache, texture lifecycle и graphics-quality tiers, не создавая второго loader или residency policy. `light-sport-robot-animation-v1` MUST поставлять одинаковую semantic hierarchy жёстких joint groups для обоих LOD: pelvis/spine/head, shoulder–elbow–wrist–hand и hip–knee–ankle–foot по обеим сторонам, а также weapon/hand mount. Нейтральная поза MUST сохранять принятые silhouette, material, identity-зоны и пространственный attachment; skin deformation MUST NOT использоваться.

#### Scenario: Два живых участника с разными цветами
- **WHEN** snapshot содержит двух живых non-local участников с разными participant colors
- **THEN** renderer показывает два отличимых робота, и цвет каждого виден на его core, visor и плечевых панелях

#### Scenario: Уничтожение и возрождение участника
- **WHEN** health участника достигает нуля, а затем его следующая жизнь становится alive
- **THEN** renderer показывает визуальное уничтожение без gameplay collision и восстанавливает ту же authoritative color identity на живом роботе

#### Scenario: Нейтральная поза обоих LOD
- **WHEN** renderer создаёт LOD0 либо LOD1 робота без активного animation state
- **THEN** semantic joints, weapon mount и все role-defining формы находятся в принятой neutral pose, а simulation state не меняется

### Requirement: Анимация non-local робота derived из presentation state
Renderer SHALL производить полный `light-sport-robot-animation-v1` набор из presentation snapshot/event данных: спортивный idle, walk/run/strafe locomotion, take-off/airborne/landing jump, aiming, firing recoil, hit reaction и destruction. Animation controller MUST поворачивать только semantic joint groups и presentation-owned weapon mount; он MUST NOT записывать или изменять position, velocity, collision, hit volumes, combat result, snapshot, replay либо state hash. Existing first-person viewmodel, camera и recoil MUST сохранять принятый отдельный contract.

#### Scenario: Locomotion и прыжок
- **WHEN** presentation snapshot описывает non-local участника с направлением движения либо фазой прыжка
- **THEN** renderer показывает согласованную walk/run/strafe или take-off/airborne/landing позу без изменения authoritative transform и collision

#### Scenario: Combat feedback
- **WHEN** presentation получает supported firing или damage event для non-local участника
- **THEN** соответствующий robot показывает краткую aiming/firing recoil или hit reaction через суставы и weapon mount, не создавая дополнительный shot, damage либо ammo change

#### Scenario: Destruction
- **WHEN** non-local participant становится destroyed
- **THEN** renderer проигрывает механическое destruction движение, сохраняет существующий corpse lifecycle и не превращает animation timing в simulation state

