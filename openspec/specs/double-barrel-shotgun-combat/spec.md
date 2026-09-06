# double-barrel-shotgun-combat Specification

## Purpose

Capability задаёт первый полный боевой verb: ограниченный боезапас двухствольного hitscan-дробовика, детерминированные дробины и occlusion, зональный урон robot targets и читаемую обратную связь в одном first-person viewport.

## Requirements

### Requirement: Боезапас и cadence двухстволки
Каждый вооружённый участник SHALL начинать slice с profile-defined боезапасом; один новый fire press MUST расходовать одну единицу и создавать не более одного выстрела после profile-defined cooldown. При достижении нуля после выстрела simulation MUST в том же tick восстановить profile-defined emptyRefillAmmo (shipped 20); cooldown, shotSequence и previousFirePressed MUST сохраняться. Валидный живой zero-ammo state SHALL нормализоваться до обработки нового fire transition по тому же правилу. Ручная перезарядка отсутствует.

#### Scenario: Успешный выстрел
- **WHEN** pointer lock активен, fire переходит из released в pressed, cooldown завершён и ammo больше нуля
- **THEN** simulation расходует одну единицу и создаёт один versioned shotgun shot event; если остаток равен нулю, он сразу пополняется

#### Scenario: Удерживание fire
- **WHEN** fire остаётся pressed несколько ticks
- **THEN** новые выстрелы не создаются до released и следующего pressed transition

#### Scenario: Cooldown не завершён
- **WHEN** новый fire press приходит раньше разрешённого следующего shot tick
- **THEN** выстрел не создаётся и патрон не расходуется; допускается только нормализация нулевого ammo по правилу пополнения, а runtime может показать non-authoritative feedback недоступности

#### Scenario: Боезапас исчерпан
- **WHEN** fire press приходит при ammo равном нулю
- **THEN** simulation сначала пополняет ammo по профилю, затем применяет обычные cooldown/pressed правила; при допустимом выстреле расходуется одна единица

#### Scenario: Последний патрон
- **WHEN** живой человек либо бот делает допустимый выстрел с ammo равным одному
- **THEN** создаётся ровно один shot, итоговый ammo равен 20 в shipped profile, и следующий shot требует cooldown и нового нажатия

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
Каждый живой local-seat, bot либо stationary fixture SHALL быть match participant с сериализуемыми head, torso и limb hit volumes, общей health, life identity и alive/dead state. Каждый pellet MUST внести долю profile-defined полного zone damage, а mixed hit SHALL суммировать вклад всех pellets одного выстрела детерминированно. Lethal damage MUST создать один participant-death event; respawn создаёт новую life identity после profile-defined delay вместо permanent inactive state.

#### Scenario: Полное попадание в голову
- **WHEN** все pellets валидного выстрела попадают в head volume живого mannequin-участника
- **THEN** суммарный profile-defined head damage убивает текущую жизнь и создаёт один death event

#### Scenario: Полное попадание в корпус
- **WHEN** все pellets попадают в torso volume
- **THEN** участник получает profile-defined torso damage и остаётся жив при положительном health

#### Scenario: Полное попадание в конечность
- **WHEN** все pellets попадают в limb volumes
- **THEN** участник получает profile-defined limb damage

#### Scenario: Смешанное попадание
- **WHEN** pellets одного shot распределены между head, torso, limbs и misses
- **THEN** participant damage равен сумме долей соответствующих zone damages без округления, зависящего от порядка hits

#### Scenario: Уничтоженная цель
- **WHEN** health текущей жизни достигает нуля
- **THEN** последующие pellets до respawn не наносят ей урон и не создают повторные kills/deaths

#### Scenario: Новая жизнь
- **WHEN** mannequin respawn завершён
- **THEN** новая life identity снова принимает hits, а damage старой жизни не переносится

#### Scenario: Движущийся противник
- **WHEN** человек либо бот стреляет по другому живому участнику
- **THEN** hit volumes выводятся из актуального simulation transform цели, а damage и statistics используют общие правила независимо от participant kind

### Requirement: Combat events для match statistics
Каждый положительный damage и lethal transition SHALL публиковать versioned simulation events с attacker participant, victim participant/life, tick и exact amount, достаточные для deterministic damage ledger, assists и scoring; presentation feedback MUST NOT быть источником этих данных.

#### Scenario: Damage event
- **WHEN** один или несколько pellets наносят participant положительный damage
- **THEN** один ordered damage event связывает attacker, victim life и фактический health delta

#### Scenario: Lethal event ordering
- **WHEN** shot убивает participant
- **THEN** damage event предшествует единственному death event в canonical event order того же tick

### Requirement: Combat feedback и single-viewport HUD
First-person viewport SHALL показывать central crosshair, current health, shotgun icon/name и ammo. Renderer/DOM SHALL показывать краткие muzzle, per-pellet kinetic trajectory/impact, hitmarker и target-destroyed feedback из versioned simulation events, не изменяя damage state. При поддерживаемом `double-barrel-shotgun` renderer SHALL также показывать camera-attached видимую модель двухствольного оружия с двумя роботизированными предплечьями; успешный `shotgun-shot` MUST вызвать краткую двойную кинетическую muzzle flash на концах обоих стволов и presentation-only отдачу.

#### Scenario: Попадание
- **WHEN** допустимый shot наносит target damage
- **THEN** игрок видит hitmarker, per-pellet визуальный impact и краткие кинетические траектории, HUD ammo соответствует simulation snapshot, а видимая двухстволка показывает краткую двойную muzzle flash

#### Scenario: Промах
- **WHEN** допустимый shot не наносит damage
- **THEN** muzzle feedback и траектории завершившихся дробин остаются видимыми, viewmodel получает отдачу, но hitmarker не показывается

#### Scenario: Destroyed feedback
- **WHEN** target становится inactive
- **THEN** target presentation явно показывает уничтожение, не создавая gameplay collision или нового damage event

#### Scenario: Dry fire не имитирует shot
- **WHEN** fire press не создаёт simulation shotgun shot event
- **THEN** viewmodel не показывает успешную двойную вспышку, траектории или отдачу
