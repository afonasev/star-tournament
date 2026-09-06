## Why

Визуальный kit уже размещает маршрутные лампы, но они не дают выраженного объёма от теней. Нужны локальные источники света, чтобы панели, порталы и роботы читались как объекты пространства, сохраняя яркий спортивный тон и стабильную игру на разных quality tiers.

## What Changes

- Добавляется renderer-only light rig `local-light-shadows-v1`: яркие лампы регулярно размещаются по eligible wall rhythm corridors, rooms и combat zones, а ограниченное число ближайших к камере ламп отбрасывает тени.
- Арена использует регулируемый medium-dark global fill; lamp fixtures становятся основными пространственными ориентирами, а participant rim light сохраняет силуэт роботов. В `Low` тени локальных ламп выключены; `Balanced` использует один shadow caster, `High` — два, `Ultra` — четыре.
- Добавляются настраиваемые metadata presentation profile для бюджета shadow casters, разрешения shadow map и мягкого радиуса тени; значения влияют только на renderer.
- Renderer включает PCF soft shadows, создаёт и освобождает shadow resources вместе с presentation lifecycle, и не допускает, чтобы декоративный свет менял collision, navigation, ArenaDefinition, snapshot, replay, RNG или state hash.
- Performance gate и browser visual QA дополняются проверкой quality tiers и читаемости silhouette в мягких тенях.

## Capabilities

### New Capabilities

- `local-light-shadows`: Качество-зависимые локальные светильники с ограниченным числом мягких renderer-only теней.

### Modified Capabilities

- `arena-presentation-style`: Светлая спортивная читаемость получает контролируемые мягкие локальные тени.
- `graphics-quality-settings`: Graphics presets определяют budget локальных теней без изменения матча.
- `browser-runtime-foundation`: Presentation performance gate учитывает shadow resources и их lifecycle.

## Impact

- Затрагивает `src/render/firstPersonRenderer.ts`, presentation profile, их тесты и browser/performance evidence.
- Не добавляет gameplay-механику, коллизии, assets, внешние зависимости или изменения ввода.
