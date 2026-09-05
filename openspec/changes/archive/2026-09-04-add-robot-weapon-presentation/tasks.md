## 1. Контракт и handoff

- [x] 1.1 Обновить `docs/GAME_SPEC.md` утверждённым visual contract robots, weapon и first-person hands и добавить `docs/tasks/completed/05-add-robot-weapon-presentation.md`; проверить diff на отсутствие новых gameplay rules или чисел.

## 2. Renderer presentation

- [x] 2.1 Добавить shared-manifest GLB LOD0/LOD1 для лёгкого робота, third-person двойного энергодробовика и safe fallback; проверить unit tests manifest, pivot, identity и destroyed/respawn state.
- [x] 2.2 Интегрировать shared-template GLB world weapon и camera-attached first-person руки/двухстволку с общими texture quality tiers, двойной muzzle flash и event-driven recoil; проверить renderer tests, включая dry fire и resize redraw.
- [x] 2.3 Привязать player/weapon GLB к уже загруженной arena texture map и тем же quality tiers; проверить disposal и quality-tier behavior без второго loader.

## 3. Проверка и handoff

- [x] 3.1 Выполнить targeted tests, `npm test`, `npm run build` и `openspec validate add-robot-weapon-presentation --strict`; зафиксировать результаты в handoff task.
- [x] 3.2 Выполнить production performance gate и muted in-app Browser GLB startup/first-person visual smoke; pointer-lock interaction принята в ранее подтверждённой manual QA, тогда как automation не удерживает lock.
- [x] 3.3 Закоммитить ровно change, код, тесты, GAME_SPEC и handoff-задачу в ветке; проверить `git status` чистым.
