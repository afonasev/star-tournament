## Context

См. [proposal.md](proposal.md) — Why. Renderer уже получает immutable `ArenaDefinition` и authoritative participant colors, но использует временные холодные материалы и fixed `red`/`blue` team contract. Новое поведение затрагивает renderer, serializable match configuration/snapshot, standings/results и browser evidence; оно не должно влиять на geometry, collision, navigation или fixed-step simulation rules.

## Goals / Non-Goals

**Goals:**

- Ввести один versioned `clean-future-sport-v1` presentation token set для arena materials, lights, fog и participant readability.
- Перевести team domain с `red`/`blue` на `team-a`/`team-b`, сохранив цвет как authoritative configuration data и воспроизводимость match/replay.
- Сделать FFA palette расширяемой, но гарантировать детерминированный выбор попарно различимых swatches для roster до восьми.
- Сохранить zero-work pause/hidden lifecycle и существующие performance budgets.

**Non-Goals:**

- Добавлять GLB, texture files, post-processing, dynamic shadows, vertical gameplay geometry, новые gameplay правила, split-screen или palette-picker UI.
- Менять ArenaDefinition schema, collision world, spawn allocation, profile numeric balance или player camera/input.

## Decisions

### Presentation tokens остаются renderer-owned

`clean-future-sport-v1` будет неизменяемым presentation-only token set: off-white/pale-gray shells, navy floor, cyan/lime/orange accents, светлый background/fog, мягкий hemisphere/key/rim light и отдельный contrast treatment participant materials. Renderer применяет токены по уже существующему `floor`/`wall`/`accent` semantic slot.

Это сохраняет `ArenaDefinition` как единственную spatial truth и исключает appearance из arena hash/replay. Альтернатива — добавлять visual slots и параметры света в `ArenaDefinition`; она отвергнута, потому что заставила бы изменение материала менять collision-adjacent content identity без gameplay причины.

### Detail layer is flush and non-colliding

Renderer строит detail layer исключительно из существующих `ArenaBoxSurface`: тонкие panel seams и frames слегка вынесены наружу относительно wall/floor surface, а световые trims лежат заподлицо с ними. Для круглого portal renderer сначала находит зазор между коллинеарными wall segments на общей wall plane, затем добавляет дугу и две стойки строго вокруг этого уже существующего doorway. Один верхний центральный landmark выводится из центра combat regions и остаётся вне player clearance. Все эти meshes presentation-only: они не передаются в collision/query world, navigation/spawn validation или arena hash.

Детали не имитируют проход, дверь или cover там, где этого нет в `ArenaDefinition`: panel rhythms служат масштабом и спортивной стилизацией, а portal algorithm требует подтверждённый wall gap. Альтернатива — создавать отдельные фальш-объекты в центре маршрута; она отвергнута, потому что несоответствие визуальных и физических препятствий ухудшит читаемость.

### Architectural massing takes priority over micro-detail

Чтобы presentation приблизился к concept sketch, renderer обязан давать крупный читаемый ритм: cyan portal-shell обрамляет pale-gray inner arch, wall получает контрастные inset modules достаточного размера, а navy floor — широкие направляющие lane-полосы. Эти формы повторяют существующие surface planes и confirmed doorway gaps; они не могут становиться независимыми декорациями в свободном игровом объёме.

Альтернатива — увеличить число мелких seams и светящихся точек. Она отвергнута: это не меняет silhouette и оставляет арены похожими на технический blockout.

### Renderer-native architectural kit

Pass строит крупные формы из Three.js primitive/curve geometry, без GLB или texture pipeline: curved facade имеет внешний cyan shell, внутренний off-white reveal и массивные buttresses; wall bays повторяют один ограниченный модульный ритм; route guides собраны как широкие segmented lanes. Geometry крепится к planes/doorway gaps существующих surfaces и задаётся shared materials, чтобы сохранить renderer lifecycle и performance contract.

Альтернатива — начать GLB-пайплайн сейчас. Она отложена: пользователю нужен быстро проверяемый style pass, а manifest, pivot, LOD и collision-proxy для shipping assets являются отдельным pipeline change.

### Palette — pure canonical data, colors — configuration data

Будет введён pure-data registry vetted hex swatches больше восьми значений: стартовый набор включает cobalt `#3b82f6`, magenta `#d946ef`, coral `#fb7185`, violet `#8b5cf6`, emerald `#10b981`, gold `#f59e0b`, rose `#e11d48`, sky `#0ea5e9`, indigo `#6366f1`, mint `#14b8a6`. Team A/Team B default pair — cobalt/magenta; environment не использует эти цвета как large-area material.

`team-a` и `team-b` являются canonical simulation/configuration identifiers; user-facing labels всегда `Team A` и `Team B`. Team configuration хранит exact pair, а FFA configuration — exact participant colors. Registry валидирует шестизначный hex, отсутствие повторов в active roster и выбранные contrast-safe combinations; snapshot/replay несут уже resolved colors, а renderer не выполняет самостоятельный выбор.

Альтернатива — хранить только palette index или вычислять цвета в renderer. Она отвергнута: index привязывает save/replay к изменяемому порядку palette, а renderer choice ломает authoritative HUD/participant correspondence.

### Schema migration is explicit and deterministic

Match team type, configuration parsing, snapshots, replay compatibility and projections переходят atomically от `red`/`blue` к `team-a`/`team-b`. Версии compatibility contracts повышаются там, где прежний serialized payload больше не валиден; legacy payload receives stable actionable validation failure instead of silent remap.

Альтернатива — принять старые IDs как aliases. Она отвергнута: скрытая normalization создаёт две identity формы для одного match и усложняет canonical hashes.

### Readability uses material separation, not player glow alone

Arena materials ограничивают luminance/saturation backdrop, оставляют участникам более насыщенные swatches и дают soft rim contribution. Тесты проверяют token assignment и active-color uniqueness; in-app Browser подтверждает silhouette на representative small/medium/large frames, включая pause и scoreboard. Яркий outline/postprocess не используется: он повысит GPU cost и изменит aesthetic слишком резко.

### Verification is renderer-risk proportionate

Нужно расширить unit/component tests для tokens, palette/configuration validation, serialisation и UI labels. Затем выполняются typecheck, full tests, production build, performance gate, HTTP-подтверждённый dev stand и muted in-app Browser visual/playability smoke с screenshots всех затронутых surfaces. Pointer-lock/input contract не меняется, но visual test проводится в том же browser lifecycle.

## Risks / Trade-offs

- [Яркие light shells уменьшают separation с participant colors] → backdrop палитра исключает team/participant swatches, а Browser evidence проверяет дальний silhouette.
- [Изменение team IDs ломает fixtures, replays и tests] → атомарно поднять compatibility identities, обновить factory defaults и добавить rejection tests legacy payload.
- [Detail layer повышает draw calls и GPU load] → без textures/postprocessing/dynamic shadows, shared materials, ограниченное число лёгких box/ring meshes и обязательный performance gate.
- [Палитра без цветового picker UI выглядит недостаточно гибкой] → registry расширяем, а внешний UI редактирования сознательно вынесен за scope.

## Migration Plan

1. Добавить presentation tokens и pure palette registry с tests.
2. Мигрировать configuration, snapshot/replay, fixtures и UI projections на Team A/Team B и resolved colors.
3. Применить renderer material/light kit и contrast treatment без изменения arena projections.
4. Прогнать automated validation и muted browser/performance evidence на этой ветке.
5. При rollback вернуть один Git commit: legacy red/blue payload не будет частично поддерживаться на новой revision.
