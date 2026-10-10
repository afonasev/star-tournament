# Минимально достаточные проверки

Решение пользователя 2026-10-10 заменяет прежние автоматические full/regression gates, включая release и production. Полный прогон всех тестов требует отдельного явного подтверждения человека. Сборка, выкатка, commit, merge и «доведи до конца» его не разрешают. `CONFIRM_FULL_TESTS=1` фиксирует полученное разрешение; агент не может выдать его себе. Не проси full после каждой правки по шаблону.

До запуска запиши baseline SHA, diff, изменённое поведение и зависимости, конкретные команды и причину выбора. Выбирай минимально достаточный набор по семантике изменений. Общие зависимости/найденная регрессия расширяют affected набор до соседних контрактов. Неизвестное влияние сначала исследуй. Если без full достаточная проверка невозможна, объясни причину и запроси отдельное подтверждение; не запускай его автоматически.

| Изменение | Выбор проверки |
| --- | --- |
| Документация | Diff, ссылки; validation только изменённых planning artifacts |
| Python/shell QA tooling, маршруты команд, XML | Соответствующие Python contract tests и recording runner; Unity не нужен |
| UI/copy/layout/menu | Fixtures изменённого экрана/состояний; `make check-ui` — удобный layout/menu/pause набор, добавляй нужные экранные контракты |
| Gameplay/input/balance | Affected fixtures и необходимые соседние контракты; Unity для API/lifecycle/physics/input |
| Общая simulation/serialization/input ownership, engine/packages, регрессия | Разбор зависимостей и расширенный affected набор; full только после отдельного подтверждения |
| Release/production/широкая интеграция | QA по фактическому diff от проверенного baseline и взаимодействиям; никаких автоматических full gates |

Focused fixtures/методы — полноценные доказательства выбранного scope, но не проверка всей игры. Не выбирай все UI fixtures для простой правки подписи по привычке. `make check`/`check-local` — alias UI-набора без сборки; эти команды не обязательны для docs/tooling. `make check-tooling` проверяет маршруты QA и XML без Unity. Для конкретного tooling diff выбирай соответствующие модули из него.

`make check-ui`: EditMode `LocalSeatLayoutTests`; PlayMode `FourPlayerMenuTests;IndependentPauseMenuTests`. Нет natural bot matches. Для scoreboard добавь `NativeScoreboardLayoutTests`, для Lab — соответствующие workspace/history tests, для roster — roster tests. Меняющийся roster/start/input mapping требует соответствующих gameplay контрактов. Длительные natural matches выбирай отдельно при изменении прогресса/исхода/завершения матча или долгосрочного AI-поведения.

`make test`, `test-edit`, `test-play`, `check-full` и прямой `tools/unity.sh ... -runTests` без фильтра отказывают без `CONFIRM_FULL_TESTS=1`. Не обходи правило через прямой Editor/CI или фильтр, эквивалентный всем тестам. `make check-player` делает только явно заказанную Development-сборку; affected QA выбирается отдельно. Перед составной командой прочитай фактический маршрут. Player/release/deploy регулируются [локальной разработкой](local-development.md).

Тестовые wrappers по умолчанию задают новый `STAR_TOURNAMENT_QA_LAB_HISTORY` внутри `.local/unity-evidence/` каждого запуска. Общие scene fixtures не читают пользовательскую Lab-историю. Явный override сохраняется: для миграции/старой/большой истории используй принадлежащую задаче копию; не подменяй её пустой историей. Editor, build и установленная игра используют прежние пути. Исторические C#-тесты сами создают свои данные; их assertions/hash/replay contracts не меняются.

Повторяй QA после относящихся к ней изменений/ошибок. Тот же source snapshot после commit или metadata не требует повторения. После merge оцени новые зависимости/конфликты, повтори только затронутые проверки. Не пересоздавай worktree/Library между итерациями, не копируй и не чисти чужие caches.

Разделяй admission/queue, startup/import/compile, setup/body/teardown, build. XML duration не равна времени только тестовых тел; вложенные suite duration не суммируй с родителями. Логи загрузки Lab могут описывать setup внутри XML, их нельзя прибавлять ещё раз. Missing/empty/skipped XML, admission timeout и ноль выполненных тестов — NotRun/неполная проверка, а не зелёный результат. Assertion failure — отдельная категория. Не сокращай seeds/длительность/assertions/exact byte equality и не увеличивай timeout ради green.

Unity-free C# возможен только при запуске настоящего исходного кода существующим независимым runner. Переписывание логики на Python, моки всей Unity API и текстовый поиск не доказывают выполнение C#. Unity Input System, JsonUtility/Resources, lifecycle/physics/render остаются Unity-зависимыми. Automated evidence не заменяет physical/device/visual/audio/human acceptance.
