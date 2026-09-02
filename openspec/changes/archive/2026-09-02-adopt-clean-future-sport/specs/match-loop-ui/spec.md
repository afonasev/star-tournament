## MODIFIED Requirements

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
