# Verification — add-first-person-combat-slice

Дата проверки: 2026-09-01
Ветка: `change/add-first-person-combat-slice`
Change worktree: `/private/tmp/star-tournament-add-first-person-combat-slice`

## Итог

Реализован single-seat first-person combat foundation: детерминированные movement/combat rules, Rapier-backed capsule movement, keyboard/mouse adapter, pointer-lock lifecycle, first-person renderer, primitive robot target fixtures и compact React HUD. Автоматические проверки и частичная браузерная приёмка прошли.

Пользователь провёл устойчивую физическую browser-сессию и подтвердил ходьбу, прыжок и стрельбу. Embedded in-app Browser агента сам отклоняет Pointer Lock API, поэтому оставшиеся physical mouse-look, hit/destroy и lock loss/resume проверки не подменены synthetic events или unit-тестами и пока остаются открытыми.

## Automated evidence

- `npm run typecheck` — pass.
- `npm test` — pass: 33 files, 199 tests.
- Focused pointer-lock/HUD/runtime retry regression — pass: 3 files, 22 tests.
- `npm run build` — pass. Vite сохранил предупреждение о chunks больше 500 kB; основной JS около 813.57 kB, lazy Rapier chunk около 2.89 MB до gzip.
- Architecture/import boundary audits — pass.
- `git diff --check` — pass.
- `npx openspec validate add-first-person-combat-slice --strict` — pass.
- Movement replay: 10 000 fixed ticks, independent runs, restore на tick 5 000 и observer cadence 0/1/2/4 дали одинаковые hashes.
- Combat replay: одинаковые shot directions, ordered hits, ammo, target health/events и hashes в independent/restore runs.

## Dev stand и browser evidence

- Фактический URL change worktree: `http://127.0.0.1:5177/`.
- HTTP probe — `200 OK`.
- Default HUD — pass: health слева снизу, shotgun/ammo справа снизу, crosshair и start overlay; console warning/error отсутствуют.
- `?debug=1` — pass: отображены state/profile/arena/collision identities; console clean.
- Debug acceptance telemetry — pass: DOM показывает точные player position, yaw/pitch, grounded, ammo и health/active state каждой target; это позволяет сохранить state evidence при будущем физическом прогоне без вмешательства в simulation.
- Resize — pass: canvas и DOM HUD корректно перестроились для 390×844 и вернулись к desktop viewport; playfield не перекрыт критическими элементами.
- `?debug=1&collisionBenchmark=1` — pass: p95 0.5 ms, p99 0.5 ms, max около 1.1 ms; console clean.
- Без pointer lock synthetic `W`/`Space` не двигают simulation и не расходуют боезапас — pass.
- Ошибка pointer lock оставляет игровой экран, здоровье/боезапас и активный retry overlay с сообщением, а не фатальный error surface — pass.
- После outcome-правок повторный default/debug browser smoke сохранил canvas 1280×720 CSS pixels (2560×1440 backing buffer), tick 0 до lock, полный HUD/debug DOM и clean console.

## Незакрытый physical-input gate

Click через agent-driven in-app Browser доходит до start action, но embedded browser отклоняет Pointer Lock API и приложение остаётся на retryable overlay при tick 0. Пользовательская физическая сессия отдельно подтвердила:

- ходьбу;
- прыжок;
- стрельбу.

Для полного закрытия gate ещё нужно явно подтвердить mouse look, попадание/уничтожение цели и потерю lock с последующим resume. Component/integration/replay tests и browser visual/resize/console evidence уже прошли, но не заменяют эти оставшиеся физические действия.

При физическом прогоне компьютер быстро начал заметно нагреваться. Read-only аудит показал canvas 1280×720 CSS / 2560×1440 backing buffer, рендер каждого display `requestAnimationFrame` при simulation 60 Hz и принудительный WebGL `powerPreference: high-performance`. Наблюдавшийся Arc renderer использовал около 42% CPU во время игры и около 2–3% после остановки/паузы. Это обязательный отдельный performance/power change: устранить лишнюю GPU/CPU работу и ввести постоянный gate до следующего критичного расширения renderer, split-screen или контента.

## Scope boundaries

Этот change не реализует и не заявляет готовыми gamepad, split-screen, match clock/scoring, смерть/killcam/respawn, bots, procedural generation, menus/Game Design Lab UI или online adapter. Robot mannequins — deterministic combat fixtures, не боты и не production character assets.
