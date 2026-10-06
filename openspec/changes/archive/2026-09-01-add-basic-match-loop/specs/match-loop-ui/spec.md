## Purpose

Capability задаёт доступный DOM-flow базового single-seat матча: настройку, игровой таймер, live scoreboard, pause и итоговую таблицу без передачи gameplay authority интерфейсу.

## ADDED Requirements

### Requirement: Предматчевая настройка
Главное меню SHALL позволять собрать поддерживаемую configuration: режим FFA/две команды, roster из local keyboard/mouse участника и stationary mannequins, имена, индивидуальные FFA colors либо red/blue assignment, длительность 1–30 минут и nullable score target. UI MUST получать допустимые numeric range/step/default из descriptor registry и MUST NOT запускать невалидную configuration.

#### Scenario: Новый матч по умолчанию
- **WHEN** пользователь открывает меню без сохранённого draft
- **THEN** score target выключен, длительность равна profile default, roster валиден для FFA и кнопка старта объясняет либо запускает exact configuration

#### Scenario: Цель включена
- **WHEN** пользователь включает score target
- **THEN** поле получает 3000 и принимает только 1000–20 000 с шагом 100

#### Scenario: Командная ошибка
- **WHEN** одна команда пуста либо участник не назначен ровно одной команде
- **THEN** старт заблокирован, а DOM показывает локальную понятную причину

### Requirement: Match HUD и overtime
Во время running single viewport SHALL сохранять текущие health/weapon/ammo surfaces и показывать сверху по центру remaining match time. В overtime timer SHALL показывать явный `OVERTIME`, не продолжая отрицательный countdown.

#### Scenario: Countdown
- **WHEN** simulation публикует running snapshot
- **THEN** HUD показывает время, выведенное из remaining ticks, без собственного wall-clock countdown

#### Scenario: Overtime
- **WHEN** snapshot phase равна `overtime`
- **THEN** HUD явно показывает overtime и текущий gameplay остаётся активным до единоличного лидера

### Requirement: Live scoreboard по удержанию
При активном local seat удерживание `Tab` SHALL показывать поверх playfield live standings, а отпускание SHALL скрывать их. FFA SHALL сортировать игроков по score с детерминированным tie-break и показывать kills, assists, deaths, dealt, received и score; teams SHALL группировать red/blue и показывать team totals и те же player columns. UI SHALL округлять dealt/received до ближайшего целого только при отображении, сохраняя точные значения projection. Все секции SHALL использовать одинаковую выровненную сетку колонок. Лидер SHALL выделяться цветом участника, а local-seat люди SHALL получать ненавязчивую фоновую подсветку и метку, отличающую их от stationary fixtures и будущих bots. Scoreboard MUST отображать simulation projection и не пересчитывать statistics.

#### Scenario: Удержание и отпускание Tab
- **WHEN** пользователь удерживает `Tab` во время running, overtime либо killcam
- **THEN** scoreboard остаётся видимым всё время удержания и скрывается после отпускания без pause или gameplay tick mutation

#### Scenario: Командная таблица
- **WHEN** режим равен teams
- **THEN** red и blue sections имеют team totals, а участники каждой команды упорядочены стабильно по score и semantic id

#### Scenario: Читаемость строк
- **WHEN** таблица содержит лидера и local-seat участника
- **THEN** все числовые колонки остаются выровненными, лидер использует свой participant color, а человек отличается мягким фоном и меткой `ИГРОК`

#### Scenario: Округление урона
- **WHEN** standings projection содержит дробные dealt либо received values
- **THEN** таблица показывает ближайшие целые значения, не изменяя projection и authoritative statistics

#### Scenario: Потеря focus
- **WHEN** pointer lock/focus теряется при удерживаемом `Tab`
- **THEN** held scoreboard action очищается вместе с gameplay input и не остаётся stuck после resume

### Requirement: Pause menu и pointer lock
Escape, когда browser передаёт его приложению, либо потеря pointer lock/fullscreen SHALL остановить local match и открыть pause menu. Pause SHALL предлагать продолжение, повтор exact configuration, fullscreen settings и выход в главное меню. Продолжение MUST после явного действия повторно запросить нужный browser mode; failure SHALL оставить retryable pause surface.

#### Scenario: Escape получен
- **WHEN** locked runtime получает `Escape`
- **THEN** input очищается, gameplay ticks останавливаются и открывается pause menu

#### Scenario: Pointer lock потерян без Escape
- **WHEN** browser снимает pointer lock или документ теряет focus
- **THEN** runtime достигает того же paused state без зависимости от key event

#### Scenario: Settings без audio consumer
- **WHEN** пользователь открывает settings в этом slice
- **THEN** fullscreen control работает, а UI не заявляет применённой громкость/аудиосистему, которой runtime ещё не имеет

### Requirement: Итоговая таблица и навигация
После finished HUD SHALL заменить gameplay controls итоговой таблицей из immutable final standings и явно показать winner, finish trigger и действия `Повторить`/`В главное меню`. Никакое действие результата MUST NOT продолжать старый snapshot.

#### Scenario: Победитель FFA
- **WHEN** finished snapshot содержит player winner
- **THEN** results показывают победителя, final columns и finish trigger без слова «ничья»

#### Scenario: Победитель команды
- **WHEN** finished snapshot содержит team winner
- **THEN** results группируют обе команды, показывают totals, выделяют winning team и называют победителя `Красная команда` либо `Синяя команда` без внутреннего team id

#### Scenario: Повтор или меню
- **WHEN** пользователь выбирает repeat либо main menu
- **THEN** UI соответственно создаёт новый match runtime с exact configuration либо dispose старого runtime и возвращает editable pre-match surface
