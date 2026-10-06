# arena-material-coverage Specification

## Purpose

Capability гарантирует, что видимая архитектура playable arena получает цельное, читаемое material treatment без мерцания и случайных плоских заглушек.

## Requirements

### Requirement: Полное presentation material coverage
Arena renderer SHALL применять согласованный `clean-future-sport-v1` material treatment ко всем видимым architectural surface classes: off-white/pale-gray для wall shell и ceiling-facing architecture, dark navy для floor и сдержанные cyan/lime/orange accents только для route, doorway и landmark. Renderer MUST NOT оставлять видимую крупную surface class с flat fallback colour при доступном texture kit.

#### Scenario: Wall и ceiling в playable camera
- **WHEN** камера видит wall shell, route wall либо потолочную/верхнюю архитектурную плоскость
- **THEN** каждая поверхность читается как часть согласованного panel kit, а не как однотонная cyan либо неоформленная плоскость

#### Scenario: Floor сохраняет контраст маршрута
- **WHEN** камера видит floor и route details одновременно
- **THEN** тёмный navy floor остаётся отличим от светлой архитектуры и accent guides без использования participant colors

### Requirement: Presentation geometry без z-fighting
Renderer SHALL размещать presentation-owned frame, seam, GLB bay и portal details с однозначным separation от их base surface. Он MUST NOT полагаться на coplanar overlap как на видимый compositing strategy.

#### Scenario: Узкий wall стык
- **WHEN** камера приближается к стыку wall bay, frame или portal detail
- **THEN** поверхность остаётся стабильной при движении камеры без мерцания, полос depth-conflict или смены приоритета текстур

#### Scenario: Detail tier не меняет gameplay
- **WHEN** active graphics quality изменяет LOD либо texture detail
- **THEN** visual material coverage и отсутствие z-fighting сохраняются, а collision, navigation, spawn и arena hash не меняются

### Requirement: Вариативное material coverage по секторам
Renderer SHALL выбирать совместимые wall, floor и sector material variants из versioned `clean-future-sport` presentation kit по semantic layout/sector slots, сохраняя off-white/pale-gray architectural shell, dark-navy floor и cyan/lime/orange navigation accents. Floor, ramp и raised-zone treatment MUST сохранять различимый контраст и направление маршрута; крупная поверхность MUST NOT возвращаться к flat fallback colour при доступном texture kit.

#### Scenario: Два layout recipe
- **WHEN** renderer показывает две accepted arenas с разными sector assignments
- **THEN** wall/floor panel rhythm и sector accents различаются, но participant/team colors не используются как окружение

#### Scenario: Рампа и высотная зона
- **WHEN** камера видит ramp, lower floor и raised zone одновременно
- **THEN** material treatment читаемо отделяет уровни и направление перехода без z-fighting или ложной геометрии

#### Scenario: Все стороны архитектурного модуля
- **WHEN** видны боковая, верхняя и лицевая грани wall-bay или портала
- **THEN** каждая грань имеет невырожденную UV-площадь и фактуру с согласованной плотностью по метрам; панель не выходит за host collision solid

### Requirement: Variants для vertical spatial roles
Presentation material kit SHALL выбирать compatible textured wall/floor/decal variants для hall, gallery, balcony и basement sectors. Texture residency и quality degradation MUST оставаться presentation-only и MUST NOT изменять arena identity, collision, navigation или participant colors.

#### Scenario: Контраст ярусов
- **WHEN** camera одновременно видит lower floor, ramp и upper gallery
- **THEN** текстурный treatment отчётливо различает направление и роль каждого сектора без flat fallback или z-fighting
