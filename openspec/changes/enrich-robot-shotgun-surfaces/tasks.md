## 1. Реализация

- [x] 1.1 Добавить геометрию, UV и встроенные фактуры; проверить шесть GLB source audit.
- [x] 1.2 Сохранить authored materials и освобождение textures; проверить lifecycle tests.

## 2. Проверка

- [x] 2.1 Выполнить typecheck, релевантные тесты, build и strict OpenSpec validation.
- [x] 2.2 Провести muted in-app Browser playtest и визуально проверить сохранённые PNG робота, world weapon и first-person weapon.
- [x] 2.3 Зафиксировать результат, ограничения и evidence в handoff и отдельном коммите без merge/archive/deploy.

Browser QA выполнен через штатный benchmark и инспектор. Ограничение физического pointer lock и ожидающее визуальное принятие записаны в docs/tasks/07-enrich-robot-shotgun-surfaces.md.

## 3. Уточнение формы после визуального отзыва

- [x] 3.1 Заменить крупные квадратные формы и добавить механические детали; проверить обе LOD всех GLB.
- [x] 3.2 Выполнить typecheck, renderer/combat tests, build, strict validation и muted browser QA; сохранить и осмотреть новые PNG.
- [x] 3.3 Обновить handoff и зафиксировать продолжение в коммите без merge/archive/deploy.

## 4. Утончённый силуэт и связный хват

- [x] 4.1 Утончить robot geometry и собрать две связные first-person руки с хватом; проверить spatial continuity и обе LOD.
- [x] 4.2 Выполнить typecheck, tests, build, strict validation и muted Browser QA для idle/recoil и разных viewport; сохранить актуальные PNG.
- [x] 4.3 Обновить handoff и закоммитить проверенную итерацию без merge/archive/deploy.
