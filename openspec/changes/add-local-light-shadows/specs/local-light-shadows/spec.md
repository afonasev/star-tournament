## Purpose

Capability задаёт ограниченные мягкие локальные тени от маршрутных светильников, чтобы архитектура читалась объёмно без изменения игрового пространства и правил матча.

## ADDED Requirements

### Requirement: Renderer-only локальные тени маршрутных ламп

Arena renderer SHALL создавать локальный свет только для approved semantic lamp fixtures и MUST выбирать shadow casters детерминированно по расстоянию до active gameplay camera с stable attachment id как tie-break. Локальные lights, shadow map, soft filtering и selection MUST NOT изменять collision, navigation, spawn, `ArenaDefinition`, snapshot, replay, RNG или state hash.

#### Scenario: Ближайшая маршрутная лампа
- **WHEN** active camera показывает несколько lamp fixtures в arena
- **THEN** renderer даёт мягкую тень от ближайших fixtures в пределах active quality budget, а остальные lamp fixtures сохраняют только световой акцент

#### Scenario: Неизменная arena identity
- **WHEN** одна и та же validated arena запускается с разными shadow budgets
- **THEN** collision, navigation, spawn, snapshot и replay identity остаются одинаковыми

### Requirement: Качество и безопасная читаемость теней

Renderer SHALL применять soft local shadows только в количестве, разрешённом effective graphics profile: `Low` — 0, `Balanced` — 1, `High` — 2, `Ultra` — 4. Базовый fill и participant rim light MUST сохранять видимый silhouette живого participant на gameplay distance; presentation MUST NOT превращать локальную область в глубокую нечитаемую тень.

#### Scenario: Low quality
- **WHEN** active graphics preset равен `Low`
- **THEN** lamp fixtures не создают shadow map, но arena сохраняет bright fill и navigation accents

#### Scenario: High quality
- **WHEN** active graphics preset равен `High` либо `Ultra`
- **THEN** renderer создаёт не более соответственно двух либо четырёх local-light shadow casters и сохраняет bright readable participant silhouette
