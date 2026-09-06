## MODIFIED Requirements

### Requirement: Presentation-only реакция выстрела
После versioned `shotgun-shot` event renderer SHALL показывать двойной кинетический muzzle burst на концах обоих стволов, быстро рассеивающийся дым, короткие трассы дробин и краткую умеренную отдачу first-person viewmodel. Продолжительность, каденс и видимость эффекта MUST быть derived из существующего presentation tick/event contract и MUST NOT менять ammo, hit result, cooldown или damage.

#### Scenario: Допустимый выстрел
- **WHEN** simulation публикует `shotgun-shot` event для local participant
- **THEN** оба ствола first-person оружия дают краткий кинетический burst и дым, дробины получают короткие видимые трассы, а viewmodel кратко смещается отдачей, при этом HUD ammo берёт значение из simulation snapshot

#### Scenario: Dry fire
- **WHEN** fire press не создаёт `shotgun-shot` event из-за пустого боезапаса
- **THEN** renderer не показывает muzzle burst, дым, трассы или отдачу как успешный выстрел
