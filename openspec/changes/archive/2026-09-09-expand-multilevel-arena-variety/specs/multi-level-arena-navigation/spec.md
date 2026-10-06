## Purpose

Capability задаёт воспроизводимую layer-aware navigation model для арен с перекрывающимися по XZ этажами, не смешивая их gameplay routes и physical supports.

## ADDED Requirements

### Requirement: Явная опора и межэтажный маршрут
Каждый navigation region, spawn anchor и waypoint SHALL ссылаться на canonical support surface. Router MUST соединять разные support surfaces только через declared traversable transition и MUST сохранять stable semantic-ID order.

#### Scenario: Подвал под верхней галереей
- **WHEN** нижняя и верхняя gameplay regions имеют одинаковый XZ участок
- **THEN** route не соединяет их напрямую и выбирает только declared двусторонний переход ramp либо stairs

### Requirement: Physical support-aware acceptance
Physical route validation SHALL проверять feet Y, grounded state и support identity каждого waypoint, включая переходы в обоих направлениях.

#### Scenario: Ложный путь на другом ярусе
- **WHEN** capsule приходит к целевому XZ, но остаётся на другой опоре
- **THEN** validation отклоняет route как недостижимый

### Requirement: Темп подъёма и непрерывность лестницы
Grounded движение SHALL сохранять обычный темп XZ на проходимом подъёме без накопительного торможения; всё перемещение MUST проходить collision resolution. Router MUST сохранять declared последовательность ступеней при входе, выходе и replan внутри лестницы.

#### Scenario: Продолжение подъёма
- **WHEN** персонаж движется вверх или бот перестраивает маршрут на ступени
- **THEN** нет накопительного гашения скорости, а маршрут продолжается по тому же declared переходу без ложного соединения этажей
