## Why

Выбранная release revision `prototype-v1 v7` валидна и передаётся в browser bootstrap, но freshly-created simulation snapshot отклоняется собственным compatibility parser. Это блокирует запуск матча и обязательный browser QA.

## What Changes

- Связать проверку identity сериализуемого playable snapshot с exact validated profile текущего match startup.
- Сохранить явное отклонение snapshots, созданных для другой profile identity/hash.
- Добавить регрессионные unit/integration тесты bootstrap для release revision v7 и несовместимой identity.

## Capabilities

### New Capabilities

<!-- Нет. -->

### Modified Capabilities

- `game-design-profile-core`: выбранная exact revision должна быть совместима с её собственным snapshot и оставаться compatibility boundary для другого profile.
- `browser-runtime-foundation`: browser startup валидного выбранного profile не должен падать до первого gameplay tick из-за внутренней snapshot identity проверки.

## Impact

Затрагиваются snapshot parser/serializer, playable step/replay boundary и browser bootstrap tests. Renderer, arena, input, gameplay rules, profile contents и внешние зависимости не меняются.
