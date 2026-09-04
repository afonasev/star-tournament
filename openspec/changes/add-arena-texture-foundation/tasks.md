## 1. Presentation quality contract

- [ ] 1.1 Создать immutable `graphics-quality-v1` descriptor, local preference validation/fallback и unit tests, подтверждающие отсутствие simulation imports и корректные Low/Balanced/High/Ultra budgets.
- [ ] 1.2 Передать effective profile в browser renderer и проверить тестами quality-aware pixel budget, 4K output handling и неизменность existing lifecycle counters.

## 2. Texture material pass

- [ ] 2.1 Сгенерировать и сохранить в проекте first-pass clean-future-sport raster source assets для tiling wall/floor и unique decal atlas; проверить dimensions, repeatability и отсутствие текста, похожего на team/participant identity.
- [ ] 2.2 Добавить presentation-only texture manifest/factory с base-color, normal и ORM maps, GPU-friendly renderer settings, residency accounting и disposal tests.
- [ ] 2.3 Применить tiling PBR materials и atlas decals к arena renderer, включая quality-tier detail/LOD selection, и проверить renderer unit tests без изменения `ArenaDefinition`/hash.

## 3. Graphics menu

- [ ] 3.1 Добавить compact DOM graphics section в match menu с preset selector, отдельными controls, effective-value и unsupported/fallback feedback; проверить React interaction/accessibility tests и local persistence.
- [ ] 3.2 Связать меню с новым browser session и проверить, что selected quality влияет только на renderer presentation, не изменяя match configuration, seed или replay identity.

## 4. Verification

- [ ] 4.1 Запустить unit/integration suite, OpenSpec strict validation и production build; исправить все регрессии и сохранить результаты.
- [ ] 4.2 Выполнить production performance gate для Balanced и Ultra, проверить portable lifecycle gates и зарегистрировать renderer/texture evidence.
- [ ] 4.3 Провести muted in-app Browser playtest menu и playable arena в default/high tiers, проверить 4K-layout smoke при эмуляции, собрать console/DOM evidence и актуальные screenshots.
