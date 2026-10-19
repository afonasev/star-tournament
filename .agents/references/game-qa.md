# Game QA

**Актуальная политика 2026-10-08:** [локальная разработка](local-development.md). Проверки выбираются по поведению; Player build, standalone smoke и поставка не являются автоматическими gates каждой фичи. Упоминания обязательной сборки/Player ниже действуют только для явно заказанной сборки/приёмки или обоснованной минимальной QA-сборки. Tooling, не меняющий Unity runtime, не требует реального Unity запуска ради одного маршрута команд.


Прочитай этот файл перед player-visible, input, split-screen, network-play, audio или browser-QA задачей.

Для Unity набор автоматических проверок выбирается по `qa-scope.md`: UI-only change допускает `make check-ui` и tests затронутого экрана без полного bot-прогона. Для видимых изменений проверяй изменённые состояния в Editor Play Mode и сохраняй screenshots. Player build/playtest выполняй только по актуальной политике локальной разработки. Focus/cursor capture, четыре независимых устройства, reconnect и TV проверяются физически; automated input и четыре камеры не заменяют эту приёмку. Output Full HD–4K и минимум 60 FPS для четырёх игроков — целевой контракт, reference hardware/quality ещё открыты. Benchmark на текущем Mac маркируется diagnostic до согласования условий. Browser-правила ниже продолжают описывать historical browser runtime, а закрытый cancelled/superseded backlog не возобновляется ради Unity.

- Для текущей Unity-версии проверяй изменённые input/focus/split-screen состояния через соответствующие native fixtures и Editor Play Mode; Player — по политике локальной разработки. Browser-QA ниже применима только к historical browser runtime и не требует возобновлять его. Сборка и автотесты не заменяют человеческую проверку играбельности.
- Для видимого результата Unity приложи актуальные реальные screenshots изменённых состояний из проверенного Editor Play Mode выбранной ветки; для historical browser evidence — из соответствующего dev-стенда. Покажи изменённые экраны/viewport, не выдавай screenshots за input/device acceptance. Для передачи визуальной итерации используй пакет из context-efficiency.md.
- Любой запуск без тестирования человеком, включая автоматические аудиотесты и browser playtest, проводи без звука с самого старта по правилу AGENTS.md. Звук допустим только в рамках предусмотренного человеческого теста.
