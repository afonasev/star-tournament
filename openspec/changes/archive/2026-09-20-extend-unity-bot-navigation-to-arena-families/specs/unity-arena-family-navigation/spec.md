## Purpose

Определяет проверяемое native следование bot через canonical `ArenaDefinition` каждого из пяти зарегистрированных пространственных семейств.

## ADDED Requirements

### Requirement: Definition-owned navigation context
Навигация SHALL принимать только один активный native context, построенный из принятого immutable `ArenaDefinition`. Она MUST получать supports, declared transitions, route anchors и physical geometry только из этой definition и MUST NOT выбирать family по именам, глобальным координатам или второму spatial source.

#### Scenario: Выбор family
- **WHEN** active profile создаёт любую из пяти допустимых ARENA-2 definitions
- **THEN** navigation использует её own supports и transitions без отдельного prefab, browser route или family-specific follower branch

#### Scenario: Чужая arena
- **WHEN** endpoint или native query относится к другой одновременно созданной arena
- **THEN** owned context MUST отклонить маршрут до выдачи movement action

### Requirement: Physical declared-transition traversal
Для каждого declared transition navigation SHALL independently validate a complete native route по ordered physical feet, последовательности named supports и capsule headroom. Bot MUST выдавать только ordinary move/look actions existing motor и MUST NOT прыгать, телепортироваться или использовать recovery на свободном валидном переходе.

#### Scenario: Пять families
- **WHEN** bot получает маршрут по каждому из двух transitions каждой ARENA-2 family в обоих направлениях и трёх capsule lanes
- **THEN** он достигает цели с допустимыми feet/support/headroom, без recovery и без bypass collision

#### Scenario: Overlapping XZ
- **WHEN** lower и upper supports имеют одинаковые X/Z координаты
- **THEN** достижение верхней цели возможно только через declared transition и совпадение X/Z MUST NOT считаться arrival

### Requirement: Bounded physical failure and lifecycle
Неполный путь, отсутствующий physical entrance, недостаточный headroom или живой capsule blocker MUST приводить к явному отказу либо existing bounded recovery без изменения blocker/collision. Replan и snapshot restore SHALL повторно проверить route в current owned definition; pause и Repeat MUST сохранить прежние lifecycle boundaries.

#### Scenario: Недопустимый или занятый маршрут
- **WHEN** transition разорван, headroom уменьшен, endpoint не принадлежит support либо passage занят живым участником
- **THEN** bot не проходит сквозь geometry/participant и возвращает bounded state без ложного arrival

### Requirement: Diagnostic evidence boundary
Изменение SHALL иметь EditMode/PlayMode, Mac Development Player build и muted FHD/4K native Player evidence representative families. Workload measurements MUST быть помечены diagnostic и MUST NOT объявляться physical device/TV, artistic, target-hardware performance или playable-bots acceptance.

#### Scenario: Успешный diagnostic
- **WHEN** automated и native Player checks проходят
- **THEN** evidence фиксирует family identity и traversal result, а открытые ARENA-4/5 и acceptance gates сохраняются открытыми
