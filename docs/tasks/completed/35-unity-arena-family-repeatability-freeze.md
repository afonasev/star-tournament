# 35 — Unity arena family repeatability and freeze

Статус: implementation verified; не integration/archive/deploy.

`add-unity-arena-family-repeatability-freeze` реализует только native Unity ARENA-5: одинаковые explicit seed/generator version/profile/family дают один identity, а `ArenaFreezeSnapshot` хранит валидированный immutable definition/profile hand-off. Unity projection и native navigation используют только этот snapshot; definition/profile drift завершает создание projection/navigation context стабильной диагностикой без retry, fallback или подмены family.

Проверки 2026-09-20: `./unity/tools.sh test-edit` — 102/102 passed; `./unity/tools.sh test-play` — 68/68 passed; `openspec validate add-unity-arena-family-repeatability-freeze --strict` — valid; `git diff --check` — только существующие Unity-generated serialized settings, не относящиеся к change и не добавляемые в commit.

Native Player QA намеренно не запускался: change не меняет player-visible input, topology, UI или presentation. Открыты физическая device/TV, performance, artistic и final playable acceptance; replay, Repeat lifecycle, retry/fallback, integration, archive и deploy не выполнялись.
