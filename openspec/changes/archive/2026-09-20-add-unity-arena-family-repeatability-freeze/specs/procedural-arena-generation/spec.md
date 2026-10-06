## MODIFIED Requirements

### Requirement: Каноническая детерминированная ArenaDefinition
Генератор SHALL из конкретных `seed`, `generatorVersion`, size preset и identity валидированного generator profile создавать одну immutable `ArenaDefinition` с canonical semantic-ID order и content hash. Arena RNG MUST быть отделён от match simulation RNG и завершаться до initial tick. Для native registered arena family explicit selected family, canonical profile identity и accepted definition SHALL образовывать frozen hand-off snapshot: повторная generation с теми же входами MUST возвращать ту же arena identity, а derived validator/projection/navigation consumers MUST получать именно этот immutable snapshot без повторного выбора family или подмены profile.

#### Scenario: Одинаковые входы
- **WHEN** два независимых запуска используют одинаковые seed, generator version, size и profile identity
- **THEN** они создают byte-identical canonical JSON и одинаковый arena content hash

#### Scenario: Изменён generation input
- **WHEN** меняется seed, size, generator version либо generator profile content hash
- **THEN** generation identity и effective arena content hash отражают новый результат

#### Scenario: Native family freeze
- **WHEN** два native запуска используют одинаковые seed, generator version, canonical profile identity и явно выбранный registered family
- **THEN** они создают definitions с одной arena identity, а projection и navigation получают тот же accepted frozen snapshot
