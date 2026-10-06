## MODIFIED Requirements

### Requirement: Versioned конфигурация матча
Матч SHALL запускаться только из валидной сериализуемой конфигурации с identity, roster от двух до восьми уникальных участников, одним seat-backed keyboard/mouse участником, режимом `ffa` либо `teams`, длительностью и nullable целью по очкам. FFA SHALL хранить индивидуальный participant color, назначенный из расширяемой vetted palette; `teams` MUST назначать каждого участника ровно в `team-a` либо `team-b`, содержать обе непустые команды и хранить одну контрастную team-color пару для Team A и Team B. Цвета и user-facing names MUST NOT использовать legacy identities `red` либо `blue`.

#### Scenario: Валидный FFA
- **WHEN** меню создаёт FFA с одним local seat, хотя бы одним stationary mannequin, допустимыми попарно различимыми цветами, длительностью и выключенной целью
- **THEN** симуляция принимает immutable configuration и создаёт initial snapshot с тем же roster и identity

#### Scenario: Валидный командный матч
- **WHEN** roster содержит непустые Team A и Team B, одного seat-backed участника, stationary mannequin-участников и допустимую контрастную team-color пару
- **THEN** configuration принимается, а цвет каждого участника выводится из его команды

#### Scenario: Недоступный participant contract
- **WHEN** configuration запрашивает второй local seat, gamepad, bot, online participant, пустую команду, повторяющийся id либо число участников вне 2–8
- **THEN** validation отклоняет матч до первого tick со стабильным path и не подменяет участника mannequin без явной конфигурации
