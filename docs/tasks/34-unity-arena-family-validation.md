# 34 — Unity ARENA-4 family validation

Статус: реализовано и проверено в изолированной ветке `codex/unity-arena-family-validators`; не интегрировано, не архивировано и не опубликовано.

`validate-unity-arena-families` добавляет pure pre-runtime gate для пяти ARENA-2 `ArenaDefinition`: finite/positive solids, collision layers, named supports, spawn/route anchors, spawn regions и declared transitions. Ошибка возвращает stable code, family и semantic element; generator не делает retry/fallback и не создаёт Unity projection.

Проверки: strict OpenSpec PASS; Unity EditMode **101/101 PASS** (`2026-09-20 11:20:42Z–11:20:43Z`); Unity PlayMode **68/68 PASS** (`2026-09-20 11:21:57Z–11:22:46Z`). Новые fixtures принимают все пять family и отвергают solid/layer/spawn/region/transition mutations; PlayMode подтверждает отсутствие дочерней projection для invalid definition. Точный XML/log: `.local/unity-evidence/editmode.xml`, `.local/unity-evidence/editmode.log`, `.local/unity-evidence/playmode.xml`, `.local/unity-evidence/playmode.log`.

Это data/validation-only change: native Player QA, build и performance run намеренно не запускались, потому что player-visible state, input, camera, HUD, bot runtime и render output не изменены. Открыты: map topology/fairness scoring, retry/fallback/Repeat/replay, physical devices/TV, art, reference-hardware performance, финальная playable acceptance, integration, OpenSpec archive и deploy.
