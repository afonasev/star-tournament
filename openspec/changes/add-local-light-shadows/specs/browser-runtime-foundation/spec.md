## ADDED Requirements

### Requirement: Performance evidence локальных теней

Production-browser performance gate SHALL фиксировать effective graphics preset и local-light shadow budget для renderer change, затрагивающего локальные тени. Gate MUST сохранять existing lifecycle counters и MUST NOT включать shadow telemetry в simulation, replay или state hash.

#### Scenario: Проверка quality tiers
- **WHEN** browser performance gate запускает renderer с каждым поддерживаемым graphics preset
- **THEN** report показывает active preset и effective shadow budget, а portable lifecycle hard gates сохраняют passed result
