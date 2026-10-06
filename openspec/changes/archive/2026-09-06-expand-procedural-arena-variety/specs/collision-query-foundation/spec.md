## MODIFIED Requirements

### Requirement: Канонические ray и shape queries
Ray/shape queries MUST возвращать только сериализуемые результаты, применять явные filters и стабильно сортировать равные кандидаты по semantic ID; ближайшее допустимое projectile-blocking препятствие SHALL перекрывать более дальний hit. Movement capsule query MUST использовать movement-blocking filter отдельно от projectile occlusion, так что canonical movement-only barriers блокируют capsule, но не попадают в hitscan ray result.

#### Scenario: Hitscan occlusion
- **WHEN** ray пересекает arena cover раньше диагностической цели
- **THEN** query возвращает projectile-blocking cover как ближайший hit, и дальняя цель считается перекрытой

#### Scenario: Movement-only barrier
- **WHEN** ray и movement capsule одновременно пересекают canonical movement-only barrier
- **THEN** ray не возвращает barrier как occluder, а capsule query возвращает stable blocking contact

#### Scenario: Равная дистанция
- **WHEN** несколько допустимых hits имеют одинаковую distance в пределах versioned epsilon
- **THEN** результат выбирается по документированному semantic-ID tie-break и одинаков во всех повторных запусках
