# 31 — Unity arena families

Статус: completed change handoff, не integration/archive/deploy.

`add-unity-arena-families` реализует только ARENA-2 поверх коммита ARENA-1 `d42a1a7`: пять утверждённых spatial families, явный canonical profile selector `unity-arena-families-v1@1` и stable family ID в definition/navigation identity. Seed не выбирает family; native collision/navigation/spawn/presentation продолжают строиться из одной accepted definition.

Проверки: EditMode 86/86, PlayMode 65/65, strict OpenSpec и Mac Development build. Muted foreground Player diagnostic в 1920×1080/4 cameras записан для `wide-hall-circuit-v1`, `split-balconies-v1` и `lower-basement-v1`; PNG и JSON осмотрены в `docs/evidence/unity-arena-families-2026-09-20/`.

Не выполнены и не заявляются: ARENA-3 generated-map navigation, ARENA-4 complete validators, ARENA-5 Repeat freeze/retry/fallback/replay, user/physical/art/reference-performance acceptance, integration, archive и deploy. Новый запрос о видимости buckshot и configurable mouse sensitivity сохранён для отдельного следующего change и не смешан с ARENA-2.
