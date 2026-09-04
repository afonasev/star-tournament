# 06 — Collidable arena panels

## Scope

`add-collidable-arena-panels`: крупные wall bays и portal facades следуют canonical wall shell; shell входит в deterministic arena identity, collision, navigation и spawn validation. Мелкие details остаются presentation-only.

## Acceptance evidence

- typecheck, unit tests, production build, performance gate и strict OpenSpec validation;
- muted in-app Browser playtest representative procedural arena и актуальные screenshots;
- отдельный commit в `codex/collidable-arena-panels`; без merge, archive или deploy в этой задаче.
