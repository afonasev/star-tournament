## MODIFIED Requirements

### Requirement: Светлая спортивная читаемость
Arena renderer SHALL использовать medium-dark profile-owned global fill, контролируемый контровой свет для participants, регулярные яркие lamp fixtures по wall rhythm и ограниченные мягкие тени от ближайших local fixtures. Presentation MUST избегать глубоких нечитаемых теней, хоррорного тона, милитаристских маркировок и графического насилия.

#### Scenario: Дальний opponent
- **WHEN** живой participant находится на representative gameplay distance в коридоре или combat region
- **THEN** его silhouette остаётся различимым на фоне arena shell и floor без HUD-only подсказки, включая область, освещённую local lamp shadow caster

#### Scenario: Возрастной тон
- **WHEN** renderer показывает arena, живых participants и corpse presentation
- **THEN** visual treatment остаётся в аркадном спортивном sci-fi тоне 10+ без gore или horror imagery
