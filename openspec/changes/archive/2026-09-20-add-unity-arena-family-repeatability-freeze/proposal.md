## Why

ARENA-1/2 задают immutable identity, а ARENA-4 проверяет definition до projection, но нет отдельного контракта, который доказывает, что повторное построение выбранного family не дрейфует между generator, validator, Unity projection и owned navigation context. Нужен узкий ARENA-5 freeze gate до дальнейших lifecycle/replay решений.

## What Changes

- Добавить ARENA-5 repeatability contract для одинаковых explicit generation input, canonical profile identity и selected family.
- Зафиксировать immutable definition/profile snapshot как единственный источник для generator, ARENA-4 validator, projection и navigation context; отклонять identity/profile drift с устойчивой диагностикой до использования Unity scene.
- Добавить representative EditMode и PlayMode evidence для всех пяти families, без изменения player-visible topology или input.

## Capabilities

### New Capabilities

- `unity-arena-family-repeatability`: Native Unity contract для repeatable identity и frozen hand-off arena family definition/profile между generation и derived adapters.

### Modified Capabilities

- `procedural-arena-generation`: Generation и derived projections получают explicit freeze/repeatability requirements для native family definition.
- `multi-level-arena-navigation`: Owned navigation context принимает только frozen definition/profile snapshot с совпадающей arena identity.

## Impact

Затрагиваются `ArenaGenerator`/family catalog/identity, ARENA-4 validator gate, `ProvingArena` projection и native navigation context, а также focused EditMode/PlayMode tests. `docs/GAME_SPEC.md` §§2, 8 и 11 задают границы: replay, retry/fallback, authoring/editor pipeline, delivery и physical Player acceptance не входят в change.
