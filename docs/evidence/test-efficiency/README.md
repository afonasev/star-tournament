# Ускорение QA — 10 октября 2026

Baseline `dd8af9e2`; срез tooling/policy, без изменения игрового C#. Разбор последних сохранённых проверок Lab staging и menu cursor. Числа повторно извлечены из исходных XML/logs, [summary.json](summary.json) хранит пути и точные значения. Исторические speed-up/Lab отчёты использовались для направления поиска; старые обязательные full gates отменены решением пользователя 2026-10-10.

## Что измерено

| Сохранённый прогон | Результат | XML duration |
| --- | --- | --- |
| Lab staging EditMode | 427/427 | 949,75 с (15:50) |
| Lab staging graphics PlayMode с отдельной QA-историей | 252/252 | 989,36 с (16:29) |
| Menu cursor: menu/pause/settings/setup | 25 passed, 1 failed; последующий исправленный сценарий прошёл | 152,37 с |
| Menu cursor: integration affected | 3/3 | 31,98 с |

Крупные EditMode fixtures: `DesignLabHistoryTests` 345,28 с; `LabReleaseCatalogTests` 179,93 с; `LunarLaboratoryTests` 162,38 с; `WeaponSwitchPresentationTests` 109,26 с. Самый дорогой case — staging `ExplicitStagingRetainsProfilesAndPreservesSourceHistory`, 154,60 с, прежний timeout 180 с сохранён предыдущим исправлением. PlayMode: `NativeBotBehaviorTests` 133,04 с; `HitFeedbackPresentationTests` 84,93 с; `NativeMatchIntegrationTests` 52,73 с. Два natural terminal cases занимают 40,55 и 34,75 с; сокращать матч/seed/assertions ради времени нельзя.

Суммы leaf test cases — 949,60 / 989,16 с. Они почти равны root XML duration: parent fixture/suite нельзя добавлять повторно. Эти значения могут включать fixture lifecycle/setup, а не только тела assertions. XML не измеряет guardian admission/queue или весь startup/import/compile.

Подтверждённая лишняя работа: в остановленном общем PlayMode восемь завершённых загрузок пользовательской Lab-истории заняли **759,71 с** (91,29–107,21 с каждая); следующая ещё не завершилась. Это diagnostic/NotRun, не assertion failure. В успешном graphics PlayMode с отдельной историей 116 завершённых загрузок заняли 149,64 с, обычно ~1,1 с, максимум 12,46 с. В integration cursor три загрузки — 20,56 с (первый cold load 17,99 с, остальные около 1,2 с). Эти startup сообщения могут лежать внутри XML setup, добавлять их поверх XML нельзя. Это разные прогоны/fixtures, не контролируемый before/after benchmark нового патча.

Логи initial Asset Pipeline Refresh успешных прогонов показывают 6,00 с EditMode и 4,38 с PlayMode; эти строки не измеряют всю компиляцию/запуск. Guardian queue из данных XML не восстановить. Доказательств, что основной расход здесь — очередь или старт Editor, нет; повторная подготовка/история значительно заметнее. Уже сделанные runtime migration/cache ускорения не откатывались и повторно не измерялись.

## Изменения

- Отдельная явная человеческая авторизация full, включая production, закреплена в AGENTS, QA/delivery/local-development rules, README и shared planning profile. `CONFIRM_FULL_TESTS=1` лишь подтверждает существующее разрешение; агент не выдаёт его себе. Full routes отказывают до запуска runner; тот же guard у прямой Editor-обёртки без фильтра.
- `check-player` больше не включает full tests; явно заказанный build отделён от affected QA. `make test` последовательный даже с parallel make, чтобы оба Editor не использовали одну Library одновременно.
- Wrappers по умолчанию передают новый `STAR_TOURNAMENT_QA_LAB_HISTORY` на запуск. Общие сцены перестают зависеть от личной растущей истории. Явная история остаётся без подмены/перезаписи; history migration/load fixtures сохраняют собственные данные и assertions. Editor/build/игра не получают тестовый override.
- Минимальный набор выбирается по записанному baseline/diff и зависимостям. Commit/merge/metadata без изменения проверенного snapshot не вызывают повтор. Неизвестное влияние исследуется; broad impact расширяет affected, а не автоматически запускает full.

## Где Unity можно исключить

Маршруты shell/Make, разрешения полного прогона, environment/path isolation и XML проверяются настоящими существующими Python контрактами. Этот срез: `make check-tooling`, **19/19**, суммарно 2,468 с внутри трёх unittest запусков; около 2,6 с команды. Recording runner не запускает Unity. Shell syntax обоих wrappers и diff whitespace проверены. `test_flow.py` и distribution не запускались: их реализации не меняются.

Для flow/publishing/installer tooling выбирай соответствующие `tools/test_flow.py` либо `distribution/tests/test_*.py`, без UI smoke. В distribution есть существующий независимый C# executable runner `distribution/tests/Launcher.Contracts.csproj`, связанный с реальным launcher/maintenance кодом; Unity для этих контрактов не нужен. В этом срезе он не запускался.

`LocalSeatLayoutTests` использует Unity Input System/Rect, history использует `JsonUtility` и зависимые Unity профили/catalogue, scene/physics/render/input fixtures используют движок. Существующего независимого runner этих игровых C#-тестов не найдено. Нельзя просто заменить их Python-копией или mock всей Unity API. Извлечение чистой логики потребовало бы отдельного рефакторинга с настоящим C# runner и сравнением serialization/hash contracts. Поэтому дорогие migration и natural matches не переписаны без доказательства сохранения контракта.

## Границы результата

Новый runtime speedup не измерялся: для изменения shell environment/guard достаточно recording contract checks и уже сохранённого исполнения существующего runtime override. Новые Unity/Player/build/benchmark/release/deploy не запускались, assertions/seeds/durations/timeouts не ослаблены. Full и будущие измерения выбираются по фактическому изменению и соответствующему отдельному разрешению; human/device/visual/audio acceptance не заявлены.

Исходная работа и отчёт сохраняются в Git и интегрируются под integration lease. Принадлежащие задаче Python bytecode/temp copies удаляются; игровые caches и личная история не создавались. Чужие main ProjectSettings и planning delivery metadata сохраняются.
