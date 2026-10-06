## MODIFIED Requirements

### Requirement: Зональный урон robot target
Combat mannequin SHALL быть stationary match participant с сериализуемыми head, torso и limb hit volumes, общей health, life identity и alive/dead state. Каждый pellet MUST внести долю profile-defined полного zone damage, а mixed hit SHALL суммировать вклад всех pellets одного выстрела детерминированно. Lethal damage MUST создать один participant-death event; respawn создаёт новую life identity после profile-defined delay вместо permanent inactive state.

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

## ADDED Requirements

### Requirement: Combat events для match statistics
Каждый положительный damage и lethal transition SHALL публиковать versioned simulation events с attacker participant, victim participant/life, tick и exact amount, достаточные для deterministic damage ledger, assists и scoring; presentation feedback MUST NOT быть источником этих данных.

#### Scenario: Damage event
- **WHEN** один или несколько pellets наносят participant положительный damage
- **THEN** один ordered damage event связывает attacker, victim life и фактический health delta

#### Scenario: Lethal event ordering
- **WHEN** shot убивает participant
- **THEN** damage event предшествует единственному death event в canonical event order того же tick
