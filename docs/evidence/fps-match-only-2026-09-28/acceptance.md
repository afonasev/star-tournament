# FPS только в матче

Явная небольшая правка по запросу пользователя от 2026-09-28: «в меню игры не нужно показывать счетчик FPS, только в мачте». Выполнена без нового OpenSpec change по разрешённому inline-пути.

FPS скрыт в главном меню, подготовке, профилях, настройках вне матча, Balance Lab и результатах. Во время матча, включая его паузу, действует сохранённая настройка FPS. Скрытие не меняет личные или общие настройки.

## Ресурсы

- Owner: `01a0e6a3-7039-72a1-bd53-b00aee60aea3`.
- Baseline: `295e1ec9d65a433bfce71b7dfe30f7801142955f`.
- Worktree: `/Users/eaafonasev/.codex/worktrees/fps-match-only/star-tournament`.
- Branch: `codex/fps-match-only`.
- Canonical spec: planning commit `53b3640`, `player-profiles-and-match-menu`.
- Несвязанные настройки Unity в основном checkout сохранены.

## Человеческая проверка

1. Открыть Player: в главном меню нет FPS-счётчика.
2. Включить «Показывать FPS» в настройках меню: счётчик остаётся скрытым.
3. Начать матч с включённым FPS: счётчик виден в human viewport либо в общем all-AI представлении.
4. Открыть паузу: счётчик продолжает работать согласно настройке игрока.
5. Выйти в главное меню: счётчик исчезает; при следующем матче выбор применяется снова.

Human acceptance ожидается; автоматизированная проверка её не заменяет.

## Проверки и сборка

- Implementation revision: `8f92dba5ed69133ccc98b7df61b9e4a45df817b2`.
- `unity/tools.sh test-edit`: 112/112 passed.
- `unity/tools.sh test-play`: 115/115 passed, включая сохранение FPS и запрет отображения вне матча.
- `unity/tools.sh build`: exit 0, `STAR_PROVING_BUILD_PASS 469813698`. Unity 6000.3.23f1; проверки использовали существующий licensing IPC `LicenseClient-eaafonasev-6000.3.23` через локальную обёртку UNITY_EDITOR.
- Muted native Player `-pauseReview … -pauseReviewCount 1`: complete.txt PASS; main menu: visibleFps=0; running: visibleFps=1; paused: visibleFps=1; return main menu: visibleFps=0. PNG просмотрены. Synthetic devices; human acceptance pending.
- Локальный кандидат: `/Users/eaafonasev/Projects/star-tournament/.local/player-candidates/fps-match-only/8f92dba/StarTournamentProvingGround.app`.
