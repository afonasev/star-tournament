## 1. Контракт presentation events

- [ ] 1.1 Расширить renderer adapter полными existing shot/impact полями и построить order-independent association по canonical event IDs; проверить unit test-ом несколько shooters и impact-before-shot sort.
- [ ] 1.2 Обновить `docs/GAME_SPEC.md` и журнал: двухстволка использует кинетическую visual feedback с дробинами и небиологическими impacts; проверить соответствие OpenSpec requirements.

## 2. Кинетические эффекты дробовика

- [ ] 2.1 Заменить placeholder muzzle feedback на двойной warm kinetic burst, дым и existing viewmodel recoil; проверить expiry и dry-fire в renderer tests.
- [ ] 2.2 Реализовать renderer-owned pooled per-pellet trail от authoritative origin к impact/range endpoint; проверить точные endpoints, cleanup и rewind в renderer tests.
- [ ] 2.3 Реализовать transient surface/robot impact variants с normal-aware sparks/chips и без persistent geometry; проверить оба варианта и lifecycle в renderer tests.

## 3. Верификация

- [ ] 3.1 Запустить targeted renderer tests, полный `npm run check` и `openspec validate add-shotgun-fire-impact-presentation --strict`; исправить все регрессии.
- [ ] 3.2 Запустить из worktree muted dev-стенд, подтвердить HTTP URL, провести in-app Browser playtest local shot, wall hit и robot hit; сохранить и визуально проверить актуальные screenshots всех изменённых состояний.
