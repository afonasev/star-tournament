## Why

В playable матче участники пока визуализируются аналитическими объёмами попадания, а локальный игрок видит только вспышку выстрела. Это не позволяет с первого взгляда различать соперников по цвету и не даёт ощущение оружия в first-person бою.

## What Changes

- Добавить renderer-only модель лёгкого спортивного робота для живых участников и его визуальное тело после уничтожения.
- Закрепить контрастную цветовую идентичность участника на chest core, visor, плечевых панелях и rear beacon, не занимая архитектурные accent-цвета арены.
- Добавить видимый двухствольный энергодробовик у участника и camera-attached first-person viewmodel с двумя роботизированными предплечьями, двойной вспышкой и умеренной отдачей.
- Заменить renderer-native primitive-макеты на локальные GLB LOD0/LOD1, адресуемые через уже поставленный `clean-future-sport-glb-v1` manifest/cache; model и texture maps используют тот же renderer-owned lifecycle и quality tiers, что architectural assets.
- Обновить каноническую visual-договорённость в `docs/GAME_SPEC.md` и создать handoff-задачу.

## Capabilities

### New Capabilities

- `participant-weapon-presentation`: renderer-only модели роботов и читаемое оружие в third-person и first-person представлении.

### Modified Capabilities

- `double-barrel-shotgun-combat`: визуальная обратная связь дробовика теперь включает видимую модель и двойную вспышку, не меняя правила атаки.

## Impact

- Затрагиваются общий GLB manifest/cache, локальные player/weapon GLB и texture assets, `src/render/firstPersonRenderer.ts`, его тесты и renderer presentation helpers; simulation snapshots и combat reducer остаются источником истины и не получают model state.
- Нужны WebGL/browser и performance проверки, так как изменяются геометрия, материалы, эффекты и draw calls.
- Затрагиваются §§2, 3, 7, 8, 9 и журнал `docs/GAME_SPEC.md`; новые оружия, skeletal animation, split-screen, боты и сеть не входят в scope.
