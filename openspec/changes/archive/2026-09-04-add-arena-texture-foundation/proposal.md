## Why

Текущий GLB architectural kit задаёт силуэт арены, но пока не имеет shipping texture language и управляемого качества для больших телевизоров. Первый texture pass делает `clean-future-sport-v1` более читаемым и масштабируемым до 4K display output, сохраняя неизменными игровое пространство и детерминированную идентичность матча.

## What Changes

- Ввести `clean-future-sport-texture-v1`: повторяемые PBR material sets для wall/floor surfaces и atlas-backed decals для уникальных route и sector markings.
- Определить базовый texture contract (base color, normal, ORM), detail-tier для будущего повышения детализации и GPU-compressed shipping variants.
- Добавить renderer-only quality profile с пресетами Low, Balanced, High и Ultra, 4K display output и отдельными controls render scale, texture quality, LOD, decals и post-processing.
- Зафиксировать texture-residency budgets и обязательную browser performance/playtest evidence для texture и settings pass.
- Исключить из scope любые изменения `ArenaDefinition`, collision, navigation, spawn, simulation, snapshot, replay, RNG и state hash.

## Capabilities

### New Capabilities

- `graphics-quality-settings`: DOM-меню renderer-only quality presets и доступных отдельных графических параметров.

### Modified Capabilities

- `arena-presentation-style`: material kit получает hybrid tiling/atlas texture contract и правила presentation-only detail tiers.
- `browser-runtime-foundation`: browser runtime применяет graphics-quality profile, 4K display output и texture-residency budgets без изменения simulation/lifecycle guarantees.

## Impact

Затронуты renderer material/asset manifest, browser presentation lifecycle, React DOM settings surface, локальные texture assets и performance evidence. Изменение опирается на `clean-future-sport-v1`, `clean-future-sport-glb-v1` и требования `docs/GAME_SPEC.md` §§2, 7–9; новые runtime-зависимости не предполагаются. Открытых продуктовых развилок для первого pass нет; device-specific FPS/thermal thresholds вне portable gates остаются будущей калибровкой согласно §10.
