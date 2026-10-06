## Context

См. `proposal.md` и `specs/hit-driven-death-presentation/spec.md`. Сейчас renderer наклоняет живую group при dead state, создаёт transient wireframe-сферу на `target-destroyed` и показывает corpse отдельной сферой. GLB уже имеет semantic rigid-joint hierarchy, а snapshot несёт corpse position/yaw и в смертельном tick содержит `shotgun-shot` origin.

## Goals / Non-Goals

**Goals:**

- Сохранить направление последнего смертельного выстрела в presentation history и разыгрывать одну time-based последовательность падения со сдвигом тела.
- Создать GLB-corpse с той же identity/weapon presentation и правильно спроецировать floor contact для capsule-based participants.
- Дать изолированной review surface управляемую проверку фаз для LOD0/LOD1.

**Non-Goals:**

- Ragdoll, физика трупов, gameplay knockback, изменение hit volumes/collision, schema snapshot/replay и killcam.
- Новые asset, loader, texture budget, audio, split-screen или сетевой контракт.

## Decisions

### Direction is event-derived, with a stable fallback

При обработке `target-destroyed` renderer найдёт matching shotgun shot того же tick и вычислит нормализованный horizontal vector от `origin` к погибшему target. Он кэшируется по `lifeId` только в presentation. Если renderer начинает с позднего snapshot или matching event недоступен, yaw corpse даёт фиксированное направление. Это сохраняет намерение выстрела при нормальном runtime, не расширяя authoritative snapshot.

Альтернатива — добавить impulse в corpse/snapshot — отклонена: presentation angle не является gameplay authority и потребовала бы schema/replay migration.

### Profiled visual-only slide

`presentation-balanced-v1.death.corpseSlideDistanceMeters` имеет shipped значение 0,65 м, минимум 0 м, максимум 1,5 м и шаг 0,05 м. Renderer применяет smoothstep progress от 0 до этого расстояния в `death-fall` к dynamic model и готовому corpse rest transform. Параметр живёт в presentation profile с полным descriptor metadata: UI лаборатории сможет использовать его после появления presentation-profile editing, но profile не входит в match identity.

Один чистый helper возвращает абсолютный transform из death contact, elapsed time, yaw и distance; dynamic target, corpse и muted review используют его совместно. `target-destroyed.killerId` выбирает matching `shotgun-shot.shooterId`, а отсутствие такого события остаётся stable yaw fallback. Для nonlocal life dynamic target видим только до final fall frame, затем corpse занимает тот же final transform; для local-seat corpse сам проходит фазы, потому что separate dynamic target отсутствует.

### One rigid staged collapse, not ragdoll

Animation controller получит phases `death-hit`, `death-fall`, `death-rest`, parameterized elapsed presentation time. Root/model transform даст перенос вниз, горизонтальный roll и renderer-only горизонтальный сдвиг до `presentation-balanced-v1.death.corpseSlideDistanceMeters`; semantic joints добавят спортивную потерю опоры, защитную реакцию рук и сохранение weapon mount. Absolute time from `deathTick` делает результат независимым от browser frame rate; corpse rest pose не дрейфует.

Альтернатива — realtime physics ragdoll — отклонена: она противоречит предсказуемой renderer-only presentation, усложняет floor contact и даёт непроверяемые различия между устройствами.

### Transfer presentation ownership to a GLB corpse

Погибшая target instance представляет анимацию collapse до финальной фазы. Затем отдельный GLB corpse instance, keyed by authoritative `corpseId`, удерживает rest pose до expiry. Он берёт participant descriptor для цвета, weapon и kind; floor base вычисляется аналогично active robot: capsule center уменьшается на capsule half-height и radius, fixture position остаётся base position. Placeholder sphere и destroyed transient удаляются.

### Review and verification

Animation-review получает death sequence control с зафиксированным shot origin, визуальными phase labels и LOD selector. Unit tests проверят направление, floor projection, GLB identity/weapon и lifecycle; renderer performance test гарантирует отсутствие лишних entity/resource allocations между snapshots. Browser acceptance — muted kill в реальном матче плюс screenshots review и матча.

## Risks / Trade-offs

- [Renderer впервые видит уже мёртвую life] → стабильная yaw fallback pose вместо ложной траектории от текущей позиции стрелка.
- [Смена active model на corpse даёт flicker] → общий key по `lifeId` и тест boundary ticks.
- [Поза пересекает геометрию на рампе] → scope ограничен presentation floor-contact существующей позиции; тело не имеет collision и не симулирует terrain conforming.
- [Несколько одновременных смертей] → независимая presentation history по `lifeId` и `corpseId`.

## Migration Plan

1. Добавить renderer/profile/test coverage и обновить canonical GAME_SPEC.
2. Выполнить strict OpenSpec validation, typecheck, tests, build и performance gate.
3. Провести muted in-app Browser review и gameplay kill visual acceptance; при регрессии откатить один change commit, так как simulation data и migration отсутствуют.
