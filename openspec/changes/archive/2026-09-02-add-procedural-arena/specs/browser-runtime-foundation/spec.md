## ADDED Requirements

### Requirement: Browser session из validated ArenaDefinition
Browser shell SHALL завершить profile validation, generation и arena validation до создания initial snapshot, collision world и renderer. Все runtime consumers MUST получать один immutable `ArenaDefinition`; fixture arena не может оставаться скрытым production default.

#### Scenario: Успешный generated startup
- **WHEN** выбранные size/seed/profile создают accepted arena
- **THEN** snapshot, collision checkpoint, renderer dataset и debug surface показывают одну arena identity и content hash до первого input tick

#### Scenario: Spatial source mismatch
- **WHEN** snapshot identity, definition, collision projection либо renderer description не совпадают
- **THEN** startup завершается стабильной compatibility error до gameplay tick и WebGL loop

### Requirement: Самодостаточный arena replay
Playable replay SHALL хранить validated `ArenaDefinition` один раз рядом с initial snapshot и MUST проверять совпадение id, seed, generator version и content hash до reconstruct collision world или применения action frame.

#### Scenario: Replay reconstruction
- **WHEN** replay запускается без исходной browser session
- **THEN** runtime восстанавливает derived collision/navigation data из embedded definition и получает те же per-tick hashes

#### Scenario: Подменена definition
- **WHEN** embedded definition не соответствует initial snapshot arena identity
- **THEN** replay отклоняется до первого action frame

### Requirement: Procedural arena performance evidence
Каждый size preset MUST проходить production-browser gate; densest accepted representative seed SHALL сохранять renderer counters, collision timings, generation/validation timings и lifecycle counters без включения telemetry в simulation/replay/hash.

#### Scenario: Representative sizes
- **WHEN** performance gate запускает small, medium и large seeds
- **THEN** portable cadence/lifecycle gates проходят, а device-sensitive counters публикуются как evidence или явно unavailable
