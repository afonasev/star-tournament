# 25 — Unity: физическая навигация бота

Статус: реализован и проверен для отдельного коммита `add-unity-bot-navigation`. Ветка `codex/unity-bot-navigation`, worktree b6fa, база `6f8ed15`. Исходная verified ветка `codex/unity-native-bots` сохранена.

Полная цель остаётся активна: `docs/UNITY_MIGRATION_MATRIX.md`. Следующие changes продолжаются в этой задаче отдельными worktrees/commits; без нового запроса не создавать sidebar tasks.

## Реализация

Native route adapter физически проверяет опоры/headroom и declared stairs/ramp; data-only controller выдаёт обычные LocalAction в CharacterMotor. Честные goals приходят из скопированных per-observer knowledge. Ограниченные recovery, transition-exit lock, independent validated snapshot, профиль `unity-bot-navigation-v1@1` с metadata. Motor/collision не ослаблены.

Development journey подключён к настоящему advancing session tick. Pause не двигает часы/намерение, смерть очищает цель, Repeat создаёт fresh driver с frozen profiles, menu освобождает его. Shipping planner/setup пока отсутствует.

Astra architecture и повторные read-only reviews: clock/recovery/Arrived/transition/lifecycle замечания исправлены, regressions добавлены. Последний review не нашёл блокеров.

## Проверка

- EditMode **61/61**, PlayMode **44/44**, Mac Development build **PASS**.
- Stairs/ramp × обе стороны × три lanes; mid-transition restore/reversal, overlap-XZ, partial routes, чужая поверхность, living blocker и narrow corridor.
- FHD/4K по **16 PNG/JSON**, muted/focused; все просмотрены. Ordinary Player CUA setup/teams/diagnostic/pause/Repeat/menu/Exit прошёл.
- FHD frame p95 9.611 ms / worst 84.275; 4K p95 9.532 / worst 237.175. Route query p95 0.1167/0.1198 ms; 4K worst 10.4017 ms. Короткая диагностика, не target 60 FPS acceptance.
- Первый startup stall и заменённые measurement reports сохранены; GPU/allocation counters недоступны, причины редких задержек остаются открытыми.

[Evidence и build identity](../../evidence/unity-bot-navigation-2026-09-20/README.md). Полный архив: `/Users/eaafonasev/Documents/Codex/qa-vault/star-tournament/bot-navigation-2026-09-20`.

## Следующий срез и gates

Playable planner/combat/support/три difficulty, participant/local-seat separation, bot setup, solo/8 и independent evaluation ещё обязательны. Physical gamepads/TV, trooper art, reference hardware/quality/internal scale, long foreground 60 FPS и прежний unexplained FHD spike открыты. Navigation snapshot не определяет полный Unity replay. Main/archive/deploy не выполнялись; полная миграция не завершена.
