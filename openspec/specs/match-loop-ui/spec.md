# match-loop-ui Specification

## Purpose

Capability задаёт доступный DOM-flow базового single-seat матча: настройку, игровой таймер, live scoreboard, pause и итоговую таблицу без передачи gameplay authority интерфейсу.

## Requirements

### Requirement: Вход в лабораторию и выбранный профиль нового матча
Главное меню SHALL предоставлять действие открытия Game Design Lab и SHALL отображать identity выбранной сохранённой game-design revision до запуска матча. Предматчевый Start MUST использовать именно эту revision либо понятным образом блокироваться, если revision недоступна/невалидна; settings и graphics controls сохраняют presentation-only contract.

#### Scenario: Возврат из лаборатории
- **WHEN** пользователь выбирает valid revision и возвращается из лаборатории в главное меню
- **THEN** меню показывает выбранные profile id/revision/content hash, а последующий Start создаёт матч с этой exact identity

#### Scenario: Недоступный профиль
- **WHEN** выбранная revision больше недоступна или невалидна
- **THEN** меню не запускает матч, объясняет проблему и не подменяет revision без явного выбора пользователя

### Requirement: Предматчевая настройка
Главное меню SHALL позволять собрать поддерживаемую configuration: режим FFA/две команды, roster из local keyboard/mouse участника и 1–7 ботов с индивидуальной сложностью, имена, индивидуальные FFA colors либо назначение Team A/Team B, длительность 1–30 минут и nullable score target. UI MUST получать допустимые numeric range/step/default из descriptor registry и MUST NOT запускать невалидную configuration.

#### Scenario: Новый матч по умолчанию
- **WHEN** пользователь открывает меню без сохранённого draft
- **THEN** score target выключен, длительность равна profile default, roster валиден для FFA и кнопка старта объясняет либо запускает exact configuration

#### Scenario: Цель включена
- **WHEN** пользователь включает score target
- **THEN** поле получает 3000 и принимает только 1000–20 000 с шагом 100

#### Scenario: Командная ошибка
- **WHEN** одна команда пуста либо участник не назначен ровно одной команде
- **THEN** старт заблокирован, а DOM показывает локальную понятную причину

#### Scenario: Добавление и удаление
- **WHEN** пользователь добавляет либо удаляет бота
- **THEN** редактируется только выбранный roster entry, общий лимит равен восьми; невалидный состав блокирует старт с объяснением

#### Scenario: Индивидуальная сложность
- **WHEN** пользователь выбирает Салага, Боец либо Ветеран для одного бота
- **THEN** сложность остальных не меняется, а старт и Repeat сохраняют точные выбранные значения

### Requirement: Match HUD и overtime
Во время running single viewport SHALL сохранять текущие health/weapon/ammo surfaces и показывать сверху по центру remaining match time. В overtime timer SHALL показывать явный `OVERTIME`, не продолжая отрицательный countdown.

#### Scenario: Countdown
- **WHEN** simulation публикует running snapshot
- **THEN** HUD показывает время, выведенное из remaining ticks, без собственного wall-clock countdown

#### Scenario: Overtime
- **WHEN** snapshot phase равна `overtime`
- **THEN** HUD явно показывает overtime и текущий gameplay остаётся активным до единоличного лидера

### Requirement: Live scoreboard по удержанию
При активном local seat удерживание `Tab` SHALL показывать поверх playfield live standings, а отпускание SHALL скрывать их. FFA SHALL сортировать игроков по score с детерминированным tie-break и показывать kills, assists, deaths, dealt, received и score; teams SHALL группировать Team A/Team B и показывать team totals и те же player columns. UI SHALL округлять dealt/received до ближайшего целого только при отображении, сохраняя точные значения projection. Все секции SHALL использовать одинаковую выровненную сетку колонок. Лидер SHALL выделяться authoritative participant color, а local-seat люди SHALL получать ненавязчивую фоновую подсветку и метку, отличающую их от stationary fixtures и будущих bots. Scoreboard MUST отображать simulation projection и не пересчитывать statistics.

#### Scenario: Удержание и отпускание Tab
- **WHEN** пользователь удерживает `Tab` во время running, overtime либо killcam
- **THEN** scoreboard остаётся видимым всё время удержания и скрывается после отпускания без pause или gameplay tick mutation

#### Scenario: Командная таблица
- **WHEN** режим равен teams
- **THEN** sections Team A и Team B имеют team totals, используют контрастные authoritative team colors, а участники каждой команды упорядочены стабильно по score и semantic id

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
- **THEN** results группируют обе команды, показывают totals, выделяют winning team её authoritative color и называют победителя `Team A` либо `Team B` без внутреннего team id

#### Scenario: Повтор или меню
- **WHEN** пользователь выбирает repeat либо main menu
- **THEN** UI соответственно создаёт новый match runtime с exact configuration либо dispose старого runtime и возвращает editable pre-match surface

### Requirement: Выбор arena size и seed до матча
Предматчевый DOM setup SHALL предоставлять выбор `small`, `medium` либо `large`, где `medium` выбран по умолчанию, и необязательный ручной unsigned seed. Пустое seed field SHALL означать auto generation; UI MUST показать resolved seed и arena identity после успешного старта.

#### Scenario: Auto seed
- **WHEN** пользователь оставляет seed пустым и запускает валидную configuration
- **THEN** shell разрешает новый concrete seed, генерирует выбранный size и сохраняет exact identity сессии

#### Scenario: Ручной seed
- **WHEN** пользователь вводит допустимый seed и запускает матч
- **THEN** runtime использует именно этот seed и тот же size/version/profile reproduces arena content hash

#### Scenario: Неверный seed
- **WHEN** seed не является поддерживаемым unsigned integer
- **THEN** старт блокируется, а поле показывает локальную понятную ошибку

### Requirement: Actionable generation error
Если generation либо validation не завершается accepted arena, setup MUST остаться DOM-surface без gameplay ticks и показать seed, стабильную причину и действия retry, new seed и explicit fallback.

#### Scenario: Generation failure
- **WHEN** все bounded attempts отклонены
- **THEN** UI не создаёт частичный collision/renderer runtime и позволяет пользователю выбрать следующее действие

#### Scenario: Fallback подтверждён
- **WHEN** пользователь явно выбирает fallback
- **THEN** UI запускает shipped fallback и показывает её фактическую identity

### Requirement: Сложность в имени бота
Live и итоговая таблицы SHALL отображать bot name как `Имя · Салага`, `Имя · Боец` либо `Имя · Ветеран` на основе сохранённой difficulty. Base nickname SHALL храниться отдельно от отображаемой подписи; повторное открытие таблицы MUST NOT добавлять суффикс повторно.

#### Scenario: Смешанная таблица
- **WHEN** FFA либо teams содержит человека и ботов разных уровней
- **THEN** каждая строка бота показывает свою сложность, человек сохраняет метку ИГРОК, а колонки остаются выровненными

#### Scenario: Итог и Repeat
- **WHEN** матч завершился и затем повторён
- **THEN** результаты и новый roster сохраняют выбранные сложности без потери или дублирования подписи
