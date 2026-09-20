# 30 — Unity procedural arena foundation

Статус: completed change handoff, не archive/deploy.

`add-unity-procedural-arena-foundation` вводит только ARENA-1: pure serializable `ArenaDefinition`, отдельный generator input (`seed`, generator version и frozen profile fingerprint) и один сохранённый two-level layout family. Unity adapter, spawn selection и navigation identity получают geometry из definition; seed пока не создаёт искусственную вариативность до ARENA-2.

Проверки: EditMode 85/85, PlayMode 64/64, strict OpenSpec validation и Mac build. Muted foreground Player diagnostic в 3840×2160 с четырьмя cameras записан в `docs/evidence/unity-arena-foundation-2026-09-20/`; это diagnostic, не physical/art/performance acceptance.

Открыто: ARENA-2 spatial families/size UX, ARENA-4 complete validators, ARENA-5 generated lifecycle/retry/fallback/replay, реальные устройства/TV, artistic approval, reference-hardware performance gate. Main, archive и deploy не выполнялись.
