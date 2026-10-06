## MODIFIED Requirements

### Requirement: Явная опора и межэтажный маршрут
Каждый navigation region, spawn anchor и waypoint SHALL ссылаться на canonical support surface. Router MUST соединять разные support surfaces только через declared traversable transition и MUST сохранять stable semantic-ID order. Native owned navigation context MUST быть создан только из validated frozen arena definition/profile snapshot и MUST отклонять arena identity или profile identity mismatch до route query.

#### Scenario: Подвал под верхней галереей
- **WHEN** нижняя и верхняя gameplay regions имеют одинаковый XZ участок
- **THEN** route не соединяет их напрямую и выбирает только declared двусторонний переход ramp либо stairs

#### Scenario: Navigation drift
- **WHEN** navigation context получает definition или profile, которые не совпадают с frozen snapshot projected arena
- **THEN** context не публикует route и сообщает stable mismatch diagnostic до использования чужих supports или transitions
