## ADDED Requirements

### Requirement: Presentation style validated arena
Browser runtime SHALL применять утверждённый arena presentation style к renderer-derived surfaces одной immutable validated `ArenaDefinition`. Style, lights, fog, transient presentation и materials MUST NOT входить в simulation snapshot, replay, RNG или state hash.

#### Scenario: Renderer style при неизменной симуляции
- **WHEN** один и тот же initial snapshot отображается с разной render cadence, после resize либо в debug surface
- **THEN** style kit не меняет arena identity, participant transforms, simulation hash или количество gameplay ticks

#### Scenario: Presentation performance
- **WHEN** style kit изменяет материалы, освещение или visibility treatment arena
- **THEN** production-browser performance gate сохраняет lifecycle counters и проходит утверждённые portable hard gates
