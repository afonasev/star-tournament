## ADDED Requirements

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
