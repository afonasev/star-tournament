# unity-native-match-loop Specification

## Purpose
Подключённый native Player проводит четырёхместный FFA матч от настройки через бой и подсчёт результата до чистого повтора на фиксированной арене.

## Requirements

### Requirement: Native scoring
Player SHALL учитывать фактически применённый damage, kills, deaths, assists текущей жизни и kill chains согласно существующему match-session-lifecycle. Окна включают точную границу; killer/victim не получают assist, повторы hits не дублируют assist. Смерть завершает серию; разрыв начинает новую без потери прежних очков. Tuning MUST принадлежать именованному профилю с общей metadata для UI/validation.

#### Scenario: Смешанный урон и серия
- **WHEN** несколько участников повреждают жизнь и последний убивает её
- **THEN** damage ограничен оставшимся health, убийца получает kill-chain delta, подходящие помощники получают по одному assist; stale-life damage не меняет счёт

### Requirement: Atomic match completion
Native матч SHALL иметь setup duration и optional target, по умолчанию выключенный, per-seat timer и finish после всех событий tick. Цель имеет приоритет на последнем regulation tick. Равенство переводит в overtime с сохранением состояния; следующий tick с единственным лидером завершает матч с исходной причиной. Finished MUST замораживать gameplay и независимый ordered result.

#### Scenario: Последний tick
- **WHEN** события последнего tick достигают target и меняют порядок лидеров
- **THEN** результаты включают все события, причина target, а равенство запускает overtime

#### Scenario: Пауза
- **WHEN** pause, focus loss либо disconnect останавливает игру
- **THEN** match/combat/death/corpse clocks и счёт не меняются; resume требует явного действия и connected assignments

### Requirement: Native standings and results
Tab/View SHALL показывать live standings в viewport вызывающего seat, в том числе killcam, с именем, kills/assists/deaths, округлённым dealt/received damage и score. Колонки выровнены, лидеры отмечены цветом, human/diagnostic строки различимы. Итоговая таблица SHALL предлагать Repeat и setup; pause также предлагает Repeat/setup. Настройки длительности/цели доступны только до матча.

#### Scenario: Killcam standings
- **WHEN** погибший удерживает View или Tab
- **THEN** его viewport показывает актуальный рейтинг без изменения других seats или camera lifecycle

### Requirement: Clean Repeat and setup
Repeat SHALL сохранять состав, device bindings, фиксированную арену и frozen настройки/profiles; новый матч начинает timer/stats/lives/ammo/input/corpses/killcam заново. Setup SHALL освобождать старую session. Невозможность запуска четырёх seats MUST оставлять доступный UI восстановления устройств.

#### Scenario: Repeat из результата и паузы
- **WHEN** игрок повторяет матч после смерти или завершения
- **THEN** новый gameplay lifecycle не содержит старых corpses/input/results и не создаёт дополнительных EventSystem/AudioListener/NavMesh; удержанный fire не стреляет автоматически

#### Scenario: Выход и новый запуск
- **WHEN** игрок возвращается в setup и запускает матч с новыми настройками
- **THEN** прежний матч не продолжается скрыто, а новая конфигурация применяется к новой session
