# Scope Unity QA — 2026-10-01

Добавлен `make check-ui`: 3 LocalSeatLayout EditMode + 7 FourPlayerMenu/IndependentPauseMenu PlayMode. Реальный Unity run: 10/10 Passed; PlayMode test duration 19.386 s (не включает запуск Editor/import/ожидание host). Natural bot matches не запускались. Runtime tree совпадает с baseline 9743b12, изменены только tooling/docs. Проверены XML всех fixtures, минимум 3/3/4 case; пустые/failed/skipped/неполные XML дают failure.

6 Python contract tests Passed: 3 существующих shell route tests + 3 UI validator/scope tests. `--plan` не запускает Unity. `git diff --check` и strict OpenSpec validation Passed. Validator minimum counts добавлены после Unity run и проверены на тех же retained XML без повторного запуска.

Полные combat tests не сокращались и не менялись. Для этой tooling правки нет runtime/visible изменения: Player rebuild/screenshots не требуются. Для будущего UI-only изменения они обязательны вместе с tests конкретного экрана. Политика: `.agents/references/qa-scope.md`; project profile выбирает gate по scope, default full.

XML/log/scope и result.json сохранены здесь до удаления worktree; старые absolute paths в Unity logs — provenance исходного запуска. UI gate JSON с hashes текущих runner files связывает результат с tooling. Приёмка пользователя пока не записана; предыдущий change speed-up-unity-checks остаётся отдельно.
