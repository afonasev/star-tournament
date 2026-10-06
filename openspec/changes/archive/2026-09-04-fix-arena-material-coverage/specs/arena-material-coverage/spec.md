## Purpose

Capability гарантирует, что видимая архитектура playable arena получает цельное, читаемое material treatment без мерцания и случайных плоских заглушек.

## ADDED Requirements

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
