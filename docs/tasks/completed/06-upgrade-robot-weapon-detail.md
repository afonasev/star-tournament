# 06 · Upgrade robot and weapon detail

Статус: завершено; реализация `4ca5d0e` уже входит в main. Пользователь 2026-09-05 поручил синхронизировать спецификации и архивировать change. Архив: `openspec/changes/archive/2026-09-05-upgrade-robot-weapon-detail/`.

При закрытии сохранены более новые требования semantic joints, neutral pose, slender silhouette, authored materials и first-person grip. Устаревшая формулировка широких плеч уточнена согласно GAME_SPEC; code/assets не менялись. Все 19 объектов OpenSpec прошли strict validation до архивации. Исходные тесты и browser evidence ниже исторические; повторный visual/performance gate не требуется для documentation-only закрытия. Ограничения автоматического pointer lock не подменяются новой проверкой. Деплой не настроен и не выполнялся.

## Scope

OpenSpec change `upgrade-robot-weapon-detail` replaces only the six existing participant GLB LOD assets with a second dense hard-surface pass. It retains `light-sport-robot-v1`, `clean-future-sport-glb-v1`, existing manifest keys, renderer-owned cache/texture lifecycle and graphics-quality behavior.

## Confirmed rules

- World robot adds layered shell/armour panels, joint collars, vents, cable/energy details and articulated fingers while retaining the off-white/pale-gray shell, dark-navy joints, broad shoulders, core, visor and rear beacon. Participant identity remains explicit on core, visor, shoulders and beacon.
- World double-barrel energy shotgun adds multi-part shrouds, recessed bores, visible coils, heat-sink vents and rails while retaining the receiver/grip silhouette and owner-color indicator.
- First-person view adds articulated forearms, hands and fingers plus the matching dense weapon lower-right, keeping the centre crosshair unobscured.
- The work is presentation-only: simulation, snapshot/replay/state hash, collision/hit volumes, combat, balance, input, camera controls, networking and `add-collidable-arena-panels` are out of scope.

## Verification

- `node scripts/generate-participant-assets.mjs`: generated six shipping GLB files reproducibly.
- Source audit in `src/render/arenaGlbManifest.test.ts`: all LOD pairs retain manifest spatial extras, `identity` material and role-specific panel/joint/finger/bore/coil/vent/rail names; LOD0 has at least 1.75× the node count of LOD1.
- `npm run typecheck`: passed.
- Targeted asset/renderer tests: 3 files / 20 tests passed; after the compact viewmodel correction, 2 files / 18 tests passed.
- `src/simulation/playableDeterminism.test.ts`: passed 2 tests in 51.68 s.
- `npm test`: passed 48 files / 288 tests in 54.97 s.
- `npm run build`: passed; Vite retains the pre-existing large-chunk warning.
- `openspec validate upgrade-robot-weapon-detail --strict`: passed.
- `npm run perf:browser`: passed `performance-reference-v1`; active phases were 59.95–59.97 simulation ticks/s, 54.45–59.97 WebGL submissions/s and 10.49 HUD publications/s; menu, paused and results stayed zero-work. Headless reference browser reports long-task/thermal telemetry unavailable.
- Branch dev stand: `http://127.0.0.1:5178/?muted=1`, HTTP 200.
- Muted in-app Browser smoke: match setup and mandatory GLB preparation completed without startup error. The first-person presentation frame renders the compact lower-right robotic forearms and double-barrel viewmodel while the centre crosshair remains available. Automation cannot retain pointer lock, so interactive locomotion/fire acceptance remains the existing manual-QA boundary.
- Dense-detail recheck: typecheck plus the asset/renderer suite (3 files / 20 tests), build, strict OpenSpec validation and complete `npm test` (48 files / 288 tests) passed. `performance-reference-v1` passed again with 59.44–59.96 simulation ticks/s; pauses and results remained zero-work. Muted in-app Browser at `http://127.0.0.1:5178/?muted=1` rendered the new first-person model without a startup error; the fresh screenshot is attached to this handoff turn.
