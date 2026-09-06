## Why

Текущий `broken-ring-v2` даёт корректную, но слишком узнаваемую одноуровневую арену: один тип планировки, единый пол и ограниченный набор архитектурных акцентов быстро перестают создавать новые тактические ситуации. Нужен следующий procedural slice, в котором seed выбирает действительно разные читаемые карты, не жертвуя воспроизводимостью, честностью FFA/teams и скоростным темпом arena shooter.

## What Changes

- Заменить единственное семейство «Разорванное кольцо» набором именованных layout recipes для `small`, `medium` и `large`: большие залы, split-atrium с рампами, service-loop и courtyard с флангами, комнатами и коридорами.
- Добавить canonical семантику малых высотных зон и широких рамп; маршруты, spawn safety и fairness учитывают реальную достижимость между уровнями.
- Добавить canonical барьеры разных высот, блокирующие только движение capsule, но не блокирующие hitscan/LOS.
- Добавить непростреливаемые полупрозрачные окна как часть wall shell и presentation-only простреливаемые wall-relief niches, не образующие доступных игроку укрытий.
- Расширить renderer-only material/light kit: варианты panel/floor texture sets, sector accents, потолочные и настенные лампы, локальная световая окраска и новые арки/portal treatments.
- Расширить versioned generator profile и validation metrics для выбора recipes, перепадов высот, проходимости рамп, movement-only барьеров и разнообразия компоновок.

## Capabilities

### New Capabilities

_Нет._

### Modified Capabilities

- `procedural-arena-generation`: генератор получает несколько layout recipes, elevation/ramp semantics, movement-only blockers, окна и wall-relief niches с проверяемыми route/fairness gates.
- `game-design-profile-core`: profile получает versioned, descriptor-backed generator budgets для recipes, высот, рамп, barriers и material variation.
- `collision-query-foundation`: static collision и hitscan occlusion различают movement-blocking и projectile-blocking arena semantics.
- `architectural-height-semantics`: canonical низкие barriers и elevation surfaces получают явную семантику вместо неявных box heights.
- `arena-presentation-style`: renderer показывает новые архитектурные формы, texture variation и локальный свет, оставаясь derived presentation от validated arena.
- `arena-material-coverage`: material contract покрывает варианты wall/floor/sector treatment без flat fallback или расхода participant colours.

## Impact

- Затронуты `src/arena/`, `src/collision/`, `src/combat/`, `src/ai/`, `src/profiles/` и `src/render/`, а также deterministic, property-based, collision/occlusion, renderer, performance и browser-playtest проверки.
- Изменятся versioned generator/profile/validation identities и canonical arena content hash; старые replay с прежним generator version остаются воспроизводимыми через свою полную definition.
- Новые GLB или texture assets остаются локальными и проходят существующие manifest, LOD, texture-budget и lifecycle gates; сетевой режим, split-screen UX, оружейный баланс и input mapping не входят в scope.
- Требуется muted in-app Browser playtest с keyboard/mouse, включая рампы, барьеры, окна, маршруты и актуальные скриншоты каждого изменённого визуального состояния.
