## ADDED Requirements

### Requirement: Authored equipment surface detail

Робот, world shotgun и first-person shotgun SHALL показывать фактуру в масштабе оборудования: светлое покрытие, металлические узлы и тёмные рукояти, фаски и конструктивно сгруппированные детали. Identity-зоны MUST оставаться читаемыми; визуальная детализация MUST NOT менять simulation state.

#### Scenario: Close equipment inspection
- **WHEN** игрок видит робота или дробовик вблизи
- **THEN** различимы фаски, крепёж, панели и фактура поверхностей без наложенной текстуры архитектурной стены

#### Scenario: Reduced detail
- **WHEN** renderer использует LOD1
- **THEN** сохраняются основные панели, два ствола и цветовая идентификация с сокращёнными вторичными деталями

#### Scenario: Resource lifecycle
- **WHEN** матч закрывается после загрузки моделей
- **THEN** renderer освобождает принадлежащие GLB текстуры и допускает повторный запуск
