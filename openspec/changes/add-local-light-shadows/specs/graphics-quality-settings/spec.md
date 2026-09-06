## ADDED Requirements

### Requirement: Quality budget локальных теней

Active graphics profile SHALL определять effective local-light shadow budget без изменения match configuration, arena identity, input, snapshot или replay: `Low` — 0 casters, `Balanced` — 1, `High` — 2, `Ultra` — 4. Профиль MUST применять только presentation profile values для shadow-map resolution и soft radius.

#### Scenario: Смена graphics preset
- **WHEN** пользователь выбирает другой поддерживаемый graphics preset до следующего матча
- **THEN** browser presentation применяет соответствующий local-light shadow budget к следующему renderer session без изменения arena seed или simulation result
