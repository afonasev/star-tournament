# Проверки по затронутому поведению

Перед реализацией записать в change выбранный scope, изменённое поведение, затронутые зависимости и дополнительные проверки. Выбор делается по смыслу diff, а не автоматически по именам файлов. Если влияние не доказано локальным — полный gate.

| Scope | Обязательный gate |
| --- | --- |
| Документация без runtime/config changes | Проверка diff, ссылок и OpenSpec validation при изменении planning |
| Tooling тестов/сборки | Контрактные тесты tooling и реальная проверка изменённого маршрута; build только при изменении сборки |
| Локальная UI правка: подпись, оформление, layout, menu navigation | `make check-ui` + focused tests затронутого экрана + build и muted native Player smoke изменённых состояний со скриншотами |
| Бой, AI, физика, movement, spawn, матч, общий simulation state, balance/defaults, gameplay input, serialization/replay; engine/packages либо неясные зависимости | `make check` + затронутый native Player smoke; natural bot matches обязательны |
| Дистрибутивный release / широкая интеграция нескольких систем | `make check` + отдельные платформенные/физические gates по scope |

`make check-ui` — минимальный автоматический UI gate, допустимый при интеграции UI-only change. В нём нет завершённых natural bot matches: EditMode `LocalSeatLayoutTests`, PlayMode `FourPlayerMenuTests` и `IndependentPauseMenuTests`. Проверяются viewport layout, seat ownership, меню/настройки, запуск, pause/resume и независимые pause menus. Короткий запуск игрового сеанса здесь нужен для UI переходов, а не для проверки качества AI.

Базовый smoke не охватывает каждый экран: для scoreboard добавить `NativeScoreboardLayoutTests`, для Lab — соответствующие workspace/history tests, для roster — roster tests. Изменение общих параметров через Lab требует full scope; перенос подписи поля без изменения значения/metadata — UI scope. Если UI код меняет roster, команду начала матча или input mapping, проверить соответствующий контракт и выбрать full при влиянии на gameplay. Synthetic gamepads не заменяют физическую приёмку input.

`make check` остаётся полным gate. Без явно записанного scope выбирается full. Успешный UI smoke не даёт право объявлять полный gameplay gate зелёным. Full suite не запускается автоматически вслед за успешно проверенной локальной UI правкой. Произвольный `test-*-filter` остаётся focused evidence, а не универсальным gate.

Runner хранит отдельный JSON plan/result, revision, dirty state, XML/log. Отсутствующий XML, ноль тестов, пропущенный fixture, skipped/failed тест или ошибка Unity считаются ошибкой. Смена scope не сокращает, не ослабляет и не удаляет существующие bot tests. Human acceptance точного результата сохраняется.
