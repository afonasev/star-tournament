## 1. Реализация

- [x] 1.1 Добавить геометрию, UV и встроенные фактуры; проверить шесть GLB source audit.
- [x] 1.2 Сохранить authored materials и освобождение textures; проверить lifecycle tests.

## 2. Проверка

- [x] 2.1 Выполнить typecheck, релевантные тесты, build и strict OpenSpec validation.
- [x] 2.2 Провести muted in-app Browser playtest и визуально проверить сохранённые PNG робота, world weapon и first-person weapon.
- [x] 2.3 Зафиксировать результат, ограничения и evidence в handoff и отдельном коммите без merge/archive/deploy.

Browser QA выполнен через штатный benchmark и инспектор. Ограничение физического pointer lock и ожидающее визуальное принятие записаны в docs/tasks/07-enrich-robot-shotgun-surfaces.md.
