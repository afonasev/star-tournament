# Affected проверки интеграции

Baseline main f8a54b7e, alignment d9cd23ed, release source 5f1b2909. После сверки Runtime/Editor/Resources/Tests/distribution совпадают с release source; новые Well и правила main сохранены трёхсторонним объединением.

Выполняются полные изменённые EditMode/PlayMode fixtures из reconciliation.json и LabSnapshotCopyTests для общей границы хранения. Scene fixtures исполняют настоящий Editor Play Mode. Не затронутые AI/physics/natural matches, full suite и сборки не запускаются. Все Unity-команды используют shared runner, -noaudio -muted и отдельную Lab history. PlayMode использует графику для настоящих Canvas PNG; системный экран, фокус и устройства не используются.

Distribution: изменённые Python/JS модули и соседние packaging/custody contracts; режимы launcher recovery/broker/compatibility/discovery, без native установки/UAC и упаковки приложения. Maintenance проверяется компиляцией исходников, установщик не создаётся. Assertions/seeds/timeouts не меняются. Проверка whitespace source проходит; пробелы в исторических XML/логах сохранены.

Первичный Python прогон сохранил FAIL/ERROR/skip. Ошибки среды: Pillow отсутствовал, dotnet не был в PATH; создан собственный venv с pinned Pillow11.3.0, задан настоящий DOTNET. В fixture changelog добавлен обязательный compatibility PKG, чтобы исходный assertion снова проверял Git revision. Повторены только затронутые модули. Первоначальный broker-consumer skip закрыт отдельным запуском с собранной настоящей C# DLL. Нативные платформенные границы остаются NotRun по итогам соответствующих contracts.

Первичный PlayMode81/86: transient no-space setup failure (после выхода Editor14GiB свободно; чужие ресурсы не удаляются), старые assumptions по Well/grid, source/cache fixture, список карт без Well и hidden hint RectTransform+50px. Runtime-правка только начальных offsetMin подсказки. Повтор:4 упавших EditMode cases + MenuActionHintTests;5 упавших PlayMode cases + полный LabDiscardWarningTests и два соседних hint/handover contracts. Остальные PASS не повторяются: их исходники не затронуты.
