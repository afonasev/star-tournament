## Purpose

Capability задаёт проверяемую семантику высоты архитектуры, чтобы читаемые стены образовывали непрерывный объём до потолка, а низкие объекты были однозначно укрытиями.

## ADDED Requirements

### Requirement: Full-height primary architecture
Accepted procedural arena SHALL строить primary corridor, room и perimeter walls от floor до profile-defined `wallHeightMeters`. Эти surfaces MUST иметь canonical collision geometry и входить в navigation, spawn safety и arena content hash.

#### Scenario: Центральная стена
- **WHEN** generator создаёт primary wall в combat либо route architecture
- **THEN** её верхняя грань совпадает с profile-defined ceiling height и не оставляет случайный вертикальный разрыв

### Requirement: Intentional low obstacles
Only explicitly identified buttress/cover obstacles MAY быть ниже primary wall height. Они MUST сохранять canonical collision geometry и не маскироваться как полная wall chain.

#### Scenario: Низкое укрытие
- **WHEN** arena содержит buttress или cover obstacle
- **THEN** его высота меньше потолка, а validation и collision используют именно его фактический canonical extent
