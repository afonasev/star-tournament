# Verification — add-first-person-combat-slice

Дата проверки: 2026-09-01
Ветка: `change/add-first-person-combat-slice`
Change worktree: `/private/tmp/star-tournament-add-first-person-combat-slice`

## Итог

Реализован single-seat first-person combat foundation и устранена обнаруженная при физическом прогоне избыточная presentation-нагрузка: добавлены `presentation-balanced-v1`, pixel-budget resolution, стандартный WebGL power preference, render-on-new-tick, ограниченный HUD cadence и постоянный `performance-reference-v1` gate. Автоматические проверки, production/in-app Browser performance gate, 10-minute soak, визуальная браузерная приёмка и полный physical keyboard/mouse gate прошли.

Пользователь провёл устойчивую физическую browser-сессию и подтвердил ходьбу, прыжок, стрельбу, mouse look, уничтожение цели и `Esc` с последующим действием `Продолжить`. Embedded in-app Browser агента сам отклоняет Pointer Lock API, поэтому physical acceptance опирается на явную пользовательскую проверку, а не подменяется synthetic events или unit-тестами.

## Automated evidence

- `npm run typecheck` — pass.
- `npm test` — pass: 37 files, 221 tests.
- Focused pointer-lock/HUD/runtime retry regression — pass: 3 files, 22 tests.
- `npm run build` — pass. Vite сохранил предупреждение о chunks больше 500 kB; основной JS около 829.63 kB, lazy Rapier chunk около 2.89 MB до gzip.
- Focused presentation/performance/renderer/runtime/bootstrap regression — pass: 7 files, 42 tests.
- `npm run perf:browser` — pass: production build/preview и headless Chrome/WebGL, 1280×720 CSS, DPR 2, backing buffer 1920×1080.
- Architecture/import boundary audits — pass.
- `git diff --check` — pass.
- `npx openspec validate add-first-person-combat-slice --strict` — pass.
- Movement replay: 10 000 fixed ticks, independent runs, restore на tick 5 000 и observer cadence 0/1/2/4 дали одинаковые hashes.
- Combat replay: одинаковые shot directions, ordered hits, ammo, target health/events и hashes в independent/restore runs.

## Dev stand и browser evidence

- Фактический URL change worktree: `http://127.0.0.1:5177/`.
- HTTP probe — `200 OK`.
- Default HUD — pass: health слева снизу, shotgun/ammo справа снизу, crosshair и start overlay; screenshot сохранён, console warning/error отсутствуют.
- `?debug=1` — pass: отображены state/profile/arena/collision identities; screenshot сохранён, console warning/error отсутствуют.
- Debug acceptance telemetry — pass: DOM показывает точные player position, yaw/pitch, grounded, ammo и health/active state каждой target; это позволяет сохранить state evidence при будущем физическом прогоне без вмешательства в simulation.
- Resize — pass: canvas и DOM HUD корректно перестроились для 390×844 и вернулись к desktop viewport; playfield не перекрыт критическими элементами.
- `?debug=1&collisionBenchmark=1` — pass: p95 0.5 ms, p99 0.5 ms, max около 1.1 ms; console clean.
- Без pointer lock synthetic `W`/`Space` не двигают simulation и не расходуют боезапас — pass.
- Ошибка pointer lock оставляет игровой экран, здоровье/боезапас и активный retry overlay с сообщением, а не фатальный error surface — pass.
- После performance-правок повторный default/debug browser smoke сохранил canvas 1280×720 CSS pixels, снизил backing buffer с 2560×1440 до 1920×1080, оставил tick 0 до lock, полный HUD/debug DOM и clean console.

## Performance/power evidence

- In-app Browser `?performanceBenchmark=1` на display cadence 123–130 RAF/s — pass: pause 0 ticks / 0 WebGL / 0 HUD; активные фазы 59.93–60.47 simulation ticks/s, 59.96–60.47 WebGL submissions/s и 10.49–11.00 HUD publications/s. Повторные RAF одного tick не отправляются в GPU.
- Headless production Chrome gate — pass: активные фазы 59.93–59.99 simulation ticks/s, 58.94–59.99 WebGL submissions/s и 10.00–10.49 HUD publications/s; frame p95 не более 0.8 ms, simulation p95 не более 0.6 ms, peak 43 draw calls.
- In-app Browser `?performanceSoakSeconds=600` — pass: измерительное окно 600000.8 ms, 36 000 simulation ticks, 35 999 WebGL submissions при 74 474 RAF callbacks и 6 001 HUD publications. Rates: 59.9999 ticks/s, 59.9983 WebGL/s, 10.0017 HUD/s.
- 10-minute soak provisional evidence: animation callback p95 0.70 ms / p99 0.90 ms / max 1.50 ms; simulation p95 0.60 ms / p99 0.70 ms / max 1.00 ms; peak 43 calls, 5 204 triangles, 32 geometries, 1 texture, 2 shader programs.
- Heap delta soak: +3 970 198 bytes за 10 минут, около 0.40 MiB/min — ниже provisional warning 2 MiB/min.
- Browser/OS thermal telemetry недоступна и явно отмечена `unavailable`; абсолютная температура не заявляется. Связанный renderer process снизился примерно с 6.3% CPU во время soak до 0% после settle, но process attribution считается только вспомогательным evidence.
- Performance, default и debug screenshots сохранены в in-app Browser handoff; во всех состояниях console содержит только Vite connection и React DevTools info, без warning/error.
- После дополнительного времени на стабилизацию пользователь подтвердил физический эффект performance-правок: заметный нагрев ушёл, вентиляторы перестали работать на высокой скорости. Это подтверждает улучшение на текущем устройстве вместе с portable performance gates, но не устанавливает универсальный температурный SLA для другого hardware.

## Physical-input acceptance

Click через agent-driven in-app Browser доходит до start action, но embedded browser отклоняет Pointer Lock API и приложение остаётся на retryable overlay при tick 0. Пользовательская физическая сессия отдельно подтвердила:

- ходьбу;
- прыжок;
- стрельбу;
- mouse look;
- попадание и уничтожение цели;
- потерю pointer lock через `Esc` и успешное продолжение через overlay.

Physical keyboard/mouse gate закрыт. Component/integration/replay tests и browser visual/resize/console evidence дополняют, но не заменяют эту пользовательскую проверку.

Первичный физический прогон выявил быстрый нагрев при 2560×1440 backing buffer, повторном render каждого display RAF и `powerPreference: high-performance`. Эти источники лишней presentation-работы устранены в текущем change и покрыты постоянным gate. После стабилизации пользователь подтвердил, что заметный нагрев ушёл и вентиляторы перестали работать на высокой скорости; результат относится к текущему устройству и не заменяет будущие reference-hardware budgets.

## Scope boundaries

Этот change не реализует и не заявляет готовыми gamepad, split-screen, match clock/scoring, смерть/killcam/respawn, bots, procedural generation, menus/Game Design Lab UI или online adapter. Robot mannequins — deterministic combat fixtures, не боты и не production character assets.
