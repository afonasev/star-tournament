# architectural-height-semantics Specification

## Purpose

Capability задаёт проверяемую семантику высоты архитектуры, чтобы читаемые стены образовывали непрерывный объём до потолка, а низкие объекты были однозначно укрытиями.

## Requirements

### Requirement: Full-height primary architecture
Accepted procedural arena SHALL строить primary corridor, room и perimeter walls от floor до profile-defined `wallHeightMeters`. Эти surfaces MUST иметь canonical collision geometry и входить в navigation, spawn safety и arena content hash.

#### Scenario: Центральная стена
- **WHEN** generator создаёт primary wall в combat либо route architecture
- **THEN** её верхняя грань совпадает с profile-defined ceiling height и не оставляет случайный вертикальный разрыв

### Requirement: Intentional low obstacles
Only explicitly identified movement-only barriers MAY быть ниже primary wall height. Они MUST сохранять canonical collision geometry, явную movement/projectile filter semantics и не маскироваться как полная wall chain. Их высота MUST быть выбрана из versioned profile range, а validator MUST подтвердить, что барьер не создаёт narrow gap, непроходимый маршрут либо недопустимое spawn advantage.

#### Scenario: Низкое укрытие
- **WHEN** arena содержит movement-only barrier
- **THEN** его высота меньше потолка, capsule movement блокируется его фактическим canonical extent, а hitscan/LOS не получают от него occlusion

#### Scenario: Перепад пола
- **WHEN** canonical floor surface образует elevation zone и ramp
- **THEN** validator использует её фактические heights для capsule route, spawn safety и navigation distance
