## Context

Репозиторий не содержит runtime-кода; см. `proposal.md` — Why. Канон уже требует TypeScript/Vite, Three.js, React DOM, fixed-step simulation, replay/hash и metadata-driven Game Design Lab (`docs/GAME_SPEC.md` §§ 6–8). Локальная среда Node 24 совместима с актуальным Vite toolchain. Документация текущих Three.js подтверждает multi-viewport через один renderer с `setViewport`/`setScissor`, а Vite React/TypeScript template выполняет typecheck отдельно от transpilation через `tsc -b && vite build`.

## Goals / Non-Goals

**Goals:**

- Создать минимальный production-buildable browser runtime с явным lifecycle.
- Доказать детерминированный simulation/replay/snapshot/hash контракт без engine/browser dependencies.
- Создать одно каноническое описание диагностической арены и один renderer-adapter.
- Заложить полный versioned profile snapshot, descriptor registry и validation до появления игровых чисел в коде.
- Сделать архитектурные границы автоматически проверяемыми.

**Non-Goals:**

- Не реализовывать управление, pointer lock, player camera, физику, бой, match flow, split-screen или геймпады.
- Не выбирать network authority, prediction/reconciliation или transport.
- Не реализовывать UI редактирования, browser storage, историю и release lifecycle профилей.
- Не добавлять Rapier, GLB assets, post-processing или production performance targets.

## Decisions

### Toolchain и процессы

Используем ESM package с Vite, TypeScript strict mode, React DOM и Three.js. Production script выполняет `tsc -b` перед `vite build`, потому что Vite transpiles TypeScript, но не доказывает типовую корректность. Unit tests выполняются Vitest; lockfile фиксирует реально установленные версии. Альтернатива — React Three Fiber — отклонена: renderer должен оставаться императивным адаптером и не зависеть от React reconciliation.

### Модульные границы

```text
src/simulation/  pure data + step + snapshot + replay + hash
src/profiles/    schema + descriptors + validation + shipped profile
src/arena/       ArenaDefinition + diagnostic arena
src/runtime/     fixed-step scheduling + lifecycle + UI snapshot fan-out
src/render/      Three.js scene/camera/resources adapter
src/ui/          React DOM diagnostics and error surface
src/input/       serializable ActionFrame types; browser mapping comes later
src/diagnostics/ architecture/performance observability
```

`simulation` не импортирует другие верхнеуровневые browser/render/UI модули. `runtime` владеет orchestration, но не правилами; `render` читает snapshots; `ui` получает immutable `UiSnapshot`. Renderer и UI никогда не передают mutable objects обратно в simulation.

### Fixed-step scheduling

Технический инвариант первого runtime — 60 simulation ticks/second. Он не является балансной ручкой: изменение tick rate меняет сериализацию, replay и будущую сетевую совместимость, поэтому константа хранится рядом с явным обоснованием и отсутствует в descriptor registry.

Browser runtime использует `requestAnimationFrame`, accumulator и interpolation alpha. Wall-clock delta ограничивается, а число catch-up ticks имеет технический предел, чтобы возврат из background tab не заморозил UI. Эти ограничения влияют только на scheduling: simulation получает фиксированный `dt` и последовательный tick. Headless runner применяет те же ticks без RAF.

### Состояние, команды и replay

`SimulationSnapshot` содержит `schemaVersion`, `tick`, RNG state, profile identity/hash, arena identity/hash и минимальное diagnostic entity state. `ActionFrame` содержит целевой tick и нормализованные сериализуемые actions; device events и timestamps браузера в него не входят.

Replay хранит initial snapshot и упорядоченные frames. На одном tick команды сортируются по стабильному participant/action key. Round-trip parser валидирует schemaVersion и обязательные поля до создания state.

### Canonical serialization и hash

Объекты canonical serializer рекурсивно сортируют по ключам, сохраняют порядок массивов, отклоняют `undefined`, non-finite numbers и unsupported values. State/profile/arena hashes используют version-tagged FNV-1a 64-bit hex для переносимого синхронного прототипного контракта. Альтернативный SHA-256 не выбран сейчас из-за async Web Crypto boundary; формат hash допускает миграцию через префикс алгоритма.

FNV-1a не является security hash. Он подходит для replay divergence и content identity прототипа, но не для проверки недоверенного сетевого контента.

### GameDesignProfile `prototype-v1`

Profile является полным deep-frozen snapshot. Первая schema version фиксирует уже утверждённые числовые правила, даже если соответствующие механики появятся в следующих changes:

- здоровье и стартовый боезапас;
- head/torso/limb damage дробовика;
- дефолт и допустимые границы времени матча;
- assist window/points;
- kill-chain gap, totals для первых пяти убийств и increment после пятого;
- длительности killcam и visual corpse.

Для каждого поля создаётся descriptor с разумными широкими min/max/step. Позднейшие changes расширяют полный schema snapshot и повышают `schemaVersion`; частичных runtime overrides не будет. Cross-field invariants первоначально проверяют порядок min/default/max времени и монотонность kill-chain totals.

Identity включает стабильные `id`, `name`, `revision`, `source`, `schemaVersion` и `contentHash`. Hash вычисляется без самого `contentHash`, затем snapshot deep-freeze. Изменение shipped object запрещается structural immutability; будущая лаборатория создаёт новый полный local snapshot.

### Диагностическая арена и renderer

`ArenaDefinition` — чистые данные: schemaVersion, id, seed, generatorVersion=`diagnostic-v1`, primitive surfaces, spawn anchors и content hash. Renderer создаёт Three.js geometry/materials из descriptors и владеет их dispose. Одна PerspectiveCamera показывает арену; camera transform является presentation state и не записывается в simulation.

Canvas создаётся один раз. Resize использует CSS display size, ограниченный device pixel ratio и обновление projection matrix. Design сохраняет будущий seam для массива viewport descriptors, но текущий change рендерит один full-canvas viewport и не утверждает раскладки split-screen.

### DOM UI и ошибки

Один React `createRoot` отображает runtime status, tick, state/profile/arena hashes и краткую маркировку «технический фундамент». Частота UI snapshot ниже render loop; React не подписывается на mutable simulation state. При невозможности создать WebGL renderer shell показывает error panel. `dispose` вызывает `root.unmount`, отменяет RAF/listeners и освобождает Three resources.

### Проверки

- Unit: canonical serialization/hash, snapshot round-trip, replay determinism, render-frequency independence, profile descriptor coverage/ranges/cross-field rules, deep immutability и arena identity.
- Architecture: import audit запрещает DOM/Three/React/browser imports внутри `src/simulation`.
- Build: `typecheck`, `test`, `build`.
- Runtime: dev server из change worktree, HTTP probe фактического URL и in-app Browser smoke с проверкой WebGL scene, DOM status и console errors.

## Risks / Trade-offs

- [FNV-1a допускает коллизии] → version-tagged hash используется только как diagnostic identity; перед недоверенной сетью выполняется отдельный выбор hash/authority.
- [Фиксированный tick может оказаться дорогим или неудобным для движения] → benchmark и movement change проверят его до сетевой фиксации; изменение потребует schema/replay version.
- [Профиль содержит ещё не используемые механиками поля] → descriptor tests и диагностика доказывают контракт, а следующие changes обязаны потреблять их без дублирующих constants.
- [WebGL работает не на каждом будущем TV/browser] → текущий change заявляет только browser smoke в доступной среде; целевая матрица остаётся открытой.
- [Diagnostic primitives не проверяют GLB pipeline] → GLB manifest и asset validation входят в отдельный visual/assets change.

## Migration Plan

Предыдущего runtime и пользовательских данных нет. Change добавляется как новый scaffold. Rollback выполняется удалением единственного change-коммита; миграции storage или сетевого протокола не требуются.

## Open Questions

- Rapier или собственный collision/query слой выбирается отдельным spike перед movement/combat implementation.
- Целевые браузеры, TV GPU, render scale и FPS gates будут зафиксированы до split-screen acceptance.
- Криптографический content hash потребуется только после выбора сетевой authority и trust model.
