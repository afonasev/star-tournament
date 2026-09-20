# Performance gates

> Historical browser gate (2026-09-21): этот документ описывает удалённый
> Vite/Three.js runtime и сохранён только вместе с его evidence. `npm run
> perf:browser` больше не существует в текущем дереве; Unity performance gates
> перечислены в `docs/UNITY_MIGRATION_MATRIX.md` и требуют native Player QA.

Этот документ задаёт постоянную проверку производительности Star Tournament. Она дополняет, но не заменяет functional tests, screenshots и физический browser playtest.

## Версии baseline

- Presentation profile: `presentation-balanced-v1`.
- Performance contract: `performance-reference-v1`.
- Gameplay simulation остаётся 60 Hz и не читает performance telemetry.
- Все измерения относятся к presentation/runtime и не входят в snapshot, replay, RNG или state hash.

Финальный reference host, целевые браузеры/TV, minimum FPS и бюджеты для двух и четырёх viewport остаются открытыми в `GAME_SPEC` §10. Поэтому device-sensitive пороги ниже provisional; portable lifecycle/cadence gates уже обязательны.

## Когда запуск обязателен

Performance-gate запускается, если change затрагивает хотя бы одну область:

- renderer, WebGL context, lights, shaders, materials, effects, post-processing или cameras;
- DPR, backing-buffer, resolution scaling, animation-frame cadence, pause/visibility lifecycle;
- arena surfaces, procedural generation, visible entity counts или draw-call topology;
- shipping GLB/glTF, textures, animation, LOD или collision proxies;
- simulation tick, collision/Rapier queries, bots или массовую обработку entities;
- split-screen, число viewport/cameras или local seats;
- React HUD cadence, projection state, debug overlays;
- массовые impacts, particles, decals, projectiles, corpses или gameplay events;
- runtime-зависимости Three.js/Rapier/Vite и production bundle configuration.

Text-only, narrative-only и test-only изменения могут пропустить gate. Причина пропуска должна быть указана в verification/handoff; отсутствие времени причиной не считается.

## Референсный прогон

Используется production build/preview и настоящий Chromium/WebGL. Для каждого прогона сохраняются commit, browser/OS, viewport CSS size, device pixel ratio, backing-buffer size и profile identities.

Каждая фаза сначала получает 500 ms warm-up/settle, которые не входят в counters. Порядок измерительных окон:

1. `pause`: 1 s после settle, runtime остановлен.
2. `running-idle`: 2 s без gameplay actions.
3. `movement-jump`: 2 s движения с одним прыжком.
4. `combat-burst`: 2 s с допустимыми fire press/release transitions.

Короткий gate проверяет counters каждой фазы. Для критичных renderer/assets/split-screen изменений дополнительно выполняется 10-minute thermal soak на утверждённом reference host. Synthetic driver обеспечивает воспроизводимую нагрузку, но не доказывает физическую играбельность управления.

## Hard gates `performance-reference-v1`

| Метрика | Pause/hidden | Активная секунда |
| --- | ---: | ---: |
| Simulation steps | 0 | 59–61 |
| WebGL submissions | 0 | ≤61 |
| Periodic HUD publications | 0 | ≤11 |

Дополнительно обязательно:

- один snapshot tick не отправляется в WebGL повторно без explicit invalidation;
- paused resize выполняет ровно один redraw последнего snapshot;
- optional browser metrics помечаются `unavailable`, если API отсутствует;
- после benchmark simulation snapshot/replay/hash совпадают с эквивалентным прогоном без probe.

## Provisional evidence budgets

Эти значения сохраняются в отчёте и считаются warning/regression evidence до утверждения reference hardware:

| Метрика | Provisional budget |
| --- | ---: |
| Full animation callback | p95 ≤8 ms, p99 ≤12 ms, max <50 ms |
| Simulation step | p95 ≤3 ms, p99 ≤5 ms, max ≤8.3 ms |
| Draw calls | peak ≤50 для одного текущего viewport |
| Long tasks после warm-up | 0 |
| JS heap slope | warning при >2 MiB/min |
| Reference-host CPU | warning при >30% одного core |
| 10-minute soak regression | warning при >10% к принятому baseline либо смене thermal-pressure state |

Абсолютная температура устройства не является gate: она зависит от корпуса, зарядки и окружающей среды.

## Команды и evidence

```text
npm run perf:browser
```

Команда строит production bundle и выполняет автоматизируемую часть versioned contract. Реальный WebGL прогон открывается с `?performanceBenchmark=1`; его итоговый JSON и DOM dataset сохраняются вместе со screenshots default/debug/performance состояний. Handoff должен отдельно перечислять:

- hard gate pass/fail;
- provisional warnings;
- недоступные optional metrics;
- фактический URL production/dev stand;
- console outcome и screenshots;
- результат физического input playtest, если change затрагивает управление.

Критичный 10-minute soak запускается на том же dev/production stand с `?performanceSoakSeconds=600`. Этот режим выполняет continuous deterministic combat-burst, сохраняет тот же machine-readable report и не заявляет thermal telemetry доступной, если browser/OS её не предоставляет.
