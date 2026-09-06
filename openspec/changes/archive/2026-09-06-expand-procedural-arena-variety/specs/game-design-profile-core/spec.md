## MODIFIED Requirements

### Requirement: Полный профиль procedural arena generator
`prototype-v1` SHALL содержать обязательные числовые generator settings для `small`, `medium` и `large`, включая topology recipe budgets, dimensions, connected-wall architecture, corridor/doorway clearance, perimeter complexity, barrier heights, elevation delta, ramp width/slope, spawn separation и fairness budgets. Каждое числовое поле MUST иметь ровно один descriptor со стабильным path, группой, подписью, описанием, unit, hard minimum/maximum и step; generator и validator MUST потреблять одну валидированную immutable revision.

#### Scenario: Generator descriptor coverage
- **WHEN** schema и descriptor registry аудируются
- **THEN** все numeric arena-generator leaves трёх presets имеют ровно один descriptor без orphan, duplicate или runtime constants

#### Scenario: Неверный generator profile
- **WHEN** preset нарушает range, step либо cross-field invariant для размеров, routes, barrier/elevation/ramp geometry, slots или fairness
- **THEN** profile validation возвращает deterministic code и связанные paths до generation

#### Scenario: Изменение после generation
- **WHEN** пользователь изменяет generator draft после создания arena
- **THEN** текущая arena и match identity не меняются, а новые значения применяются только к следующей сохранённой revision и следующей generation
