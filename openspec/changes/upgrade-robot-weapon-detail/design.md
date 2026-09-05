## Context

См. `proposal.md` и delta specs. Первый detail pass повысил payload и semantic audit, но текущие формы всё ещё читаются как blockout: не хватает слоёв shell, механики суставов, кистей/пальцев и функциональной weapon topology. Renderer применяет общий arena panel texture к cloned materials, сохраняя authored tint и различая identity material по имени.

## Goals / Non-Goals

**Goals:**

- Authorить локальные, self-contained GLB LOD pairs с плотной, многослойной hard-surface формой робота, world weapon и camera viewmodel.
- Сохранить уже утверждённые manifest keys, anchors, texture/quality lifecycle и presentation boundary.
- Сделать source audit повторяемым и проверять требуемые mesh/material semantics до runtime.

**Non-Goals:**

- Не менять combat, персонажную коллизию, hit volumes, animation system, input, camera controls, HUD, networking либо simulation serialization.
- Не добавлять внешний DCC/runtime dependency, новый texture set, новую residency policy, skeletal animation или asset streaming.

## Decisions

### Deterministic procedural source generator

Node source generator создаёт шесть binary GLB из named hard-surface kit: layered bevel panels, cylinders/rings, finger segments, cabling, vents, rails, coil cages и recessed bores. Это даёт воспроизводимый asset source в репозитории и позволяет unit-audit проверить silhouettes/semantics без ручной правки бинарников. Альтернатива — хранить только вручную созданные GLB; она не даёт повторяемой проверки или безопасного обновления LOD.

### Named semantic materials and feature meshes

GLB используют `identity` material для core/visor/shoulders/beacon/owner indicator и stable feature mesh names для panel layers, joint collars, finger segments, barrel shrouds, inner bores, coil, vent, rail, forearm и hand. Existing renderer сохраняет эти материалы при clone/texture assignment. Альтернатива — bake participant color в base texture; она нарушила бы runtime authoritative color identity.

### Geometry-only quality reduction

LOD1 сокращает micro-panels, finger segments, cable/coil rings и secondary rails, но сохраняет all role features, anchor and bounds. Quality selection остаётся exclusive responsibility of the existing template cache. Альтернатива — отдельный low-quality asset loader, отклонена как нарушение `clean-future-sport-glb-v1`.

### Renderer adjustment limited to semantic material mapping

Если source audit показывает, что current material-name condition недостаточен для всех identity surfaces, mapping расширяется только до explicit `identity` material names. Scene transforms, event processing и simulation adapter не меняются.

## Risks / Trade-offs

- [Существенно более детальные meshes повышают triangles и GLB payload] → LOD1, bounded authored forms, feature-density audit и existing performance gate.
- [Viewmodel может перекрыть crosshair] → fixed camera attachment bounds и muted browser screenshot at start/resize states.
- [Binary assets трудно review] → хранить source generator и machine-readable audit alongside generated GLB.
- [Texture replacement может скрыть identity] → audit material names and targeted renderer tests for participant color.

## Migration Plan

1. Generate and audit the six compatible GLB files in the existing participant directory.
2. Update only renderer material mapping if target tests demonstrate it is necessary.
3. Run typecheck, renderer/asset tests, full test/build/strict validation, browser performance gate and muted in-app Browser playtest.
4. Commit the isolated change; retain previous revision in Git for rollback. No archive, merge or deploy in this task.
