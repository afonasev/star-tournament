# 27 — Unity playable bot planner

Статус: выполненный внутренний срез, готов к отдельному commit. Change `add-unity-bot-planner`, worktree `/private/tmp/star-tournament-unity-bot-planner`, branch `codex/unity-bot-planner`, base9f207d2. Astra read-only decision memo в design. Цель: реальные самостоятельные Bot-kind actions/combat, без scripted damage/targets; shipping setup отдельным следующим change. Full migration active, sidebar tasks не создавать.

Приёмка: pure honesty/state/timing tests, actual physics traversal/tactics/common weapon, mixed8 lifecycle, EditMode/PlayMode/build и focused muted FHD/4K native Player. Physical devices/TV/art/target60FPS и paired statistical evaluation остаются открытыми.

Astra implementation/re-review findings исправлены: expiry/Blocked transition exit, разные этажи, actual velocity jump predictor/air steering, snapshot temporal guards, frozen cadence. Final read-only review не нашёл блокеров в исправленных ветках; автоматические/Player checks выполняет основной агент.

Focused muted native Player gate завершён: FHD и 4K actual-AI FFA/teams/lifecycle captures, отдельный keyboard/mouse-versus-seven-bots smoke и ordinary human setup сохранены в `docs/evidence/unity-bot-planner-2026-09-20/`. Evidence явно не подменяет paired difficulty, physical-device или target-performance acceptance. Change готов к отдельному commit; не архивировать и не интегрировать в main до следующих зависимых срезов.
