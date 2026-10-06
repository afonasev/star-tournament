# unity-bot-controlled-seats Specification

## Purpose
Позволить одному оператору наблюдать настоящий бой от лица ботов в обычном split-screen setup без дополнительных физических устройств.

## Requirements

### Requirement: Отображаемое место независимо от источника действий

Обычный setup SHALL разрешать human/AI и индивидуальную сложность для каждого из 1–4 отображаемых мест, сохраняя 2–8 участников, команды и дополнительных неотображаемых ботов. AI SHALL играть по существующим честным правилам независимо от наличия viewport.

#### Scenario: Один человек проверяет несколько экранов
- **WHEN** пользователь выбирает один human и 1/2/3 AI views
- **THEN** устройство требуется только человеку, AI самостоятельно двигается и сражается, layout содержит 2/3/4 viewport

#### Scenario: Все места AI
- **WHEN** пользователь назначает все отображаемые места AI
- **THEN** валидный матч запускается без игровых устройств и каждый AI сохраняет свою сложность и команду

#### Scenario: Изменение состава
- **WHEN** пользователь переключает human в AI или уменьшает число мест в setup
- **THEN** освобождаются соответствующие устройства, не удаляются дополнительные боты и сохраняется общий лимит

### Requirement: Единая first-person presentation и lifecycle

AI view SHALL использовать обычную камеру, руки, HUD, live/death/killcam/respawn и существующие trooper v2 idle/walk/run/aim/fire/hit/death без root motion и второго presentation path.

#### Scenario: Смерть отображаемого AI
- **WHEN** AI погибает в общем combat
- **THEN** его экран показывает обычную death/killcam и возвращается к first-person после respawn; остальные views продолжаются

### Requirement: Независимый оператор и frozen lifecycle

Оператор SHALL иметь доступ к pause/resume/Repeat/exit и меню даже при all-AI; назначенные human devices сохраняют disconnect/reconnect и focus protection. Отсутствующий AI device SHALL NOT блокировать матч. Repeat SHALL сохранять composition, сложности, команды, seed и профили.

#### Scenario: AI-only pause and repeat
- **WHEN** оператор нажимает Escape в AI-only матче, затем Resume либо Repeat
- **THEN** пауза останавливает session/AI clocks, Resume продолжает бой, Repeat создаёт чистый матч с тем же frozen составом

#### Scenario: Human disconnect
- **WHEN** отключается устройство human-места среди AI views
- **THEN** общий матч останавливается до reconnect и явного Resume либо выхода; AI не требует устройства

#### Scenario: Возврат в setup
- **WHEN** оператор выходит из AI-only матча
- **THEN** bot driver старого матча освобождён, draft human/AI, сложности и команды сохранены, меню доступно мышью и клавиатурой
