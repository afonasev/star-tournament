## MODIFIED Requirements

### Requirement: Канонический static world из ArenaDefinition
Static collision geometry SHALL выводиться только из валидированного `ArenaDefinition` в стабильном semantic-ID порядке, включая geometry крупных collidable architectural panels, и MUST NOT зависеть от Three.js objects, DOM или renderer transforms.

#### Scenario: Изменён порядок descriptors
- **WHEN** эквивалентные arena descriptors перечислены в разном порядке
- **THEN** canonical construction создаёт одинаковую collision identity и одинаковые результаты fixture queries

#### Scenario: Renderer не влияет на collision
- **WHEN** renderer отсутствует, работает с другой cadence либо отображает один, два или четыре viewport
- **THEN** collision world, ordered query results и simulation hashes не меняются

#### Scenario: Крупный panel proxy
- **WHEN** один и тот же canonical wall surface содержит collidable panel thickness
- **THEN** independently reconstructed worlds блокируют capsule одинаково и дают равные checkpoint hashes
