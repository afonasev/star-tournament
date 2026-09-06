## 1. Контракт и режиссура смерти

- [x] 1.1 Обновить `docs/GAME_SPEC.md` утверждённым renderer-only падением от origin смертельного выстрела и проверить соответствие §3, §7–8 и журнала.
- [x] 1.2 Расширить rigid robot animation controller фазами hit/fall/rest и покрыть unit-тестами direction, time-based easing и финальную позу.

## 2. Presentation тела

- [x] 2.1 Заменить destroyed/corpse placeholder geometry на identity-preserving GLB presentation, выводя направление из смертельного shotgun event с yaw fallback; проверить renderer tests.
- [x] 2.2 Проецировать corpse на floor contact для capsule participants, сохранить weapon/emissive treatment и проверить lifecycle до/после respawn и expiry.

## 3. Review и приёмка

- [x] 3.1 Добавить в muted animation-review управляемый death review для LOD0/LOD1 и проверить component/browser-facing tests.
- [x] 3.2 Выполнить `openspec validate animate-hit-driven-death --strict`, `npm run typecheck`, `npm test`, `npm run build` и обязательный renderer performance gate.
- [ ] 3.3 Провести muted in-app Browser death review и убийство в матче; сохранить актуальные PNG начала падения, контакта, финальной позы и gameplay результата.
