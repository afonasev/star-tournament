## Why

Planner уже управляет настоящими ботами, но доступен только в development fixtures. Игроку нужен обычный setup для solo и смешанных матчей согласно GAME_SPEC §§2, 5, 6.

## What Changes

- Native setup: 1–4 человека, 2–8 участников суммарно, добавление/удаление ботов и индивидуальные Салага/Боец/Ветеран.
- FFA и две непустые команды с назначением каждого человека и бота; устройства нужны только людям.
- Start запускает общий motor/combat/planner, Repeat сохраняет frozen состав, сложности, цвета и профили; выход освобождает матч.
- Люди и AI используют существующий TrooperVisual и семь сохранённых v2 clips без root motion.
- Проверки EditMode/PlayMode, Mac build и muted native Player. Не включены archive, main integration, deploy, новые AI возможности, procedural arenas, runtime Balance Lab и full migration.

## Capabilities

### New Capabilities
- `unity-bot-setup`: обычный native setup смешанного состава и его lifecycle.

### Modified Capabilities
Нет.

## Impact

ProvingGround, новый data-only setup draft, native UI и тесты. Зависимости: participant roster и planner d4844e4, существующие Trooper assets. Новых пакетов нет. Продуктовых развилок внутри порученного среза нет; physical devices/TV/art acceptance, целевое железо/performance и full migration остаются открытыми.
