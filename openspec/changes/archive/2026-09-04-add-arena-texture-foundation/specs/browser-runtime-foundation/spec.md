## MODIFIED Requirements

### Requirement: Presentation style validated arena
Browser runtime SHALL применять утверждённый arena presentation style и active graphics-quality profile к renderer-derived surfaces одной immutable validated `ArenaDefinition`. Style, textures, decals, quality preferences, lights, fog и transient presentation MUST NOT входить в simulation snapshot, replay, RNG или state hash. Runtime MUST поддерживать display output до 3840×2160; при ограниченном presentation budget он SHALL уменьшать только internal render scale и presentation detail, сохраняя DOM/HUD layout и gameplay camera/simulation.

#### Scenario: Renderer style при неизменной симуляции
- **WHEN** один и тот же initial snapshot отображается с разной render cadence, после resize, с другим quality profile либо в debug surface
- **THEN** style kit не меняет arena identity, participant transforms, simulation hash или количество gameplay ticks

#### Scenario: Presentation performance
- **WHEN** style kit изменяет материалы, texture quality, LOD, decals, освещение или visibility treatment arena
- **THEN** production-browser performance gate сохраняет lifecycle counters и проходит утверждённые portable hard gates

#### Scenario: 4K display output
- **WHEN** playfield отображается на 3840×2160 display output
- **THEN** browser runtime сохраняет читаемый DOM/HUD layout и применяет active graphics-quality profile без повышения simulation cadence либо нарушения pause/hidden zero-work contract
