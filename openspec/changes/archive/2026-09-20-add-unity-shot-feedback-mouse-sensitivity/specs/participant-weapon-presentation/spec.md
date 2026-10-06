## MODIFIED Requirements

### Requirement: Presentation-only реакция выстрела
После versioned `shotgun-shot` event renderer SHALL показывать двойной кинетический muzzle burst на концах обоих стволов, быстро рассеивающийся дым, короткие видимые пути отдельных дробин до resolved stopping point, читаемый material-aware impact и краткую умеренную отдачу first-person viewmodel. Продолжительность, каденс, пути и видимость эффекта MUST быть derived из существующего presentation tick/event contract и MUST NOT менять ammo, hit result, cooldown, damage, collision, replay state или persistent world geometry.

#### Scenario: Допустимый выстрел
- **WHEN** simulation публикует `shotgun-shot` event для local participant
- **THEN** оба ствола first-person оружия дают краткий kinetic burst и дым, дробины видимо следуют от muzzle mounts к authoritative stopping points, viewmodel кратко смещается отдачей, а HUD ammo берёт значение из simulation snapshot

#### Scenario: Impact по миру или участнику
- **WHEN** resolved дробина останавливается на world или participant hit volume
- **THEN** renderer показывает один краткий material-aware impact в resolved точке без дополнительного damage, hit volume или persistent decal

#### Scenario: Dry fire
- **WHEN** fire press не создаёт `shotgun-shot` event из-за пустого боезапаса
- **THEN** renderer не показывает muzzle burst, дым, пути дробин, impact или отдачу как успешный выстрел
