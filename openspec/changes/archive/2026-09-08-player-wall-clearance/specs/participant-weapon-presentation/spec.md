## MODIFIED Requirements

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
