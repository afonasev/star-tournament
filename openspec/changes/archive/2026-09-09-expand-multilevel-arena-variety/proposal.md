## Why

`arena-recipes-v4` разнообразил одноуровневые арены, но его малые elevation zones не создают ни реального второго этажа, ни подземного обхода, ни пространства для самостоятельного боя на другом ярусе. Игроку нужны заметно разные карты: широкие залы и тоннели, рискованные балконы, галереи для обхода, полноценные двухъярусные арены и подвалы — без узких маршрутов, которые допускают лишь формальное прохождение capsule.

## What Changes

- Уточнение после просмотра `a82a048`: вместо общей основы с верхней плитой строить ограниченный каталог различных графов помещений. Закрытые самостоятельные этажи преобладают; открытые галереи, балконы и атриумы остаются менее частыми вариантами.
- Seed определяет структуру комнат и тоннелей, размещение переходов и локальные высоты потолков. У каждого помещения свой canonical ceiling; на одной карте сочетаются низкие широкие тоннели, комнаты и высокие залы. Верхний этаж имеет собственную сеть помещений и два разнесённых перехода.

- Добавить пять seed-selected семейств procedural recipes: широкий зал с длинным обходом, обходная верхняя галерея, балконы большого зала, две самостоятельные боевые арены по ярусам и основной этаж с подземным обходом.
- **BREAKING**: в новых arena definitions ввести explicit support/layer identity для regions, spawn anchors и links; обычные links не соединяют пространства лишь потому, что они пересекаются в XZ. Исторические definitions и profile snapshots остаются runnable через compatibility path.
- Добавить canonical slabs, floors, потолки подвала и двусторонние ramp/stairs transitions; каждый multi-level route получает не менее двух независимых переходов без jump-only, cliff и false route.
- Сделать чистую ширину combat tunnel, doorway и ramp/stairs profile-owned: не менее трёх effective participant capsule diameters во всех сечениях, углах и у стойкек; validator проверяет три боковых lanes, а не одну центральную линию.
- Расширить layer-aware navigation, physical validation, spawn/LOS/fairness и bot telemetry, чтобы они различали одинаковые XZ на разных ярусах и проверяли каждый значимый combat region.
- Добавить renderer-only material variants для hall, gallery, balcony и basement sectors, включая новые compatible wall/floor textures и decals в рамках существующего manifest/cache/quality budget.
- Сделать главные рампы выше и заметнее через profile-owned height/length/width и semantic presentation treatment.

## Capabilities

### New Capabilities

- `multi-level-arena-navigation`: Layer-aware deterministic routes, support selection and physical traversal for overlapping arena floors.

### Modified Capabilities

- `procedural-arena-generation`: Named recipes, canonical regions and acceptance gates expand from small raised zones to wide and multi-level combat arenas.
- `architectural-height-semantics`: Canonical support floors, slabs, headroom and tier transitions become explicit gameplay semantics.
- `game-design-profile-core`: Versioned generator profile exposes bounded dimensions, clearances and transition parameters for multi-level recipes.
- `bot-ai`: Bot route planning and telemetry must visit and recover across declared multi-level combat regions.
- `arena-presentation-style`: Renderer presents semantic hall, gallery, balcony and basement sectors without owning spatial truth.
- `arena-material-coverage`: The texture kit gains compatible sector variants without flat fallbacks or changes to gameplay identity.

## Impact

- Затронуты canonical arena schema/compiler/validators, collision projections, navigation/planner, spawn allocation, AI evaluation, profile catalog, renderer material manifest and texture lifecycle.
- Изменятся generator/profile/arena schema identities и content hashes только для новых generated arenas; старые replay/definitions сохраняют полную historical identity и прежний compatibility path.
- В scope входят muted in-app Browser playtest с keyboard/mouse и screenshots всех пяти семейств; также затрагивается split-screen performance budget, но не его UI/layout, input mapping, оружие, сетевой transport или ручной map editor.

## Утверждённое уточнение переходов

Устранить накопительное замедление grounded-персонажа при подъёме. Примерно половина переходов выбирается как лестница независимо для каждого seed/transition ID; доступны пары ramp/ramp, ramp/stairs и stairs/stairs. Настоящие canonical ступени проходимы бегом без прыжка. Камера сглаживает вертикальные толчки ступеней без изменения authoritative collision и попаданий.
