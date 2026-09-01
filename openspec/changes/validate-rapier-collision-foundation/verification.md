# Verification — validate-rapier-collision-foundation

## Outcome

`go` для локального prototype foundation на reference host. Exact identity:

- contract: `collision-contract-v1`;
- backend: `@dimforge/rapier3d-deterministic-compat@0.20.0`;
- arena fixture: `collision-fixture-v1`, `fnv1a64-v1:9fb08afcdfaf94e9`.

Это не является доказательством cross-platform или network determinism. Проверка другой ОС/браузера и утверждённая target matrix остаются обязательным gate до online-режима.

## Automated evidence

- Exact package и lockfile без semver range подтверждены `npm ls @dimforge/rapier3d-deterministic-compat --depth=0` и production build.
- Functional fixture покрывает floor/wall/corner/ceiling, low/high step, slope, ledge, cover, spawn overlap, ray occlusion и equal-distance semantic-ID tie-break.
- Два независимых запуска `collision-workload-v1` по 10 000 тиков и третий запуск с canonical serialize/parse + новым world на тике 5 000 дали одинаковые checkpoint, gameplay и collision hashes.
- Observer passes 0/1/2/4 не изменили gameplay или collision hashes.
- Lifecycle tests подтвердили idempotent dispose, возврат active-world counter к baseline и stable error после dispose.
- Итоговый прогон: `npm run typecheck` PASS; `npm test` PASS — 17 files / 102 tests; `npm run build` PASS; `git diff --check` PASS; `openspec validate validate-rapier-collision-foundation --strict` PASS.

## Reference browser evidence

- Stand: `http://127.0.0.1:5187/`, запущен из change worktree; HTTP `200 OK`.
- Environment: Codex in-app Chromium browser на текущем macOS host, default viewport `1280×720`, DPR `2`.
- Cold/warm init: обычная загрузка `83,90 / 0,00 ms`; benchmark reload `44,70 / 0,10 ms`.
- 10 000 measured samples после 256 warm-up ticks: p95 `0,400 ms`, p99 `0,500 ms`, max `1,600 ms`.
- Gates: p95 ≤ `2,5 ms`, p99 ≤ `4,2 ms`, max ≤ `8,3 ms` — PASS.
- Visual fixture использует ту же arena identity в DOM, canvas и collision checkpoint; oriented slope виден в сцене.
- Resize: при viewport `390×844` WebGL canvas стал `390×844`; DOM остался читаемым.
- Console: `0` warnings, `0` errors после cold boot, benchmark и resize.
- После outcome-документации и усиления per-tick hash evidence benchmark reload повторно прошёл: p95 `0,400 ms`, p99 `0,500 ms`, max `0,900 ms`, console чистая. Для решения сохранён худший из наблюдавшихся max `1,600 ms`.
- Screenshots обычной загрузки, benchmark summary и mobile resize сохранены в browser-playtest evidence текущей задачи.

## Bundle evidence

`npm run measure:collision-bundle` после production build:

| Chunk | Raw | gzip -9 | brotli |
| --- | ---: | ---: | ---: |
| main `index-COAyf1CE.js` | 764 399 B | 203 667 B | 169 782 B |
| lazy `rapier-DKBZ8sBn.js` | 2 889 664 B | 1 094 488 B | 804 396 B |

Rapier runtime не входит в initial main chunk; main содержит только lazy boundary. Vite сохраняет предупреждение о chunks >500 kB: functional/performance gate пройден, но размер compat-кандидата остаётся известным prototype trade-off. Cold/warm init latency измеряется browser diagnostics и приведена выше.
