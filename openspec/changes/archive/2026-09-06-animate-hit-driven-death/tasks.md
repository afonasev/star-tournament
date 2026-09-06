## 1. Контракт и режиссура смерти

- [x] 1.1 Обновить `docs/GAME_SPEC.md` утверждённым renderer-only падением от origin смертельного выстрела и проверить соответствие §3, §7–8 и журнала.
- [x] 1.2 Расширить rigid robot animation controller фазами hit/fall/rest и покрыть unit-тестами direction, time-based easing и финальную позу.

## 2. Presentation тела

- [x] 2.1 Заменить destroyed/corpse placeholder geometry на identity-preserving GLB presentation, выводя направление из смертельного shotgun event с yaw fallback; проверить renderer tests.
- [x] 2.2 Проецировать corpse на floor contact для capsule participants, сохранить weapon/emissive treatment и проверить lifecycle до/после respawn и expiry.
- [x] 2.3 Добавить descriptor `presentation-balanced-v1.death.corpseSlideDistanceMeters` (0,65 м; 0–1,5 м; шаг 0,05 м), связать его с общим time-based renderer-only transform от shot origin, корректно выбрать origin по `killerId`, передать без двойной модели dynamic target → corpse и покрыть profile/renderer unit-тестами.

## 3. Review и приёмка

- [x] 3.1 Добавить в muted animation-review управляемый death review для LOD0/LOD1 и проверить component/browser-facing tests.
- [x] 3.2 Выполнить `openspec validate animate-hit-driven-death --strict`, `npm run typecheck`, `npm test`, `npm run build` и обязательный renderer performance gate.
- [ ] 3.3 Провести muted in-app Browser death review и убийство в матче; сохранить актуальные PNG начала падения, контакта, финальной позы и gameplay результата.
- [ ] 3.4 Повторить strict validation, typecheck, полный test/build и renderer performance gate после profile change; сохранить browser screenshot с финальным сдвинутым телом.
