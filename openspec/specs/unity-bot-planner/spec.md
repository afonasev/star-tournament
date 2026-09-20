# unity-bot-planner Specification

## Purpose
Обеспечить настоящий бой native Unity AI через общие команды движения и оружия с честной информацией, ограниченными затратами и проверяемым жизненным циклом.

## Requirements

### Requirement: Fair active combat
Бот SHALL использовать только собственное состояние, статическую арену и разрешённые наблюдения/память; три сложности MUST менять решения, а не физические или боевые характеристики. Бот SHALL активно искать бой, ограничивать скорость наведения и соблюдать реакцию, боезапас, cooldown и отдельное отпускание спуска.

#### Scenario: Hidden target and life change
- **WHEN** цель скрывается либо её новая жизнь ещё не наблюдалась
- **THEN** решения используют только старую неизменённую память до истечения; стрельба требует актуальной прямой видимости и реакции на конкретную замеченную жизнь

#### Scenario: Real weapon parity
- **WHEN** бот получает возможность выстрела
- **THEN** обычный weapon resolver расходует боезапас, проверяет стены/союзников и начисляет фактический damage; между попытками присутствует release tick

### Requirement: Tactical movement arbitration
Бот SHALL двигаться, менять стрейф, выполнять редкие безопасные прыжки и кратко отходить к доступному укрытию. Временная поддержка SHALL учитывать людей и ботов, сохранять пространство и завершаться. Боевой steering MUST сохранять физическую безопасность маршрута и не прерывать обязательный выход межэтажного перехода.

#### Scenario: Transition during combat
- **WHEN** враг замечен на лестнице или рампе
- **THEN** выбранный выход перехода сохраняется; strafe/jump не подменяет исполнение перехода и не создаёт ложного stuck timeout

#### Scenario: Bounded support and retreat
- **WHEN** бот помогает союзнику либо отходит при низком здоровье
- **THEN** намерение имеет конечный срок и cooldown; бот возвращается к самостоятельному поиску боя, а сведения о скрытом враге не обновляются через союзника неявно

### Requirement: Frozen lifecycle and reproducible planner state
Planner SHALL хранить независимые сериализуемые timers, goal, target life и RNG. Пауза/focus/disconnect SHALL останавливать общий session clock и AI; смерть очищает намерение, Repeat восстанавливает frozen composition/profiles. Snapshot validation MUST отклонять несовместимое/невалидное состояние атомарно; это не полный recorded replay.

#### Scenario: Pause and repeat
- **WHEN** матч ставится на паузу и повторяется после изменения draft
- **THEN** во время паузы нет решений/выстрелов; Repeat использует исходные сложности и profiles, новый чистый driver

### Requirement: Verified native integration
Development journey SHALL исполнять настоящий AI для Bot-kind участников в FFA/Teams,1–4 local views и total8. Он MUST сохранять данные принятого оружием боя и FHD/4K screenshots. Shipping setup, physical controllers, evaluation/holdout и long performance SHALL оставаться отдельными незакрытыми gates.

#### Scenario: Eight participant journey
- **WHEN** запускается development mixed roster
- **THEN** боты двигаются и наносят урон через общий runtime без scripted decisions/damage и без фиктивных устройств; human action routing сохраняется
