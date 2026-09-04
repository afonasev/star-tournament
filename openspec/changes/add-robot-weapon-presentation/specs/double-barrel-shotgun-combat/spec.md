## MODIFIED Requirements

### Requirement: Combat feedback и single-viewport HUD
First-person viewport SHALL показывать central crosshair, current health, shotgun icon/name и ammo. Renderer/DOM SHALL показывать краткие muzzle, impact, hitmarker и target-destroyed feedback из versioned simulation events, не изменяя damage state. При поддерживаемом `double-barrel-shotgun` renderer SHALL также показывать camera-attached видимую модель двухствольного оружия с двумя роботизированными предплечьями; успешный `shotgun-shot` MUST вызвать краткую двойную muzzle flash на концах обоих стволов и presentation-only отдачу.

#### Scenario: Попадание
- **WHEN** допустимый shot наносит target damage
- **THEN** игрок видит hitmarker и визуальный impact, HUD ammo соответствует simulation snapshot, а видимая двухстволка показывает краткую двойную muzzle flash

#### Scenario: Промах
- **WHEN** допустимый shot не наносит damage
- **THEN** muzzle feedback остаётся видимым на обоих стволах и viewmodel получает отдачу, но hitmarker не показывается

#### Scenario: Destroyed feedback
- **WHEN** target становится inactive
- **THEN** target presentation явно показывает уничтожение, не создавая gameplay collision или нового damage event

#### Scenario: Dry fire не имитирует shot
- **WHEN** fire press не создаёт simulation shotgun shot event
- **THEN** viewmodel не показывает успешную двойную вспышку или отдачу
