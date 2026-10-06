## Why

Текущая процедурная арена остаётся аналитическим холодным blockout: маршрутная структура валидна, но пространство не выражает спортивный характер Star Tournament и не даёт достаточно устойчивого контраста для участников на телевизоре. Сейчас, когда подтверждён first procedural playable slice, необходимо заменить временную presentation-палитру утверждённым `clean-future-sport-v1` material kit без изменения симуляции или топологии.

## What Changes

- Добавить presentation capability `clean-future-sport-v1`: renderer-native modular architectural kit из крупных двухцветных curved portal facades, повторяемых wall buttresses/panel bays, читаемых floor route guides, cyan/lime/orange route accents, верхнего центрального light landmark, мягкого яркого освещения и сдержанного haze для одноуровневых procedural arenas.
- Сделать материал и освещение renderer-derived presentation: semantic `ArenaDefinition` и collision/navigation/spawn projections остаются неизменным источником spatial truth.
- Обеспечить читаемый контраст живых участников с окружением; material kit не использует participant или team colors для архитектуры.
- **BREAKING** Заменить пользовательские team identities `red`/`blue` на `Team A`/`Team B`, назначая им выбираемую контрастную пару цветов.
- Для FFA использовать расширяемую vetted palette и назначать одновременно присутствующим участникам попарно различимые цвета; roster по-прежнему ограничен восемью участниками.
- Проверить обновлённый renderer в production build, performance-gate и in-app Browser с выключенным звуком; приложить актуальные screenshots изменённых состояний.

Не входят в scope: новая topology арен, вертикальные маршруты, collision geometry, игровые механики, оружие, input/split-screen, модели/GLB-ассеты, сетевой режим, новые меню или редактирование палитры игроком.

Открытое решение: точный набор стартовых team/FFA swatches и метрики их контраста будут определены в design artifact; они должны соответствовать уже утверждённому принципу различимости, но не меняют simulation roster limit.

## Capabilities

### New Capabilities
- `arena-presentation-style`: утверждённый analytical material kit, свет и readability contract для procedural arena renderer.

### Modified Capabilities
- `match-session-lifecycle`: team identities и authoritatively stored participant colors переходят от fixed red/blue к Team A/Team B и расширяемой accessible palette.
- `match-loop-ui`: standings и results показывают Team A/Team B и authoritative contrast-safe participant/team colors.
- `browser-runtime-foundation`: renderer применяет style kit к одной validated ArenaDefinition и сохраняет presentation lifecycle/performance contract.

## Impact

Затрагиваются `docs/GAME_SPEC.md` §§2, 4, 7–8, renderer и его tests, roster/configuration validation, match UI projections и browser/performance evidence. Новые зависимости, игровые правила, spatial schema и simulation transforms не добавляются; существующий Three.js renderer остаётся единственной WebGL-технологией.
