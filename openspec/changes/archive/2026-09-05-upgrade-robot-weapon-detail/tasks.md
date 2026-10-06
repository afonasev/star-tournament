## 1. Контракт и source audit

- [x] 1.1 Обновить `docs/GAME_SPEC.md` и создать handoff `docs/tasks/completed/06-upgrade-robot-weapon-detail.md`, явно сохранив renderer-only scope; проверить diff на отсутствие gameplay, collision или balance changes.
- [x] 1.2 Добавить повторяемый participant GLB generator и source audit для LOD/pivot/material semantics; проверить audit на всех шести shipping GLB.

## 2. High-detail participant assets

- [x] 2.1 Заменить LOD0/LOD1 `light-sport-robot` на high-detail light-sport robot с читаемыми shell/joint/core/visor/shoulder/beacon features; проверить spatial contract и participant-color renderer test.
- [x] 2.2 Заменить LOD0/LOD1 `double-barrel-shotgun` на world weapon с muzzle openings, barrel shrouds, energy chamber, receiver/grip и owner indicator; проверить GLB audit и world renderer attachment test.
- [x] 2.3 Заменить LOD0/LOD1 `first-person-shotgun` на camera viewmodel с двумя forearms/hands и matching weapon features; проверить GLB audit, first-person render/resize and muzzle flash tests.
- [x] 2.4 Выполнить second-pass dense hard-surface authoring для шести participant GLB: layered panels, joint collars, articulated fingers, cable/vent details, recessed bores, coils и rails; расширить source audit и проверить LOD0/LOD1 role features.

## 3. Проверка и handoff

- [x] 3.1 Выполнить `npm run typecheck`, targeted asset/renderer tests, `npm test`, `npm run build` и `openspec validate upgrade-robot-weapon-detail --strict`; записать результаты в handoff.
- [x] 3.2 Выполнить `npm run perf:browser`, затем muted in-app Browser playtest из dev-стенда этой ветки, проверить world и first-person states и приложить свежие screenshots в handoff.
- [x] 3.3 Закоммитить ровно новый change, спецификацию, handoff, generator, assets и tests; проверить чистый `git status`, не архивируя, не вливая и не деплоя.

## Последующее закрытие — 2026-09-05

Ограничение пункта 3.3 относилось к первоначальному handoff. Реализация `4ca5d0e` уже интегрирована; пользователь отдельно разрешил archive и sync. Перед архивацией delta согласована с более новыми neutral pose/joint hierarchy и slender silhouette, обе capabilities синхронизированы без потери прочих требований. `openspec validate --all --strict`: 19/19 PASS; `git diff --check`: PASS. Runtime и assets не изменены, прежние browser/performance evidence сохранены. Деплой не выполнен.
