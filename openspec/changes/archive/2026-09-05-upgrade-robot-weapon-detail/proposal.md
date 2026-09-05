## Why

Текущие manifest-addressed GLB участника и оружия выполняют контракт первого visual slice, но остаются слишком схематичными: на игровой дистанции робот, world weapon и first-person viewmodel не дают уровня формы и функционального чтения, ожидаемого от `clean-future-sport-glb-v1`. Нужен detail pass без расширения боевых правил, collision или asset lifecycle.

## What Changes

- Выполнить второй, существенно более глубокий authoring pass для содержимого LOD0/LOD1 трёх существующих local GLB asset pairs: лёгкого робота, world-view двухствольного энергодробовика и first-person рук/оружия.
- Сохранить stable manifest keys, meter/+Y-up/+Z-front/pivot contracts, existing texture lifecycle, graphics-quality LOD selection и presentation-only границу.
- Увеличить плотность читаемых форм на порядок: layered shell/armour panels, mechanical joint collars, hands/fingers, vents, cables и energy details у робота; recessed bores, multi-part barrel shrouds, coils, heat sinks и rails у оружия. При этом participant color остаётся явным на core, visor, плечах и rear beacon.
- Обновить canonical visual decision и handoff evidence; проверить GLB contracts, renderer lifecycle, browser performance и muted in-app Browser states.

## Capabilities

### New Capabilities

_Нет._

### Modified Capabilities

- `participant-weapon-presentation`: Требования к manifest-addressed participant GLB уточняются минимальной читаемостью высокодетализированных моделей и сохранением идентичности в world и first-person presentation.
- `arena-glb-asset-pipeline`: Existing `clean-future-sport-glb-v1` contract уточняется проверяемой authoring/validation границей для обновляемых participant asset pairs без нового loader или residency policy.

## Impact

- Затронуты шесть GLB в `public/assets/participants/`, их deterministic source-generation/audit tooling, renderer presentation tests и документация.
- Не затронуты simulation, snapshots/replay/state hash, hit volumes, collision, gameplay profile, network, input и `add-collidable-arena-panels`.
