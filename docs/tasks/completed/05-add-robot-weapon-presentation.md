# 05 · Add robot and weapon presentation

Статус: завершено и интегрировано в main; OpenSpec архивирован в `openspec/changes/archive/2026-09-04-add-robot-weapon-presentation/`. Ниже сохранены результаты исходной проверки; указанные dev URL — исторические, не текущие стенды. Деплой не выполнен.

## Scope

OpenSpec change `add-robot-weapon-presentation` adds renderer-only light sports robots, readable participant identity colors, a double-barrel energy shotgun in world view, and a first-person robotic hands/weapon viewmodel.

## Confirmed rules

- Base silhouette: lightweight cyan concept robot; retain angular weapon language and broad contrast shoulder panels from the selected concept sheet. Detail-ready LOD0/LOD1 GLB assets use the shared `clean-future-sport-glb-v1` manifest/cache and the arena panel texture lifecycle; they do not introduce a second loader or texture budget.
- Identity color appears on chest core, visor, shoulder panels and rear beacon; neutral shell remains off-white/pale-gray and arena navigation colors stay reserved for environment.
- The one supported gameplay weapon remains the existing double-barrel shotgun; this change adds no combat rules or additional weapons.
- First-person view shows two robotic forearms and the weapon lower-right, retains a clean crosshair center, and only reacts to existing successful shot events.
- Models, recoil, lights and effects stay renderer-only; snapshots, replay, hit volumes, collision and state hashes remain unchanged.

## Verification

- `npm run typecheck`: passed.
- Targeted renderer/manifest suite: 3 files / 16 tests passed.
- `npm test`: 46 files / 278 tests passed.
- `npm run build`: passed; existing Vite large-chunk warning remains.
- `openspec validate add-robot-weapon-presentation --strict`: passed.
- `npm run perf:browser`: passed all reference phases. Active phases held 59.95–59.99 simulation ticks/s, no more than 60 WebGL submissions/s and 10.50 HUD publications/s; menu, pause and results stayed zero-work. Long-task and thermal telemetry are unavailable in headless reference browser.
- Branch dev stand: `http://127.0.0.1:5176/?muted=1`, HTTP 200.
- Muted in-app Browser smoke: the match configurator and renderer completed mandatory GLB preparation, then returned to the normal first-person entry overlay without a startup error. The visible first-person frame shows the lower-right robotic forearms and double-barrel viewmodel. Browser automation cannot retain pointer lock, while the earlier manual acceptance confirmed the basic WASD/mouse flow.
