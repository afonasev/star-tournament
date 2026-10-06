## Why

Сейчас simulation-капсула не покрывает фактический габарит visual robot у стены, поэтому тело может визуально заходить в wall shell, а world-weapon и first-person viewmodel клиппятся в архитектуру. Это нарушает читаемость столкновений и противоречит утверждённому требованию фактического capsule clearance в `docs/GAME_SPEC.md` §§2, 3 и 7.

## What Changes

- Profile-owned capsule тела получает audited radius, покрывающий robot silhouette; полувысота компенсирует изменение радиуса, сохраняя общую высоту и anchor камеры.
- Исправляется детерминированная projection/движение у стен так, чтобы player model не попадала внутрь wall, corner, portal, barrier, ramp или slab после movement/collision response.
- Оружие сохраняет renderer-only gameplay статус: у static wall и выступающего wall-attached presentation decor оно плавно принимает всегда видимую сложенную позу, не меняя hit volumes, shot origin, damage, ammo, cooldown, snapshot либо replay.
- **BREAKING** collision compatibility identity и profile revision изменяются: старые collision checkpoint/replay не интерпретируются как новый wall-clearance contract.
- Добавляются regression, determinism, arena-clearance и muted browser acceptance проверки, включая pointer-lock movement/look/jump/fire и актуальные screenshots.

Решение пользователя: увеличить collision radius тела до 0,55 м, уменьшить half-height до 0,35 м и сохранить общую высоту 1,80 м; weapon остаётся visual-only retract. Он не может исчезать за camera near plane и учитывает conservative renderer-owned envelopes wall-attached decor без добавления collision в simulation. Первый scope охватывает только живых участников и canonical static arena geometry. Трупы остаются renderer-only.

## Capabilities

### New Capabilities

- `participant-wall-clearance`: Контракт отсутствия визуального пересечения живого participant body со static arena wall geometry и visual-only weapon retraction у препятствия.

### Modified Capabilities

- `collision-query-foundation`: Collision backend получает versioned wall-clearance movement contract при сохранении renderer-independent authority.
- `first-person-player-control`: Движение игрока принимает только position, безопасную для body и wall-clearance, а look сохраняет свободный yaw/pitch без игрового weapon collision.
- `game-design-profile-core`: Profile описывает и валидирует числовые параметры participant wall-clearance через descriptor registry.
- `procedural-arena-generation`: Generator, validator и spawn safety используют effective wall-clearance там, где проверяется static player traversal.
- `participant-weapon-presentation`: Viewmodel/world weapon визуально retracts у static wall, не становясь gameplay collider или damage volume.

## Impact

Затрагиваются `docs/GAME_SPEC.md` §§2, 3, 6, 7 и журнал изменений; collision contracts/Rapier adapter, playable movement/reconstruction, game-design profile, procedural arena validation/spawn allocation, renderer weapon presentation и их tests. Внешних сервисов, новых зависимостей, сети и изменения игровых weapon rules нет.
