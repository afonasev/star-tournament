## Context

См. `proposal.md` — Why. Текущий runtime уже имеет pure fixed-step simulation, canonical snapshots/replay/hash и static `ArenaDefinition`, но collision отсутствует. Обычный Rapier гарантирует только локальную детерминированность; deterministic build является единственным допустимым кандидатом для будущего replay/network пути. Capability contract задан в `specs/collision-query-foundation/spec.md`.

## Goals / Non-Goals

**Goals:**

- Проверить deterministic Rapier как изолированный static-world query backend, не превращая его mutable WASM world в скрытый authority.
- Зафиксировать backend-neutral serializable query/result contract, compatibility identity и lifecycle.
- Получить воспроизводимые functional, determinism, bundle/init и reference-browser performance evidence с однозначным `go`/`no-go`.
- Сохранить возможность заменить backend без изменения action frames, gameplay rules, renderer и DOM UI.

**Non-Goals:**

- Не реализовывать player movement rules, оружие, damage, dynamic rigid-body gameplay или физику тел.
- Не добавлять gameplay-параметры KCC в `prototype-v1`: spike использует versioned diagnostic fixture, а принятые backend-neutral настройки добавит movement change со своими descriptors.
- Не выбирать сетевую authority/prediction и не заявлять cross-platform determinism по одному host.
- Не реализовывать реальные split-screen layout/cameras; разная render load моделируется дополнительными read-only observer passes над тем же workload.

## Decisions

### Единственный кандидат — deterministic compat 0.20.0

Устанавливаем exact `@dimforge/rapier3d-deterministic-compat@0.20.0` без semver range. Package name, version и наш `collision-contract-v1` входят в compatibility identity snapshot/replay. Обычный compat отклонён из-за отсутствия cross-platform guarantee. Deterministic non-compat оставлен будущей bundle-оптимизацией: его Vite/Vitest WASM path сначала потребует отдельного доказательства.

### Derived static world вместо authoritative Rapier snapshot

Выбираем reconstruction path. Fixed arena colliders создаются из canonical sorted `ArenaDefinition`; participant capsules имеют стабильные semantic IDs и перед каждым tick/query явно проецируются из сериализуемого snapshot в стабильном порядке. Acceleration, velocity, gravity, jump и финальная position остаются gameplay state симуляции. Rapier world не содержит динамики, которой нет в snapshot, и не сериализуется как часть gameplay authority.

Альтернатива `World.takeSnapshot()` отклонена для foundation: binary snapshot связывает save/replay со внутренним форматом, а character-controller configuration всё равно приходится пересоздавать. Spike всё же вычисляет collision checkpoint hash из compatibility identity, static geometry и canonical projected capsule state, чтобы доказать отсутствие расхождения.

### Query adapter и ownership

`src/collision/` владеет асинхронной загрузкой WASM, world/collider/controller handles, stable ID mapping, filters, sorting, diagnostics и `free()`. Он не импортирует renderer, React, DOM или физические устройства. Simulation runner передаёт adapter-у чистые vectors/shapes и получает frozen serializable results; правила движения не живут в adapter.

Renderer читает те же simulation snapshots и может выполнять ноль или несколько observer renders без вызова collision mutations. DOM показывает `loading`, `ready`, `error`, compatibility identity и benchmark summary через immutable UI snapshot. Input, pointer lock, keyboard/gamepad seats, reconnect и pause не затрагиваются: workload формирует action/query data напрямую.

### Stable construction и query order

Arena surfaces и participants сортируются по semantic ID до создания handles. Backend handles никогда не попадают в hash. Все multi-hit результаты нормализуются по distance, затем semantic ID; versioned epsilon и quantization существуют как обоснованные technical invariants, а не Game Design Lab параметры. Любые callback/results, порядок которых backend не обещает, собираются полностью и сортируются до возврата.

### Functional fixture

Создаётся отдельный `collision-fixture-v1` как `ArenaDefinition`: пол, стена, внутренний угол, низкий потолок, допустимая и высокая ступени, склон, край, cover и overlap-spawn. Fixture profile содержит только диагностические KCC значения и не экспортируется как shipped `GameDesignProfile`. Expected outcomes фиксируют containment, grounded, contact normals, step/slope decisions, nearest ray hit и tie-break.

### Determinism и restore доказательство

Versioned workload создаёт восемь capsules, выполняет 10 000 fixed ticks и deterministic sequence move/overlap/ray/shape queries. Два полностью независимых world сравниваются на каждом checkpoint. В середине run исходный snapshot проходит canonical serialize/parse, новый world реконструируется и продолжает тот же action stream; hashes должны совпасть с непрерывным run.

В browser отдельные observer loops выполняют различное число render callbacks между ticks. Ни runtime `performance.now`, ни frame delta не входят в workload/hash. Cross-OS/browser проверка не симулируется и остаётся явно незакрытой.

### Performance и bundle measurement

Benchmark имеет warm-up и измеряет collision section вокруг каждого tick через browser diagnostics, вне simulation state. Reference gates: p95 ≤ 2.5 ms, p99 ≤ 4.2 ms, max ≤ 8.3 ms. Report сохраняет host/browser, warm-up, sample count и raw summary; это prototype engineering gate, не production target-device budget.

WASM импортируется динамически как отдельный lazy chunk. Production evidence фиксирует размеры основных и Rapier chunks без compression, gzip и brotli, а browser — cold/warm init latency. Если compat chunk неприемлем, это не отменяет functional result, но candidate получает `no-go` до отдельной deterministic non-compat проверки.

### Результат change

При `go` dependency и adapter остаются production foundation, `GAME_SPEC` фиксирует provisional выбор и доказанные ограничения. При `no-go` runtime integration и production dependency удаляются, но versioned evidence и причина сохраняются в change; capability/artifacts пересматриваются перед архивацией, чтобы main specs не обещали непринятый backend.

## Risks / Trade-offs

- [WASM init или Vite bundling не работает стабильно] → явный error surface, production build и browser cold-start являются обязательными gates; при провале dependency не остаётся в runtime.
- [Derived reconstruction расходится из-за скрытого backend cache] → checkpoint-resume и independent-world tests выполняют тот же query stream после новой инициализации; любое расхождение означает `no-go`.
- [Semantic tie остаётся backend-dependent] → adapter собирает кандидатов и выполняет собственную stable distance/ID сортировку; fixture содержит равные hits.
- [Microbenchmark шумный] → warm-up, 10 000 samples, p95/p99/max и сохранённая среда; unit tests не используют wall-clock thresholds.
- [~1.1 MB gzip compat chunk] → lazy loading и точный bundle report; non-compat рассматривается только отдельным change.
- [Один host создаёт ложную уверенность] → итог всегда маркируется reference-host-only, а online/cross-platform release блокируется отдельным matrix gate.

## Migration Plan

Предыдущего collision backend и пользовательских collision данных нет. `go` добавляет новый compatibility identity и повышает schema snapshot/replay только там, где этот identity становится обязательным; старые diagnostic snapshots получают явную unsupported-version ошибку. Rollback — удалить dependency/adapter и вернуть change-коммит. `no-go` не мигрирует runtime state.

## Open Questions

- Подходит ли deterministic non-compat package как уменьшенный shipping path — только после отдельного Vite/Vitest/browser proof, если compat bundle не пройдёт gate.
- На каких ОС/браузерах подтверждать cross-platform hashes перед сетью — после утверждения target matrix в `GAME_SPEC` §10.
