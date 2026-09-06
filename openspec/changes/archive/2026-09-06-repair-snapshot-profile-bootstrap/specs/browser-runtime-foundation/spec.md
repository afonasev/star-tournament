## ADDED Requirements

### Requirement: Validated profile startup compatibility
Browser runtime SHALL использовать один exact validated profile для match configuration, arena generation, initial simulation snapshot и последующего snapshot parsing. Внутренний parser MUST NOT отклонять initial snapshot, созданный из этого же profile.

#### Scenario: Start current release match
- **WHEN** пользователь запускает матч с current valid release profile
- **THEN** browser создаёт ready match surface без `SimulationSnapshotError` до pointer lock и первого gameplay tick
