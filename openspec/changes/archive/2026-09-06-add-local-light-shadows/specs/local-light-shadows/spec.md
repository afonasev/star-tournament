## Purpose

Capability задаёт ограниченные чёткие локальные тени от маршрутных светильников на stable floor geometry без изменения игрового пространства и правил матча.

## ADDED Requirements

### Requirement: Renderer-only локальные тени маршрутных ламп

Arena renderer SHALL детерминированно размещать яркие renderer-only lamp fixtures по eligible wall rhythm corridors, rooms и combat zones и MUST выбирать shadow casters по расстоянию до active gameplay camera с stable fixture id как tie-break. Локальные lights, emissive lens, shadow map, soft filtering и selection MUST NOT изменять collision, navigation, spawn, `ArenaDefinition`, snapshot, replay, RNG или state hash.

#### Scenario: Ближайшая маршрутная лампа
- **WHEN** active camera показывает corridor, room либо combat zone
- **THEN** renderer показывает регулярный rhythm ярких lamp fixtures, даёт чёткую тень от ближайших fixtures на floor geometry в пределах active quality budget, а остальные fixtures сохраняют emissive световой акцент; wall shell и wall detail не получают local shadows

#### Scenario: Неизменная arena identity
- **WHEN** одна и та же validated arena запускается с разными shadow budgets
- **THEN** collision, navigation, spawn, snapshot и replay identity остаются одинаковыми

### Requirement: Качество и безопасная читаемость теней

Renderer SHALL применять small-radius PCF local shadows только в количестве, разрешённом effective graphics profile: `Low` — 0, `Balanced` — 1, `High` — 2, `Ultra` — 4. Profile-owned medium-dark global fill и participant rim light MUST сохранять видимый silhouette живого participant на gameplay distance; presentation MUST NOT превращать локальную область в глубокую нечитаемую тень.

#### Scenario: Low quality
- **WHEN** active graphics preset равен `Low`
- **THEN** lamp fixtures не создают shadow map, но arena сохраняет bright fill и navigation accents

#### Scenario: High quality
- **WHEN** active graphics preset равен `High` либо `Ultra`
- **THEN** renderer создаёт не более соответственно двух либо четырёх local-light shadow casters и сохраняет bright readable participant silhouette
