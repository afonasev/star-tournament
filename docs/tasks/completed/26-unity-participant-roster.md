# 26 — Unity: участники отдельно от локальных мест

Статус: проверено для отдельного коммита. Astra decision memo получен и сохранён в design; strict validation проходит. Change `add-unity-participant-roster`, worktree `/private/tmp/star-tournament-unity-participant-roster`, ветка `codex/unity-participant-roster`, база `45e7f27`.

Цель среза: явное immutable participant↔seat mapping; 2–8 физических участников и 1–4 local views, correct action/HUD/killcam/standings/lifecycle ownership. Один экран не означает матч с единственным участником. Diagnostic fixtures не публикуются как playable bots; обычный human setup сохраняется.

Найдены границы: ProvingGround arrays max4; presentation требует camera на каждого; killer lookup ограничен cameras; initial spawn ограничен4 и search надо bounded; standings capacity6/palette4; простое body layer11+participant столкнётся с arms layers15–18. Дизайн утверждён в рамках существующего поручения. Immutable composition/action routing и EditMode tests добавлены; итог66/66 EditMode и49/49 PlayMode; Mac Development build PASS.

Проверки: mapping validation/aliasing и action routing; real physics восьми тел/slots/collision; nonlocal killer life/corpse; solo/2/3/4 layouts, full eight-row FFA/teams table; pause/Repeat/menu/focus; muted native FHD/4K screenshots + XML/build, performance diagnostic. Физические controllers/TV, art и reference performance остаются открытыми.

Полная цель активна и ведётся по `docs/UNITY_MIGRATION_MATRIX.md`. Этот change не завершает миграцию. Новые sidebar tasks автоматически не создавать.

Evidence: `docs/evidence/unity-participant-roster-2026-09-20/README.md`.18 focused/muted состояний в каждом FHD/4K, screenshot/state + performance diagnostics; ordinary UI Repeat/menu smoke и ограниченный keyboard/fire CUA journey. Astra final code review без блокеров. p95 FHD9.490ms /4K9.562ms, worst75.316/217.967ms; не long60FPS acceptance. Исправлено перекрытие killcam собственным corpse.
