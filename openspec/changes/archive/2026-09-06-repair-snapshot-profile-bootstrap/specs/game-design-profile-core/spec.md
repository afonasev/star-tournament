## ADDED Requirements

### Requirement: Snapshot compatibility exact selected profile
Playable simulation snapshot SHALL принимать exact validated `GameDesignProfile`, выбранный для текущего match startup, если его id, revision и content hash совпадают с identity snapshot. Parser MUST отклонять snapshot с другой identity до gameplay action или simulation tick.

#### Scenario: Release revision creates its initial snapshot
- **WHEN** browser startup использует valid repository release revision
- **THEN** initial snapshot успешно сериализуется и разбирается с той же profile identity до первого gameplay tick

#### Scenario: Different profile identity
- **WHEN** snapshot разбирается в контексте profile с отличающимся id, revision либо content hash
- **THEN** parser возвращает стабильную unsupported-identity ошибку до применения action frame
