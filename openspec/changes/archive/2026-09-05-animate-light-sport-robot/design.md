## Context

См. `proposal.md` для мотивации. Current participant GLB содержит независимые mesh nodes без skin, skeleton или clips; renderer меняет только общий transform модели и destruction tint. Утверждённая geometry должна остаться неизменной в neutral pose.

## Goals / Non-Goals

**Goals:**

- Дать обоим robot LOD одинаковый named rigid rig и сохранить их внешний вид в rest pose.
- Ввести presentation controller с полным набором согласованных движений, получающим только projected snapshot/event данные.
- Дать reviewable browser surface для состояний без добавления новой gameplay simulation.

**Non-Goals:**

- Skinning, blend shapes, DCC dependency, новый GLTF loader либо animation clip format.
- Изменение deterministic simulation, collision, hit volume, combat, networking, camera или accepted first-person animation.
- Заявление, что review sandbox проверяет pointer lock, physical mouse-look либо матчевую физику.

## Decisions

### Rigid semantic hierarchy вместо skinning

Generator сохранит existing hard-surface meshes, но будет parent их под named pivot groups с локальными transform, эквивалентными текущему world-space rest pose. Иерархия содержит root/pelvis/spine/head, symmetric arm/leg chains и weapon mount. Это позволяет вращать панели без deformation и воспроизводится в LOD0/LOD1.

Skin/animation clips отвергнуты: они потребуют weights, authoring/DCC pipeline и создадут риск деформации принятого hard-surface silhouette. Runtime groups делают joint contract явным и проверяемым source audit.

### Renderer-owned action controller

Новый controller получает elapsed presentation time и typed projected motion/event hints; он вычисляет pose additively от neutral transform, применяет его к named joints и сбрасывает после duration. Locomotion и jump используют только derived kinematic hints, firing/hit/destruction — поддерживаемые event IDs. Controller никогда не является источником authoritative transform или timing simulation.

Экстраполяция по mesh transforms отвергнута: она не даёт стабильного семантического контракта и ломает LOD parity. Новая simulation state machine отвергнута, потому что animation не меняет правила матча.

### Weapon и first-person границы

World shotgun parented under robot weapon mount, чтобы follows aiming/firing joints. Existing first-person GLB продолжает использовать свой independent viewmodel root и current recoil; он не наследует third-person rig.

### Review sandbox

Отдельная browser route создаёт scene/controller с deterministic local review clock, controls и LOD switch. Он не импортирует gameplay input adapter и не запускает match reducer. Состояния могут запускаться по отдельности или по последовательному review loop; audio остаётся muted.

## Risks / Trade-offs

- [Неверный pivot сдвинет принятую neutral pose] → generator audit сравнивает world-space rest bounds/features до и после hierarchy для обоих LOD.
- [Controller может незаметно менять gameplay projection] → typed adapter принимает только immutable snapshot/event input; renderer tests сверяют отсутствие mutation.
- [Полный набор движений ухудшит читаемость или budget] → bounded procedural amplitudes, LOD parity test и muted browser review/gameplay capture.
- [Hit event не указывает semantic target] → projection расширяется только renderer-facing event metadata; combat reducer и persisted event identity не меняются.

## Migration Plan

1. Regenerate participant robot LOD assets и добавить source audit нейтральной позы/hierarchy.
2. Ввести controller и projected presentation event hints с regression tests.
3. Подключить controller к third-person renderer и review sandbox.
4. Выполнить muted browser review и gameplay renderer checks; rollback — удалить controller/hierarchy usage и вернуться к existing static presentation assets.
