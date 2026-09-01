## 1. Toolchain и границы проекта

- [x] 1.1 Создать Vite/React/TypeScript ESM scaffold, установить и зафиксировать Three.js, React DOM, Vite, TypeScript и Vitest; проверить, что `npm install` завершается и lockfile создан.
- [x] 1.2 Настроить strict TypeScript projects и scripts `dev`, `typecheck`, `test`, `build`, `preview`; проверить, что пустой scaffold проходит `npm run typecheck` и `npm run build`.
- [x] 1.3 Создать каталоги `simulation`, `profiles`, `arena`, `runtime`, `render`, `ui`, `input`, `diagnostics` и import-boundary test; проверить, что запрещённый browser/Three/React import в fixture для `simulation` обнаруживается тестом.

## 2. Детерминированные data contracts

- [x] 2.1 Реализовать canonical serialization и version-tagged FNV-1a 64-bit hash; проверить unit-тестами порядок ключей, массивы, invalid values и изменение hash при изменении данных.
- [x] 2.2 Реализовать полный deep-frozen `GameDesignProfile` `prototype-v1`, descriptor registry, range/step/cross-field validation и content identity; проверить descriptor coverage, shipped immutability, invalid profile errors и стабильный hash unit-тестами.
- [x] 2.3 Реализовать чистый `ArenaDefinition` и статическую diagnostic arena с content hash; проверить unit-тестами identity, canonical round-trip и отсутствие renderer objects.
- [x] 2.4 Реализовать `ActionFrame`, versioned `SimulationSnapshot`, seeded RNG state, pure simulation step, fixed-step headless runner и replay; проверить unit-тестами snapshot round-trip, одинаковые hash на каждом tick и независимость от render cadence.

## 3. Browser runtime и presentation adapters

- [x] 3.1 Реализовать Three.js renderer-adapter diagnostic arena с одной camera, resize, WebGL error path и полным dispose ресурсов; проверить component-level tests где возможно и production typecheck.
- [x] 3.2 Реализовать React DOM diagnostics с runtime status, tick и state/profile/arena hashes без чтения mutable simulation state; проверить UI test и error-panel path.
- [x] 3.3 Реализовать browser runtime lifecycle с RAF accumulator, ограниченным catch-up, immutable UI snapshots и явным `start`/`stop`/`dispose`; проверить lifecycle tests на остановку ticks, listener cleanup и render-cadence independence.
- [x] 3.4 Собрать минимальную визуально читаемую diagnostic scene и low-chrome shell; проверить, что canvas и DOM overlay используют одну arena/profile identity.

## 4. Проверка и поставка change

- [x] 4.1 Выполнить `npm run typecheck`, `npm test`, `npm run build`, `git diff --check` и `openspec validate bootstrap-browser-runtime --strict`; сохранить краткое доказательство всех результатов.
- [x] 4.2 Запустить dev server из worktree, получить фактический URL из вывода и подтвердить его HTTP-запросом; не использовать предполагаемый порт.
- [x] 4.3 Провести in-app Browser smoke test diagnostic scene, DOM status, resize и console; сохранить screenshot и перечислить всё, что не проверено на реальном TV/GPU.
