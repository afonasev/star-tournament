## Why

В playable arena ещё видны мерцающие узкие стыки, однотонная cyan-стена и потолочные плоскости без material treatment. Текущий canvas-first texture pass не передаёт утверждённый `clean-future-sport-v1` visual language и мешает чтению пространства на игровой дистанции.

## What Changes

- Устранить coplanar presentation geometry на wall/portal/GLB-деталях вместо зависимости от depth bias как основного решения.
- Дать wall, accent и ceiling-facing архитектурным поверхностям полный согласованный material coverage с off-white/pale-gray panel language, а floor оставить dark navy.
- Заменить текущие растровые wall/floor maps на более близкий к утверждённому clean-future-sport panel kit и настроить UV/repeat по semantic surface class.
- Сохранить renderer-only ownership: collision, navigation, spawn, `ArenaDefinition`, snapshots, replay и hashes не меняются.

## Capabilities

### New Capabilities

- `arena-material-coverage`: Проверяемое покрытие всех видимых архитектурных surface classes и отсутствие renderer-originated z-fighting.

### Modified Capabilities

- `arena-presentation-style`: Material kit получает требования к полному coverage и читаемому off-white спортивному panel language.

## Impact

Затрагиваются `src/render/firstPersonRenderer.ts`, presentation texture assets и renderer tests; новых runtime-зависимостей, GLB collision proxies или изменений симуляции нет. Изменение следует `docs/GAME_SPEC.md` §§2 и 7–9.
