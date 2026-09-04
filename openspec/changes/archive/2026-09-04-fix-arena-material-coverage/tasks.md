## 1. Material coverage

- [x] 1.1 Обновить утверждённый texture/material contract в `GAME_SPEC.md` и changelog; проверить `openspec validate --strict`.
- [x] 1.2 Заменить project-bound wall/floor texture assets на tile-safe clean-future-sport panel kit и проверить dimensions/repeatability без participant identity.
- [x] 1.3 Назначить отдельные wall/ceiling, floor и accent material families в renderer; проверить unit tests, что крупные visible surface classes не используют flat fallback.

## 2. Stable presentation geometry

- [x] 2.1 Удалить coplanar detail composition у wall, portal и GLB instances, сохранив renderer-only transforms; проверить scene-graph unit tests на fixed presentation separation.
- [x] 2.2 Сохранить корректное disposal текстур/материалов и проверить существующие renderer lifecycle tests.

## 3. Verification

- [x] 3.1 Запустить targeted renderer tests, полный unit suite, production build и strict OpenSpec validation.
- [x] 3.2 Провести muted in-app Browser playtest в default и High quality: начать матч, осмотреть wall/ceiling/стыки и сохранить актуальные screenshots без console errors.
