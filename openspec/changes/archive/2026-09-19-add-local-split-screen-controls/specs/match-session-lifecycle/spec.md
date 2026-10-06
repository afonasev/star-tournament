## MODIFIED Requirements

### Requirement: Versioned конфигурация матча
Матч SHALL запускаться только из валидной сериализуемой конфигурации с identity, roster от двух до восьми уникальных участников, от одного до четырёх local seats в browser либо без local seats в явно выбранном headless harness, режимом `ffa` либо `teams`, длительностью и nullable целью по очкам. Каждый browser local seat MUST иметь уникальную device binding identity; FFA SHALL хранить individual participant color, а `teams` MUST назначать каждого participant ровно в `team-a` либо `team-b` и содержать обе непустые команды.

#### Scenario: Валидный FFA
- **WHEN** меню создаёт FFA с 1–4 уникально привязанными local seats, ботами и допустимыми цветами
- **THEN** симуляция принимает immutable configuration и создаёт initial snapshot с тем же roster и identity

#### Scenario: Валидный командный матч
- **WHEN** roster содержит непустые Team A и Team B, 1–4 уникально привязанных seats и допустимую контрастную team-color пару
- **THEN** configuration принимается, а color каждого участника выводится из его команды

#### Scenario: Недоступный participant contract
- **WHEN** configuration содержит ноль либо более четырёх local seats, повторяющуюся binding identity, пустую команду, повторяющийся participant id либо число участников вне 2–8
- **THEN** validation отклоняет матч до первого tick со стабильным path и не подменяет participant без явной конфигурации

#### Scenario: Headless roster
- **WHEN** evaluation harness явно запускает валидные 2–8 bot-only участников
- **THEN** симуляция принимает состав без local seats, а browser setup продолжает требовать 1–4 local seats

#### Scenario: Боты разных уровней
- **WHEN** valid browser roster содержит ботов трёх поддерживаемых уровней
- **THEN** configuration принимает и сохраняет отдельную difficulty каждого

#### Scenario: Неверная сложность
- **WHEN** bot difficulty отсутствует или неизвестна
- **THEN** validation отклоняет configuration со стабильным path без default подмены
