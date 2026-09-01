## Purpose

Capability задаёт первый полный боевой verb: ограниченный боезапас двухствольного hitscan-дробовика, детерминированные дробины и occlusion, зональный урон robot targets и читаемую обратную связь в одном first-person viewport.

## ADDED Requirements

### Requirement: Боезапас и cadence двухстволки
Игрок SHALL начинать slice с profile-defined боезапасом; один новый fire press MUST расходовать одну единицу и создавать не более одного выстрела после profile-defined cooldown. Перезарядка и восстановление боезапаса до будущего respawn отсутствуют.

#### Scenario: Успешный выстрел
- **WHEN** pointer lock активен, fire переходит из released в pressed, cooldown завершён и ammo больше нуля
- **THEN** simulation уменьшает ammo на один и создаёт один versioned shotgun shot event

#### Scenario: Удерживание fire
- **WHEN** fire остаётся pressed несколько ticks
- **THEN** новые выстрелы не создаются до released и следующего pressed transition

#### Scenario: Cooldown не завершён
- **WHEN** новый fire press приходит раньше разрешённого следующего shot tick
- **THEN** ammo и combat state не меняются, а runtime может показать только non-authoritative feedback недоступности

#### Scenario: Боезапас исчерпан
- **WHEN** fire press приходит при ammo равном нулю
- **THEN** damage и shot event не создаются, ammo остаётся нулём и HUD показывает dry-fire feedback

### Requirement: Детерминированный hitscan-набор дробин
Каждый допустимый выстрел SHALL создавать profile-defined количество ray pellets с range и spread, детерминированно выводимыми из shooter life identity, shot sequence и simulation tick; renderer random и frame timing MUST NOT менять направления или результаты.

#### Scenario: Повтор выстрела
- **WHEN** два runner выполняют один и тот же выстрел из одинакового snapshot
- **THEN** ordered pellet directions, collision hits, damage events и итоговый state hash совпадают

#### Scenario: Укрытие перекрывает цель
- **WHEN** pellet сначала пересекает static arena cover, а затем target hit volume
- **THEN** pellet завершается на cover и не наносит урон цели

#### Scenario: Дальность исчерпана
- **WHEN** до первого допустимого hit больше profile range
- **THEN** pellet не создаёт damage event

### Requirement: Зональный урон robot target
Combat target SHALL иметь сериализуемые head, torso и limb hit volumes, общую health и active state. Каждый pellet MUST внести долю profile-defined полного zone damage, а mixed hit SHALL суммировать вклад всех pellets одного выстрела детерминированно.

#### Scenario: Полное попадание в голову
- **WHEN** все pellets валидного выстрела попадают в head volume активной цели
- **THEN** суммарный profile-defined head damage уничтожает цель с полным стартовым health

#### Scenario: Полное попадание в корпус
- **WHEN** все pellets попадают в torso volume
- **THEN** цель получает profile-defined torso damage и остаётся активной при положительном health

#### Scenario: Полное попадание в конечность
- **WHEN** все pellets попадают в limb volumes
- **THEN** цель получает profile-defined limb damage

#### Scenario: Смешанное попадание
- **WHEN** pellets одного shot распределены между head, torso, limbs и misses
- **THEN** target damage равен сумме долей соответствующих zone damages без округления, зависящего от порядка hits

#### Scenario: Уничтоженная цель
- **WHEN** health цели достигает нуля
- **THEN** target один раз переходит в inactive state, последующие pellets не наносят ей урон, а respawn не выполняется в этом slice

### Requirement: Combat feedback и single-viewport HUD
First-person viewport SHALL показывать central crosshair, current health, shotgun icon/name и ammo. Renderer/DOM SHALL показывать краткие muzzle, impact, hitmarker и target-destroyed feedback из versioned simulation events, не изменяя damage state.

#### Scenario: Попадание
- **WHEN** допустимый shot наносит target damage
- **THEN** игрок видит hitmarker и визуальный impact, а HUD ammo соответствует simulation snapshot

#### Scenario: Промах
- **WHEN** допустимый shot не наносит damage
- **THEN** muzzle feedback остаётся видимым, но hitmarker не показывается

#### Scenario: Destroyed feedback
- **WHEN** target становится inactive
- **THEN** target presentation явно показывает уничтожение, не создавая gameplay collision или нового damage event
