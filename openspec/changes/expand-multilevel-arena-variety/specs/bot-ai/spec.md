## ADDED Requirements

### Requirement: Multi-level bot route coverage
Bot planning SHALL использовать support-aware routes и telemetry MUST фиксировать посещение каждого declared combat layer, basement bypass и transition recovery.

#### Scenario: Двухъярусная arena
- **WHEN** headless evaluation запускает bot match на recipe с двумя ярусами
- **THEN** bots достигают обоих combat layers через declared transitions без repeated stuck recovery
