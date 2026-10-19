# Q9/30: основа DMG, без упаковки установленного кандидата

Baseline `codex/game-standard-alignment@670e7f38`. Макет30 согласован ранее. Реализованы input admission и Finder layout builder в distribution/macos_dmg.py. Это предпосылка Q9, не готовый first-install pipeline. package.py/collector/landing пока всё ещё используют PKG; задача4.1 и integration4.2 остаются открытыми.

## Реализовано

- Принимается complete outer Velopack .app с launcher, UpdateMac, nested Player, release.json, Resources/sq.version и точным pinned update key. Неправильная release identity/архитектура метаданных/версия/OS в Info.plist, entrypoint Player вместо launcher, неисполняемые компоненты, escaping links и traversal отклоняются. Внутренние ссылки допускаются. Это не PE/Mach-O architecture proof и не криптографическая проверка всего bundle.
- Builder сохраняет входные bytes/links, копирует полный bundle как Star Tournament.app и создаёт symlink /Applications. DS_Store/settings dmgbuild1.6.7 задают две позиции icon-view, фон по30 и действительные config metadata. Фон не содержит поддельных иконок: их рисует Finder.
- Выход immutable, collision в начале и во время build не затирает чужой файл; временный stage удаляется при success/failure. Требования интерпретатора будущей упаковки: distribution/macos/requirements.txt; они не установлены глобально.
- background-preview.png — PNG того же renderer с fixture macOS12.0/arm64; просмотрен. Это только фон будущего Finder, не screenshot Finder/DMG. Иконки/подписи должны появиться из actual bundle и /Applications при отдельной native проверке.

## QA

План/команда до запуска: plan.md. Выполнено9/9 Python unittest, tests-final.log, без Unity/Player. Recording callback проверяет реальные settings/PNG/no-overwrite/unchanged-input, но записывает явно fixture bytes; это НЕ настоящий DMG. Initial8=7pass/1fail: fixture ожидал /var вместо canonical /private/var; исправлен fixture .resolve(), контракт не ослаблен. При добавлении path assertion был ошибочно перемещён fixture cleanup (NameError), сохранён path-fixture-error.log; corrected9/9. Все runtime assertions сохранены.

Официальные источники: [Velopack macOS](https://docs.velopack.io/packaging/operating-systems/macos) описывает portable ZIP→DMG с сохранением update-capable .app; [dmgbuild settings](https://dmgbuild.readthedocs.io/en/latest/settings.html) задаёт rename tuple, Applications symlink, icon locations/window/background. Current CLI docs показывают1.2.161, поэтому pinned1.2.158 флаги/реальный plist/sq.version ещё нужно сверить по его источникам перед подключением. Context7 lookup завершился fetch failed, использован официальный fallback.

## Продолжение без уменьшения scope

1. Проверить pinned1.2.158 portable zip/app metadata, создать нужный внешний plist с LSMinimumSystemVersion до упаковки/signing; нельзя постфактум менять подписанный bundle. Извлекать bundle с проверкой всех ZIP paths/symlinks и сохранением modes.
2. Подключить builder к package.py, skip PKG при поддерживаемом pinned flag, добавить .dmg в custody. Collector/landing выбирают modern DMG, сохраняя immutable historical PKG и bridge pair invariants. Проверить запись маршрутов без действительной сборки/публикации.
3. После отдельной команды на installer build — actual DMG/Finder→Applications/Spotlight/first launch/minOS/Gatekeeper/update/user-data gates. Пока NotRun; сам этот модуль не является их доказательством.
4. Полная цель сохраняет обязательный automatic Windows0.1.5/test.1 bootstrap/broker/handoff/recovery/native proof; выпуск ждёт проверки, manual installer exception нет. Canonical specs/integration/full matrix/human acceptance также остаются открыты.

Owned render venv/scratch27M удаляются после сохранения доказательств; active worktree нужен общей задаче. Installer/Player/full/build/deploy не запускались. В main кода этот срез не интегрирован.
