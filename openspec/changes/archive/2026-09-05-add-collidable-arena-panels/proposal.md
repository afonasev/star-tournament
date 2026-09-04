## Why

Крупные текстурированные panel bays визуально сужают пути, но игрок может проходить сквозь них. Это создаёт collision/render divergence и делает узкие проходы нечитаемыми.

## What Changes

- Сделать крупные wall-bay и portal-facade panels canonical collision proxies.
- Включить proxies в `ArenaDefinition`, navigation, spawn validation, snapshot/replay identity и state hash.
- Оставить мелкие trim, seams, decals, route guides и landmark presentation-only.
- Отклонять generation, если collidable panel создаёт непроходимую щель или закрывает обязательный route.

## Capabilities

### New Capabilities

- `architectural-panel-collision`: классификация крупных панелей и canonical collision proxies.

### Modified Capabilities

- `procedural-arena-generation`: validation учитывает collidable architectural panels.
- `collision-query-foundation`: static world детерминированно включает panel proxies.
- `arena-presentation-style`: visual panels должны совпадать с canonical collision contract.

## Impact

Затронуты `ArenaDefinition`, procedural generator/validator, collision projection, replay/hash tests и renderer attachment. Player impact: крупная визуальная панель всегда блокирует capsule; мелкий декор — нет. Решение следует `docs/GAME_SPEC.md` §§2, 7–8.
