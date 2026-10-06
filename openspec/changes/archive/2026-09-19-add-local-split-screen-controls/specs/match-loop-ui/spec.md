## MODIFIED Requirements

### Requirement: Предматчевая настройка
Главное меню SHALL позволять собрать configuration режима FFA/две команды, roster из 1–4 local seats с явными bindings keyboard/mouse либо подключённых gamepad и ботов до общего лимита восьми, имена, FFA colors либо Team A/Team B, длительность и nullable score target. UI MUST показывать занятость/недоступность устройств и MUST NOT запускать невалидную configuration.

#### Scenario: Новый матч по умолчанию
- **WHEN** пользователь открывает меню без draft
- **THEN** setup показывает один local seat, выключенную score target и доступные устройства до Start

#### Scenario: Добавление local seat
- **WHEN** пользователь добавляет local seat и выбирает свободный gamepad
- **THEN** изменяется только эта карточка и Start остаётся доступен лишь при уникальных bindings

#### Scenario: Цель включена
- **WHEN** пользователь включает score target
- **THEN** поле сохраняет profile-defined default, range и step

#### Scenario: Командная ошибка
- **WHEN** одна команда пуста либо participant не назначен ровно одной команде
- **THEN** Start блокируется, а DOM показывает локальную понятную причину

#### Scenario: Добавление и удаление
- **WHEN** пользователь добавляет либо удаляет бота
- **THEN** редактируется только выбранный roster entry, общий лимит равен восьми и bindings local seats не меняются

#### Scenario: Индивидуальная сложность
- **WHEN** пользователь выбирает difficulty для одного бота
- **THEN** сложность остальных не меняется, а Start и Repeat сохраняют exact value

### Requirement: Match HUD и overtime
Во время running каждый local viewport SHALL показывать собственные health/weapon/ammo surfaces и общий remaining match time. В overtime timer SHALL показывать явный `OVERTIME`; при трёх local seats свободная ячейка 2×2 SHALL постоянно показывать live-счёт.

#### Scenario: Три viewport
- **WHEN** simulation публикует running snapshot для трёх local seats
- **THEN** три одинаковых HUD принадлежат соответствующим seats, а четвёртая ячейка обновляет счёт из snapshot

#### Scenario: Countdown
- **WHEN** simulation публикует running snapshot
- **THEN** каждый HUD показывает время из remaining ticks без собственного wall-clock countdown

#### Scenario: Overtime
- **WHEN** snapshot phase равна `overtime`
- **THEN** HUD явно показывает `OVERTIME`, а gameplay остаётся активным до единоличного лидера

### Requirement: Live scoreboard по удержанию
Удерживание `Tab` назначенным keyboard/mouse seat либо `View` назначенным gamepad seat SHALL показывать поверх соответствующего playfield live standings, а отпускание SHALL скрывать их. Scoreboard MUST использовать simulation projection, не пересчитывать statistics и не приостанавливать матч.

#### Scenario: Удерживание View
- **WHEN** gamepad seat удерживает и отпускает `View`
- **THEN** его scoreboard видим только во время удержания без gameplay tick mutation

#### Scenario: Удержание и отпускание Tab
- **WHEN** keyboard/mouse seat удерживает `Tab` во время running, overtime либо killcam
- **THEN** scoreboard остаётся видимым всё время удержания и скрывается после отпускания без pause

#### Scenario: Командная таблица
- **WHEN** режим равен teams
- **THEN** Team A и Team B показывают totals, authoritative colors и stable participant order

#### Scenario: Читаемость строк
- **WHEN** standings содержит лидера и local-seat участника
- **THEN** числовые колонки выровнены, лидер использует participant color, а человек получает мягкий фон и метку `ИГРОК`

#### Scenario: Округление урона
- **WHEN** projection содержит дробные dealt либо received values
- **THEN** UI показывает ближайшие целые без изменения authoritative statistics

#### Scenario: Потеря focus
- **WHEN** pointer lock/focus теряется при удерживаемом scoreboard
- **THEN** held state очищается вместе с gameplay input и не остаётся stuck после resume

### Requirement: Pause menu и pointer lock
`Escape`, `Menu/Start`, disconnect назначенного gamepad либо потеря pointer lock/fullscreen SHALL остановить общий local match и открыть pause menu. Pause MUST объяснить причину и затронутый seat, предлагать resume после восстановления устройства, repeat exact configuration, settings и выход в главное меню.

#### Scenario: Gamepad disconnect
- **WHEN** назначенный controller отключается во время running
- **THEN** input очищается, ticks останавливаются и pause surface называет seat и controller

#### Scenario: Escape получен
- **WHEN** browser доставляет `Escape` во время local match
- **THEN** input очищается, ticks останавливаются и открывается pause menu

#### Scenario: Pointer lock потерян без Escape
- **WHEN** browser снимает pointer lock или document теряет focus
- **THEN** runtime достигает paused state без зависимости от key event

#### Scenario: Settings без audio consumer
- **WHEN** пользователь открывает settings в этом slice
- **THEN** fullscreen control работает, а UI не заявляет применённую аудиосистему
