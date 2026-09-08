## MODIFIED Requirements

### Requirement: Проходимость и режимная честность
До старта матча validator MUST проверить profile-defined body capsule, region connectivity, spawn placement, FFA fairness и team fairness по canonical surfaces, включающим collision thickness крупных architectural panels. Мелкие presentation-only details MUST NOT входить в validation. Геометрическая симметрия MUST NOT быть обязательной; fairness SHALL оцениваться versioned navigation-distance, route-redundancy и enemy-LOS metrics.

#### Scenario: Асимметричная честная arena
- **WHEN** geometry не зеркальна, но обе team allocations и FFA spawn sets удовлетворяют versioned metrics
- **THEN** arena принимается для обоих режимов

#### Scenario: Непроходимая или нечестная arena
- **WHEN** отсутствует route, безопасный roster placement либо одна сторона систематически получает недопустимое преимущество для effective player clearance
- **THEN** generation завершается стабильным validation report и матч не получает initial snapshot

#### Scenario: Панель закрывает маршрут
- **WHEN** collidable panel уменьшает route ниже effective player clearance либо исключает required spawn placement
- **THEN** definition отклоняется до initial snapshot со стабильным validation report
