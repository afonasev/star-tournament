## Purpose

Capability задаёт детерминированный контракт конфигурации, хода и завершения локального FFA либо командного матча, включая participant lifecycle, статистику, scoring и overtime.

## ADDED Requirements

### Requirement: Versioned конфигурация матча
Матч SHALL запускаться только из валидной сериализуемой конфигурации с identity, roster от двух до восьми уникальных участников, одним seat-backed keyboard/mouse участником, режимом `ffa` либо `teams`, длительностью и nullable целью по очкам. FFA SHALL хранить индивидуальный цвет каждого участника; `teams` MUST назначать каждого участника ровно в `red` либо `blue` и содержать обе непустые команды.

#### Scenario: Валидный FFA
- **WHEN** меню создаёт FFA с одним local seat, хотя бы одним stationary mannequin, допустимыми цветами, длительностью и выключенной целью
- **THEN** симуляция принимает immutable configuration и создаёт initial snapshot с тем же roster и identity

#### Scenario: Валидный командный матч
- **WHEN** roster содержит непустые red и blue команды, одного seat-backed участника и stationary mannequin-участников
- **THEN** configuration принимается, а цвет каждого участника выводится из его команды

#### Scenario: Недоступный participant contract
- **WHEN** configuration запрашивает второй local seat, gamepad, bot, online participant, пустую команду, повторяющийся id либо число участников вне 2–8
- **THEN** validation отклоняет матч до первого tick со стабильным path и не подменяет участника mannequin без явной конфигурации

### Requirement: Детерминированные фазы и таймер
Simulation MUST владеть фазой `running`, `overtime` либо `finished`, elapsed/remaining ticks и finish reason; wall-clock, DOM и renderer MUST NOT менять время матча. Основное время SHALL содержать ровно `durationMinutes * 60 * simulationTicksPerSecond` активных ticks; paused/menu/finished runtime не выполняет gameplay ticks.

#### Scenario: Основное время
- **WHEN** матч без цели выполняет последний tick заданной длительности и имеет единоличного лидера
- **THEN** snapshot становится `finished` на этом tick с причиной `time-limit` и фиксированным результатом

#### Scenario: Пауза
- **WHEN** runtime остановлен pause lifecycle на несколько секунд wall-clock
- **THEN** match phase, elapsed ticks, remaining ticks, scores и state hash не меняются

#### Scenario: Replay с другой render cadence
- **WHEN** два runner применяют одинаковые configuration/profile/arena/actions при разной render cadence
- **THEN** phase, timer, events, result и каждый state hash совпадают

### Requirement: Participant life, death и fixture respawn
Каждый участник SHALL иметь versioned life identity, health, alive/dead state и statistics. В этом single-seat slice stationary mannequin не двигается и не атакует; после lethal damage он проходит profile-defined death/killcam delay и возрождается с новой life identity, полным health и ammo в своём versioned fixture anchor. Этот контракт MUST NOT объявляться bot AI либо универсальной spawn policy.

#### Scenario: Убийство mannequin
- **WHEN** local participant наносит stationary mannequin lethal damage
- **THEN** victim один раз становится dead, получает death, killer получает kill, corpse/death timing начинается, а новые hits не наносят damage этой жизни

#### Scenario: Fixture respawn
- **WHEN** profile-defined respawn delay завершён
- **THEN** stationary mannequin получает новую life identity, полные health/ammo и исходный fixture transform, оставаясь неподвижным и не создавая attack actions

#### Scenario: Gamepad не создаёт участника
- **WHEN** gamepad подключается или отключается во время матча
- **THEN** roster, participant state, teams, viewport и simulation не меняются

### Requirement: Damage ledger и статистика
Simulation SHALL атомарно учитывать для каждого участника kills, assists, deaths, damage dealt, damage received и score. Assist MUST начисляться каждому не-убийце, который наносил victim положительный damage в profile-defined окне до lethal tick; один участник получает не более одного assist за смерть независимо от числа hits.

#### Scenario: Damage statistics
- **WHEN** attacker наносит victim положительный урон
- **THEN** damage dealt attacker и damage received victim увеличиваются на одну и ту же величину без округления, зависящего от порядка events

#### Scenario: Assist в окне
- **WHEN** один участник наносит damage, а другой убивает victim не позднее profile-defined assist window
- **THEN** первый получает один assist и profile-defined assist points, а killer assist не получает

#### Scenario: Assist вне окна или в своей смерти
- **WHEN** последний damage участника старше окна либо участник является killer или victim
- **THEN** assist и assist points ему не начисляются

### Requirement: Kill-chain scoring
Kill score SHALL вычисляться из profile-defined cumulative totals для непрерывной серии убийств, где gap между соседними lethal ticks не превышает maximum gap; каждое убийство после пятого SHALL добавлять post-five increment. Смерть MUST завершать текущую серию.

#### Scenario: Серия до пяти убийств
- **WHEN** участник делает последовательные kills внутри допустимого gap
- **THEN** его kill-derived score после каждого убийства равен соответствующему cumulative total профиля, а общий score дополнительно включает assists

#### Scenario: Разрыв серии
- **WHEN** следующий kill происходит после maximum gap либо участник умирает
- **THEN** новый kill начинает новую серию с первого cumulative total без удаления ранее заработанных очков

#### Scenario: Шестое убийство
- **WHEN** серия продолжается после пятого kill
- **THEN** каждый следующий kill увеличивает ранее заработанный kill-derived score на post-five increment

### Requirement: Победитель, командный итог и overtime без ничьей
После полного применения events каждого tick simulation SHALL вычислять FFA ranking по individual score либо team ranking по сумме scores участников. Активная цель SHALL завершать матч досрочно только при единоличном лидере среди достигших лимита; если лидеров несколько, phase становится `overtime`. На последнем tick основного времени единоличный лидер завершает матч, а равенство запускает overtime. Overtime MUST закончиться на первом последующем tick с единоличным лидером и MUST NOT иметь дополнительного лимита времени.

#### Scenario: Единоличный FFA лидер достигает цели
- **WHEN** после атомарного tick один игрок имеет наибольший score не ниже цели
- **THEN** матч завершается с ним как winner и причиной `score-limit`

#### Scenario: Командный лимит
- **WHEN** сумма scores одной команды достигает цели и строго больше суммы другой
- **THEN** матч завершается победой этой команды; индивидуальный score сам по себе командный матч не завершает

#### Scenario: Одновременное достижение с разными totals
- **WHEN** несколько игроков либо обе команды пересекают лимит в одном tick, но один итог строго больше
- **THEN** полный tick учитывается и матч завершается победой наибольшего итога

#### Scenario: Равенство на лимите или по времени
- **WHEN** после score-limit tick либо последнего tick основного времени максимальный итог разделяют несколько contenders
- **THEN** матч переходит в `overtime`, не объявляет ничью и сохраняет все scores/state

#### Scenario: Завершение overtime
- **WHEN** после последующего overtime tick появляется единоличный лидер
- **THEN** матч немедленно завершается с этим winner и исходной причиной trigger (`score-limit` либо `time-limit`)

#### Scenario: Лимит на последнем tick времени
- **WHEN** score limit впервые достигнут на последнем tick основного времени
- **THEN** finish trigger фиксируется как `score-limit`, включая случай перехода в overtime

### Requirement: Immutable результат и повтор матча
Finished snapshot SHALL содержать immutable ordered standings, winner identity, finish trigger и final tick. Gameplay actions после finish MUST NOT менять snapshot. Repeat SHALL создать новый initial snapshot из той же exact configuration, profile, arena/scenario identity и seed policy, обнулив timer/statistics/lives; exit SHALL уничтожить match runtime и вернуть menu без скрытого продолжения.

#### Scenario: Actions после finish
- **WHEN** runner получает movement/fire actions после `finished`
- **THEN** gameplay state и final result не меняются

#### Scenario: Повтор
- **WHEN** пользователь выбирает повтор после результата или из pause menu
- **THEN** новый матч использует ту же configuration/profile identity, начинается с initial timer/statistics и не продолжает старые life ids

