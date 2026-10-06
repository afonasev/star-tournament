## 1. Dependency и compatibility boundary

- [x] 1.1 Установить exact `@dimforge/rapier3d-deterministic-compat@0.20.0`, зафиксировать lockfile и lazy-import boundary; проверить `npm install`, отсутствие semver range и фактическую package identity.
- [x] 1.2 Реализовать async backend initialization с singleton-ready promise, versioned compatibility identity и deterministic unsupported-identity error; проверить success/failure/concurrent-init unit-тестами.
- [x] 1.3 Расширить architecture audit для `collision`: запретить renderer/React/DOM/input imports и утечку backend handles в simulation contracts; проверить positive fixture и запрещённые import fixtures.

## 2. Arena и collision/query adapter

- [x] 2.1 Повысить `ArenaDefinition` schema для обязательной сериализуемой orientation каждого primitive, мигрировать diagnostic arena и renderer; проверить canonical round-trip, validation, content hash и визуально неизменённую identity-связность тестами.
- [x] 2.2 Реализовать frozen backend-neutral collision types: compatibility identity, capsule projection, ordered contacts/hits, checkpoint payload/hash и stable error codes; проверить parse/round-trip/immutability/incompatible-version unit-тестами.
- [x] 2.3 Создать `collision-fixture-v1` как валидированный `ArenaDefinition` с floor, wall, corner, ceiling, steps, slope, ledge, cover и overlap anchor; проверить semantic IDs, geometry validation и stable content hash.
- [x] 2.4 Реализовать deterministic world construction из canonical sorted arena surfaces и capsule semantic IDs без renderer data; проверить reordered descriptors, independent worlds и identical checkpoint identity.
- [x] 2.5 Реализовать capsule movement/ground/contact adapter и deterministic projection из snapshot перед queries; проверить wall slide, corner, ceiling, landing, slope, step, ledge, tunnelling и overlap fixture tests.
- [x] 2.6 Реализовать filtered ray/shape queries с distance/semantic-ID normalization и versioned epsilon; проверить nearest occlusion, equal-distance tie-break, stable order и frozen serializable results.
- [x] 2.7 Реализовать idempotent lifecycle с точным освобождением world/colliders/controllers; проверить повторные create/dispose/restart и отсутствие queries/ticks после dispose.

## 3. Determinism и benchmark evidence

- [x] 3.1 Реализовать `collision-workload-v1` для восьми capsules и 10 000 fixed ticks с canonical actions/create/remove/query order; проверить одинаковые per-checkpoint/final hashes у двух независимых запусков.
- [x] 3.2 Добавить mid-run canonical snapshot serialize/parse, reconstruction нового world и продолжение workload; проверить совпадение каждого последующего gameplay/collision hash с непрерывным run.
- [x] 3.3 Проверить independence от observer/render cadence на нуле, одном, двух и четырёх read-only observer passes; доказать идентичные hashes без реализации split-screen layout.
- [x] 3.4 Реализовать browser benchmark harness с warm-up, 10 000 samples и pure percentile summary; unit-тестами проверить расчёт p95/p99/max, а wall-clock measurement держать вне simulation/hash.
- [x] 3.5 Добавить production bundle measurement для raw/gzip/brotli main и Rapier lazy chunks и init latency fields; проверить generated evidence schema и что WASM candidate не попал в initial main chunk.

## 4. Browser diagnostics

- [x] 4.1 Расширить immutable UI snapshot и DOM diagnostics состояниями collision loading/ready/error, compatibility identity, fixture status и benchmark summary; проверить UI tests и отсутствие чтения mutable world.
- [x] 4.2 Подключить collision bootstrap к runtime без частичного запуска simulation до ready; проверить init failure, pagehide/dispose cleanup и успешный diagnostic workload component tests.
- [x] 4.3 Добавить визуальную diagnostic fixture scene из того же oriented `ArenaDefinition`; проверить canvas/DOM/collision arena identity и renderer dispose/resize tests.

## 5. Решение и поставка spike

- [x] 5.1 Выполнить `npm run typecheck`, `npm test`, `npm run build`, `git diff --check`, architecture audits и `openspec validate validate-rapier-collision-foundation --strict`; сохранить полные pass/fail результаты без маскировки performance warnings.
- [x] 5.2 Запустить dev server из change worktree, получить фактический URL из Vite и подтвердить его HTTP-запросом.
- [x] 5.3 Провести in-app Browser smoke: cold init, ready/error surface, oriented fixtures, workload hashes, observer-cadence independence, benchmark gates, resize и console; сохранить screenshots и reference host/browser evidence.
- [x] 5.4 Зафиксировать `go` только если p95 ≤ 2.5 ms, p99 ≤ 4.2 ms, max ≤ 8.3 ms и все обязательные gates прошли; иначе зафиксировать `no-go` и удалить runtime dependency/integration. В обоих случаях обновить `verification.md`, `GAME_SPEC` §§7–8 и журнал фактическим результатом, явно оставив cross-platform/network gate открытым.
- [x] 5.5 После outcome-правок повторить все automated checks, strict validation и browser smoke; проверить согласованность proposal/spec/design/tasks с фактическим `go`/`no-go` перед commit review.
