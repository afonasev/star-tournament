# Q9/30: DMG producer и download pipeline

Baseline `codex/game-standard-alignment@3fffd63e`. Согласованный30, без нового визуального направления. Это source/fixture milestone: настоящий DMG, Player или установленная версия не собирались/не запускались; публикация не выполнялась.

## Изменения

`package.py` получает внешний plist с minimumOS из настоящего входного Unity Player, сохраняет pinned Velopack default identifier, short-core/full CFBundleVersion и launcher entrypoint. Pinned1.2.158 `--noInst --plist` создаёт complete portable и update nupkg, исключая PKG. Проверка всего ZIP namespace/symlink graph предшествует ditto extraction. DMG builder получает целый bundle; portable ZIP удаляется после successful first-install упаковки. `--update-only` не создаёт DMG и удаляет first-install leftovers. Custody включает versioned .dmg.

Modern collector требует DMG, проверяет его exact custody. Опубликованный bootstrap допускает immutable исторический PKG либо DMG; присутствующий испорченный DMG не заменяется PKG. Подписанные metadata/legacy alias bytes не переписываются. Лендинг выбирает DMG для modern-only пары, допускает PKG для complete historical legacy пары и показывает инструкцию по выбранному формату. Candidate uploader проверяет DMG identity до любого сетевого действия и использует версионный installer destination. Landing markup изменён только в тексте первого шага, без перестройки дизайна.

Изученный dmgbuild1.6.7 core вызывает ditto без проверки exit code. Поэтому source wrapper теперь до отпуска immutable output делает read-only/nobrowse mounted image readback: весь app tree SHA256/file+directory modes/symlinks и Applications link. Mount errors/различия останавливают выдачу. При failed detach сохраняются отдельный owned mount path и hardlink образа ВНЕ автоматически удаляемого build stage; mounted tree не удаляется. Сейчас эти hdiutil routes исполнялись только recording fixtures. Реальная команда attach/detach ещё NotRun; её успешность и реальные DMG bytes не доказаны.

## Целевые проверки

- mac-readback-final.log:16/16 — app admission/layout, version/OS/key/paths, immutable output, actual tiny ditto ZIP roundtrip, symlinks/framework links, вложенный Player и launcher xattrs, archive collisions/traversal; recording image readback/success/corruption/detach failure/retention.
- package.log:3/3 — actual package.main с recording dotnet/vpk/DMG, first-install/update-only, реальное чтение входного Player plist и правильный минимум13.2 вместо hardcoded12, exact custody.
- bridge-final.log:26/26 — реальные RSA-PSS/signed ZIP/config/custody и frozen aliases, исторический PKG, DMG bridge, modern missing/corrupt image rejection. Initial25pass/1error был fixture exception type mismatch (FileNotFoundError vs существующий Unsafe asset input ValueError); body сохранён, ожидается точный штатный отказ.
- upload.log:6/6 — до network вызовов, DMG versioned path и изменённый DMG отклоняется.
- windows-neighbor.log:2/2 — unchanged Windows first-install/update-only component recording routes.
- js-final.log:11/11 — actual selector и actual async landing function с recorded DOM/fetch для DMG и historical PKG. Не browser screenshot/визуальная приёмка.
- Итого53 unique Python +11 JS passed,0 skipped. Тесты не складываются с повторными промежуточными прогонами. No full/Unity/Player/installer/build/deploy.

## Точное ограничение upstream ZIP

Исследование actual tiny ditto archive обнаружило: `--keepParent` не сохраняет произвольный xattr ВНЕШНЕЙ корневой папки .app в ZIP. Это ограничение создаваемого Velopack формата, не успешная проверка root-xattr preservation. Первоначальное дополнительное утверждение сохранено как outer-directory-xattr-not-archived.log. Актуальная проверка доказывает xattrs вложенного Unity Player.app и launcher файла, включая AppleDouble metadata paths; Info.plist/sq.version/ключ/обычные files/modes/links сохраняются. Не заявляем общей гарантии произвольных root attrs, ACL или Gatekeeper trust. Source generated outer bundle новый; любые actual signing/OS acceptance требуют отдельного платформенного сценария. Python runner не имеет os.setxattr, поэтому используется настоящий macOS xattr CLI; прежняя runner-ошибка также сохранена.

## Основания и следующие gates

Pinned tag1.2.158=3c7f52c1bf17d10ad21b794b006d5ebd1a879a3b и inspected paths/hashes — upstream.json. Version contract проверен по OsxBundleCommandRunner, NoInst по PackCommand, layout/ZIP/sq.version по OsxPackCommandRunner/OsxBuildTools. Полный pristine download source/wheel не требуется сохранять: точные ревизии/SHA остаются.

4.1/4.2 реализованы на source уровне;4.3 NotRun. Следующий шаг: обязательный автоматический Windows0.1.5/test.1 bootstrap/broker/handoff/activation/recovery/native proof; при отдельной команде на installer build — actual DMG/first-install Finder→Applications/Spotlight/minOS/Gatekeeper/update/profile preservation. Выпуск ждёт проверки Windows auto-transition; ручной installer exception нет. Полная110-ID/30-option matrix/canonical specs/integration/native/human acceptance остаются открыты.

Owned scratch/venv/wheel/tiny ZIP/xattr fixtures удаляются после сохранения доказательств; actual volumes не монтировались. Active worktree/Library нужны полной задаче. Чужие checkout/process/cache не затронуты.
