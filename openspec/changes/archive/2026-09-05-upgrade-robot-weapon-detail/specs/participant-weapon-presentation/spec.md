## MODIFIED Requirements

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

### Requirement: Third-person оружие участника
Renderer SHALL показывать у каждого живого non-local участника его поддерживаемое локальное manifest-addressed GLB оружие, визуально закреплённое в обеих руках и направленное по participant yaw. Для `double-barrel-shotgun` LOD0 MUST иметь два отчётливо разнесённых multi-part barrel shrouds с recessed muzzle openings и inner bores, центральную visible energy chamber/coil assembly, heat-sink vents, top/side rails, distinct receiver/grip silhouette и owner-color indicator; LOD1 MUST сохранять разнесённые стволы, chamber, grip и indicator. LOD0/LOD1 и textures MUST следовать существующему `clean-future-sport-glb-v1` asset contract.

#### Scenario: Видимое оружие stationary fixture
- **WHEN** живой stationary fixture с `double-barrel-shotgun` виден в игровом viewport
- **THEN** рядом с руками робота виден ориентированный вместе с участником энергодробовик с двумя recessed-bore стволами, energy coil chamber, heat-sink/rail detail и owner-color indicator

#### Scenario: Неподдерживаемая presentation identity
- **WHEN** renderer получает participant либо weapon identity без поддерживаемой presentation model
- **THEN** он не меняет simulation state и показывает стабильный нейтральный fallback без runtime error

### Requirement: First-person видимое оружие и руки
В active first-person viewport renderer SHALL показывать camera-attached LOD0/LOD1 GLB viewmodel с двумя layered роботизированными предплечьями, articulated кистями/пальцами, кабельными и joint-collar details, поддерживаемым оружием и читаемыми раздельными recessed muzzle openings в нижней правой части кадра, не закрывая центральный прицел. Viewmodel MUST использовать тот же weapon semantic identity, что и local participant, и повторять off-white shell/dark-navy joint language world модели, но MUST NOT входить в snapshot, replay либо simulation hash.

#### Scenario: Начальный first-person кадр
- **WHEN** local participant начинает матч с поддерживаемым `double-barrel-shotgun`
- **THEN** первый WebGL кадр показывает две articulated роботизированные руки и читаемый dense-detail двухствольный дробовик с bore/chamber/rail forms в нижней правой части viewport, сохраняя центральный прицел открытым

#### Scenario: Resize без нового tick
- **WHEN** paused first-person viewport меняет размер и presentation invalidated
- **THEN** renderer делает один redraw с корректно расположенной viewmodel и не меняет simulation state
