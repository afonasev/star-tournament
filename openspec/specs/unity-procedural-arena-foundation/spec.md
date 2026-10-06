# unity-procedural-arena-foundation Specification

## Purpose
Фиксирует для native Unity один воспроизводимый foundation layout с проверяемой arena identity, чтобы следующие семейства и валидаторы могли добавляться без второго источника пространственных данных.

## Requirements

### Requirement: Канонический native generation input и identity
До создания native match runtime SHALL получать immutable arena definition из явных `seed`, `generatorVersion` и identity валидированного `unity-arena-foundation-v1@1` profile. Один и тот же набор входов MUST создавать semantically identical definition с одинаковой content identity; генератор MUST не читать и не изменять match RNG.

#### Scenario: Повторяемый foundation input
- **WHEN** два независимых native запуска создают арену с одинаковыми seed, generatorVersion и profile identity
- **THEN** они получают одинаковую definition identity и одинаковые named spatial elements до создания session

#### Scenario: Изменённый input
- **WHEN** seed, generatorVersion или profile identity отличается
- **THEN** resulting arena identity явно отражает этот input и не маскируется старой identity

### Requirement: Единственный источник native spatial layout
Native collision, navigation, spawn catalog и presentation foundation SHALL строиться из одной accepted arena definition, а не из независимых spatial constants в match composition. Текущий двухуровневый proving-ground layout MUST сохранять существующие playable routes, supports и spawn points.

#### Scenario: Создание native матча
- **WHEN** native setup создаёт current proving-ground match
- **THEN** collision, NavMesh, navigation transitions и spawn positions принадлежат одной generated definition и existing match behavior сохраняется

### Requirement: Расширяемая граница generator и validation
Foundation generator SHALL регистрировать current layout family отдельно от чистых validators. ARENA-1 MUST валидировать identity и structural completeness current definition, но MUST NOT выбирать новые spatial families, size presets, fallback, replay contract или full fairness metrics.

#### Scenario: Недопустимая foundation definition
- **WHEN** definition не проходит foundation identity либо structural validation
- **THEN** native match creation отклоняется с стабильной причиной до создания gameplay session
