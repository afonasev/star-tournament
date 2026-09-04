## Context

See proposal.md. `firstPersonRenderer` уже получает participant position, yaw, color, weapon identity и versioned combat events, но собирает fixture из аналитических hit volumes и показывает только отдельную muzzle sphere. Renderer уже является presentation-only consumer immutable snapshots.

## Goals / Non-Goals

**Goals:**

- Поставить detail-ready local GLB LOD0/LOD1 для лёгкого stylized robot, double-barrel weapon и first-person forearms через existing `clean-future-sport-glb-v1` manifest/cache.
- Использовать existing participant color и weapon semantic identity как единственные inputs для визуального выбора.
- Добавить camera child viewmodel и event-driven recoil без ввода renderer state в simulation.
- Сохранить single-seat pointer-lock lifecycle, view center и existing pixel-budget/cadence contract.

**Non-Goals:**

- Не менять hit volumes, participant state, collision, combat, profile numbers, HUD contract или gameplay input map.
- Не добавлять skeletal animation, новые оружия, gamepad/split-screen, ботов или network model.
- Не делать внешний вид robots/weapon сериализуемой частью replay.

## Decisions

### Общий GLB и texture manifest для participant assets

Participant assets расширяют уже поставленный `clean-future-sport-glb-v1` manifest стабильными keys для robot LOD0/LOD1, world shotgun LOD0/LOD1 и first-person viewmodel. Они используют тот же `GLTFLoader` template cache, immutable clone ownership, meter/+Y-up/+Z-front/pivot contract и renderer disposal, что arena modules. Texture maps адресуются и загружаются тем же presentation-owned contract и quality tiers; player assets не создают второй loader, runtime-generated canvas-texture path или отдельные residency rules. Unknown identity получает нейтральный fallback.

Альтернатива — сохранить primitive factory. Она отклонена: создание второго способа для модели и texture materials расходится с уже принятым arena asset pipeline и не даёт authorable LOD.

### Composite robot отделён от hit-volume presentation

Robot GLB создаёт stylized silhouette: dark navy joints, off-white shell, round chest core, visor, wide shoulder panels и rear beacon. Participant color применяется только к renderer-cloned identity materials поверх общего texture set. Hit volumes остаются симуляционным query contract и не определяют форму или количество render meshes. Destroyed state меняет только видимость/pose presentation group.

Альтернатива — окрасить существующие analytical volumes. Она не даёт утверждённый гуманоидный силуэт, читаемость рук и оружия.

### Один weapon factory для world и viewmodel

Weapon GLB несёт two-barrel silhouette, energy chamber и color indicator. World attachment позиционируется относительно robot hand anchor и participant yaw. Viewmodel GLB получает отдельный camera-local pose и две forearm groups; он виден только в local camera и не может попадать в simulation projection.

Альтернатива — нарисовать оружие DOM overlay. Это не даст корректную перспективу, muzzle position или освещение сцены.

### Event-driven first-person recoil

Renderer сохраняет presentation-only recoil expiry/offset по аналогии с current transient muzzle feedback. Только новый `shotgun-shot` event запускает offset и обе flashes; эффект не добавляет tick, не меняет HUD и сбрасывается при rewind/dispose.

### Runtime boundaries and input

Keyboard/mouse action map, pointer lock, pause/resume, mouse focus и simulation runner не меняются. Один local-seat camera получает viewmodel; current single-seat viewport остаётся единственным поддерживаемым layout. Будущий split-screen обязан создавать independent camera-local viewmodel на seat, но не входит в этот change. Нет controller reconnect или online behavior, так как change не затрагивает эти adapters.

## Risks / Trade-offs

- [Дополнительные GLB meshes/materials/textures увеличат draw calls, triangles и residency] → shared template cache, renderer-owned texture lifecycle, LOD/quality tiers, disposal вместе с renderer и обязательный performance gate.
- [Viewmodel может закрыть action] → нижняя-правая camera-local pose, сохранение чистого central crosshair и browser screenshot gate.
- [Цвет может конфликтовать с ареной] → only participant-authoritative color на core/visor/shoulders; shell остаётся нейтральным, environment palette не меняется.
- [Renderer может случайно стать источником gameplay state] → factories принимают immutable projection и events; tests проверяют отсутствие изменения snapshot/replay contract.

## Migration Plan

1. Добавить factories и renderer integration за существующими semantic identities.
2. Проверить unit/build/strict spec и production performance gate.
3. Запустить muted branch dev stand, выполнить pointer-lock keyboard/mouse playtest и снять first-person/third-person screenshots.
4. При regression убрать factories/viewmodel в одном commit: simulation и saved snapshots останутся совместимы.
