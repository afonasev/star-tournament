## 1. Asset contract and source kit

- [x] 1.1 Добавить immutable `clean-future-sport-glb-v1` manifest с четырьмя stable keys, URI LOD0/LOD1, attachment/pivot/axis/unit/proxy audit metadata; проверить focused validation tests, включая неполный manifest.
- [x] 1.2 Создать и добавить локальные glTF 2.0 `.glb` LOD0/LOD1 для portal facade, wall bay/buttress, floor route guide и central landmark с утверждёнными material tokens; проверить asset audit (метры, `+Y`/`+Z`, pivots, matching attachment bounds).

## 2. Renderer integration

- [x] 2.1 Реализовать loader/template cache, cloning, LOD switching и exact-once disposal для manifest assets; проверить renderer unit tests для load failure, clone ownership, LOD и dispose.
- [x] 2.2 Заменить renderer-native крупные portal/wall/floor/landmark primitives GLB instances, привязанными к existing surfaces и doorway gaps; проверить, что arena/collision/navigation/spawn/replay hashes не изменены и continuous wall не получает portal.
- [x] 2.3 Обновить browser startup error surface для обязательного asset failure и lifecycle integration; проверить no loop/no tick при ошибке и zero-work paused/hidden behavior.

## 3. Canonical decisions and automated verification

- [x] 3.1 Обновить `docs/GAME_SPEC.md` и changelog закрытым GLB asset-pipeline contract без внесения presentation data в simulation; проверить согласованность с OpenSpec deltas.
- [x] 3.2 Обновить renderer/runtime tests и выполнить `npm test`, `npm run typecheck`, `npm run build` и `openspec validate modular-glb-arena-kit --strict`.
- [x] 3.3 Выполнить `npm run perf:browser` для shipping GLB/LOD pass и сохранить outcome performance gate без регрессии portable lifecycle counters.

## 4. Muted browser acceptance and handoff

- [x] 4.1 Запустить dev stand из этого worktree, HTTP-подтвердить фактический URL и провести in-app Browser playtest с выключенным звуком для representative small/medium/large arenas, pause и error state; проверить console errors и presentation readability.
- [x] 4.2 Сохранить актуальные screenshots каждого изменённого visual state в `docs/evidence/modular-glb-arena-kit/` и обновить handoff task с URL, результатами и известными browser limitations.
- [x] 4.3 Отметить проверенные OpenSpec tasks, создать один изолированный Git commit и сообщить hash, evidence paths и verification results.
