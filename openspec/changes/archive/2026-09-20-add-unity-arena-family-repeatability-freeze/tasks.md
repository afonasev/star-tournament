## 1. Frozen arena hand-off

- [x] 1.1 Добавить immutable native freeze snapshot и stable mismatch diagnostic на границе accepted family generation; проверить EditMode test, что каждый из пяти families повторяет identity при одинаковых explicit inputs.
- [x] 1.2 Направить ARENA-4 validation и Unity projection через accepted freeze snapshot без повторной generation; проверить EditMode tests для profile/definition drift и отсутствие fallback/substitution.
- [x] 1.3 Ограничить owned navigation context frozen identity/profile snapshot; проверить EditMode test, что mismatch отклоняется до route query.

## 2. Projection and evidence

- [x] 2.1 Добавить focused PlayMode coverage: валидный frozen family строит projection/navigation context, а mismatched hand-off не оставляет созданного owned context; запустить тест в Unity Test Framework.
- [x] 2.2 Запустить relevant Unity EditMode/PlayMode suites и `openspec validate add-unity-arena-family-repeatability-freeze --strict`; сохранить точные команды/итоги в handoff evidence.
- [x] 2.3 Зафиксировать, что native Player QA не запускается: change не меняет player-visible input, topology, UI или presentation; physical device/TV, performance и final playable acceptance остаются открытыми.
