# 20 — Unity: native layouts и состав 2–4 local seats

Статус: реализация, automated и muted native Player проверки завершены; отдельный commit. Change `add-unity-native-seat-layouts`, ветка `codex/unity-native-seat-layouts`.

## База и порядок

Новый worktree b03b создан от чистого main 26ce3ff. Проверены status, ancestry и git cherry: пять native-коммитов отсутствовали; ветка создана на 001824b. Main и старые worktrees не редактировались. Следующий префикс 20 проверен по active/completed.

Layouts выбраны до teams/bots: они снимают фиксированные четыре bindings и дают будущим смешанным roster независимый выбор числа человеческих экранов. Текущий срез ограничен 2–4 людьми. Один viewport утверждён, но поведение одиночного матча без native bots не определено; вопрос пользователю отправлен. Скрытые диагностические соперники не добавляются. Этап 2 не завершён.

## Контракт

Shared LocalSeatLayout определяет cameras/HUD, два left/right либо 2×2. При трёх местах нижняя правая ячейка — непрозрачный read-only счёт без камеры и игровых устройств; Tab/View остаётся независимым, включая killcam.

Сцена сохраняет пул четырёх motor/camera/GLB owners. Неактивные motor roots/capsules/viewmodels отключены; session/presentation/actions получают ровно N элементов. В данном human-only срезе seat i=participant i; это не общий bot/network binding API. NativeMatchState/scoring/profile остаются прежними.

Setup изменяет число мест; shrink освобождает только удалённые bindings, grow требует joins. Repeat сохраняет число и assignments с чистыми timers/lives/score/input/corpses. Disconnect называет место, reconnect требует Resume. Старый CLI probe действует только до выхода в menu и не влияет на следующий состав.

## Проверки и оставшиеся gates

Read-only Astra review до и после реализации. Исправлена утечка CLI probe в следующий setup; отдельный regression. EditMode 28/28, PlayMode 24/24 PASS. Проверены точные roster/capsules/cameras/HUD, 4→2→3→4, stale rows, killcam View и live scores, pause/results Repeat, frozen lifecycle, extra-device isolation, disconnect/reconnect/focus, удаление keyboard/mouse seat и обе CLI probe регрессии. Первый PlayMode startup прерван Unity Bee closed-pipe до тестов; чистый повтор и финальный прогон прошли. Mac Development build PASS (324813891 bytes). Muted native Player: 21 состояния в FHD и 21 в 4K, точные output размеры и scene owners; обычный setup/keyboard join/diagnostic/pause/Repeat/menu/Exit проверены отдельно через native UI. [Evidence, ограничения и hashes](../../evidence/unity-native-layouts-2026-09-19/README.md). Полные XML/logs — `.local/unity-evidence/layout-final/`. Strict OpenSpec validation PASS.

4K screenshots содержат FPS ниже 60: это функциональный capture с PNG readback, а не performance gate. Первый 4K запуск остался ждать focus и исключён; успешный повтор полностью focused/muted. Его duration 6 min выбран через setup при фокусировке окна, FHD duration 5 min; профиль не менялся.

Physical devices/TV, reference hardware/quality/internal scale и длительный foreground performance остаются открытыми. Synthetic input и PNG не заменяют физическую приёмку и минимум 60 FPS. Integration/archive/deploy не выполняются.
