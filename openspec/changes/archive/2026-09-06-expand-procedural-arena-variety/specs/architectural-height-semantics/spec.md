## MODIFIED Requirements

### Requirement: Intentional low obstacles
Only explicitly identified movement-only barriers MAY быть ниже primary wall height. Они MUST сохранять canonical collision geometry, явную movement/projectile filter semantics и не маскироваться как полная wall chain. Их высота MUST быть выбрана из versioned profile range, а validator MUST подтвердить, что барьер не создаёт narrow gap, непроходимый маршрут либо недопустимое spawn advantage.

#### Scenario: Низкое укрытие
- **WHEN** arena содержит movement-only barrier
- **THEN** его высота меньше потолка, capsule movement блокируется его фактическим canonical extent, а hitscan/LOS не получают от него occlusion

#### Scenario: Перепад пола
- **WHEN** canonical floor surface образует elevation zone и ramp
- **THEN** validator использует её фактические heights для capsule route, spawn safety и navigation distance
