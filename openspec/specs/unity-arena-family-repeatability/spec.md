# unity-arena-family-repeatability Specification

## Purpose
Определяет native Unity ARENA-5 contract, который закрепляет repeatable arena identity и frozen hand-off immutable definition/profile между generation и всеми derived consumers.

## Requirements

### Requirement: Repeatable canonical family identity
Для одинакового explicit набора `seed`, generator version, canonical profile identity и selected registered family native arena generation SHALL создавать одну canonical immutable `ArenaDefinition` с одинаковой arena identity. Family selection MUST оставаться explicit и MUST NOT зависеть от seed; повторный вызов MUST NOT заменять family, profile или definition.

#### Scenario: Независимые повторные построения
- **WHEN** два независимых native запуска получают одинаковые four generation inputs
- **THEN** оба возвращают definitions с одинаковой identity и family ID, которые проходят ARENA-4 validation

#### Scenario: Различный explicit input
- **WHEN** меняется хотя бы один из seed, generator version, canonical profile identity или selected family
- **THEN** generated identity отражает этот input и system не выдаёт его за frozen result другого input

### Requirement: Frozen definition and profile hand-off
После успешной validation принятый immutable definition/profile snapshot SHALL быть единственным spatial source для Unity projection и owned navigation context. Каждый consumer MUST проверять совпадение frozen arena identity и profile identity до scene creation или navigation use; mismatch или drift MUST быть отвергнут с устойчивой диагностикой, включающей family ID и затронутый contract element.

#### Scenario: Проекция frozen snapshot
- **WHEN** validated frozen snapshot передаётся в Unity projection
- **THEN** projection создаётся из его semantic data и сохраняет ту же arena identity без повторной generation

#### Scenario: Отклонённый drift
- **WHEN** projection или navigation получает definition/profile, не совпадающий с frozen snapshot
- **THEN** consumer отклоняет его до создания/использования scene state и не подменяет его другим family, seed или profile

### Requirement: Freeze gate does not introduce lifecycle policy
Repeatability/freeze gate SHALL завершаться до gameplay session и MUST NOT добавлять retry, fallback, Repeat UI state, replay schema, topology/fairness scoring либо mutation accepted definition.

#### Scenario: Invalid frozen hand-off
- **WHEN** frozen snapshot не проходит identity/profile consistency check
- **THEN** session и navigation context не создаются, а system сообщает diagnostic без retry или fallback
