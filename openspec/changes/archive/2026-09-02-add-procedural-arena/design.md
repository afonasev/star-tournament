## Context

См. `proposal.md` — Why. Сейчас `ArenaDefinition` v2 содержит только boxes и плоский список anchors; collision и renderer уже потребляют её, но browser bootstrap жёстко использует fixture arena, а initial/respawn transforms приходят из `CombatSliceScenario`. Snapshot хранит только arena identity, replay не несёт definition, а `GameDesignProfile` не содержит generator rules.

Текущий runtime остаётся single keyboard/mouse seat с одним viewport. Change не меняет action map, pointer-lock/focus, pause lifecycle, gamepad, viewport policy или online boundaries; существующие browser acceptance gates для movement/look/jump/fire остаются обязательными.

## Goals / Non-Goals

**Goals:**

- Одна canonical spatial authority до первого simulation tick.
- Играбельные single-level `small`, `medium`, `large` recipes с route choice и воспроизводимостью.
- Детерминированное initial/fixture spawn allocation для FFA и teams до восьми participants.
- Self-contained replay относительно arena content.
- Fail-closed validation с явным пользовательским fallback.

**Non-Goals:**

- Полная navmesh/pathfinding система для bots, universal competitive spawn director или online compatibility matrix.
- Vertical traversal, moving geometry, pickups/hazards, GLB kit и финальный art direction.
- Изменения input, camera, HUD cadence, split-screen либо gamepad behavior.

## Decisions

### ArenaDefinition v3 хранит semantic model, а adapters строят projections

Definition получает `size`, полную generator profile identity, `regions`, `links`, `surfaces`, `spawnRegions` и `spawnAnchors`. Region описывает gameplay role и planar bounds; link соединяет regions и помечает exposure; surface остаётся аналитическим box с material slot; anchor ссылается на spawn region. Все массивы нормализуются по semantic ID до hash.

Collision продолжает создавать Rapier static colliders только из sorted surfaces. Navigation projection использует region/link graph; spawn allocator использует regions/anchors; renderer строит meshes из surfaces/material slots. Derived hashes допускаются для diagnostics, но gameplay snapshot хранит только arena identity и participant state.

Альтернатива — сериализовать отдельные collision/nav/render payloads — отклонена: она создаёт несколько spatial authorities и требует cross-payload synchronization.

### Constructive generator вместо свободного BSP/rejection search

`broken-ring-v2` сначала строит size-specific macrograph, затем размещает замкнутый ортогональный ступенчатый perimeter, связанные контуры комнат, парные стеновые цепочки коридоров, дверные проёмы и четыре spawn regions. Wall-run helper разбивает непрерывную цепочку только в явно объявленных doorway intervals; углы и примыкания соединяются без видимых coplanar overlaps. После размещения архитектуры constructive gap-sealing закрывает непрерывными solids каждый промежуток уже полного диаметра профильной капсулы, поэтому визуально открытая щель либо действительно проходима, либо отсутствует. Отдельные cover boxes примыкают к архитектуре как buttress/pilaster и не подменяют комнату или коридор. Seed выбирает детерминированные допустимые offsets, doorway side и material accents через arena-local xorshift stream; match RNG не читается. Technical layout fractions и canonical ordering являются частью generator version, а wall thickness, corridor/door widths, perimeter depth, spawn separation и остальные balance/readability budgets находятся в `prototype-v1.rules.arenaGenerator.presets`.

Small использует один hub, две боковые комнаты и компактный двухмаршрутный обход; medium добавляет вторую поперечную связь и более глубокие боковые ниши; large использует два связанных hubs, дополнительные комнаты и три различимых коридорных маршрута. Каждый recipe имеет собственный ступенчатый silhouette и набор wall runs, а не является scale transform.

Альтернатива сохранить `broken-ring-v1` и только увеличить/сдвинуть отдельные boxes отклонена: она не создаёт архитектурной непрерывности, читаемых дверей или настоящих коридоров и воспроизводит наблюдаемую проблему «объекты на полу».

Альтернативы BSP и authored GLB modules отложены: первый усложняет bounded validation, второй требует неутверждённого asset/visual pipeline.

### Двухступенчатая validation

Pure validator сначала проверяет schema, canonical order, references, bounds, links и content hash. Gameplay validator затем использует active profile: расширяет solids на capsule clearance, проверяет свободные anchors, graph connectivity и single-link resilience, строит initial FFA/team allocations и вычисляет versioned distance/LOS metrics.

Validation возвращает immutable `ArenaValidationReport` с contract version, arena/profile identities, supported modes, metrics и стабильными errors. Report не входит в simulation hash; startup принимает только `accepted` report с совпадающими identities.

Для первого constructive recipe geometry формируется by construction, поэтому bounded attempts защищают от неожиданного invalid seed, а не служат основным алгоритмом поиска.

### Spawn regions и stable allocator

Generator создаёт минимум 12 slots и четыре spatial regions; shipped presets используют запас slots сверх минимального контракта. Initial FFA allocator выбирает следующий slot deterministic farthest-first относительно уже занятых стартовых позиций и отклоняет пары ниже preset minimum opponent separation. Teams выбирает seed-determined противоположные primary regions и заполняет соседние slots внутри союзной группы, сохраняя opponent separation между primary regions. В одном tick participants сортируются по semantic ID; один slot назначается максимум одному life.

Fixture respawn переиспользует allocator: исключает slots с capsule overlap живых participants, предпочитает region с союзниками и без enemy LOS, затем navigation distance. Если primary region заполнена, выбор продолжается по stable region/slot order. Этот slice не заявляет финальный universal spawn director, но spatial authority и distinct-slot behavior уже общие.

### Collision world проецирует весь живой roster

Runtime создаёт participant capsules из serializable simulation state, а не только для local-seat. Перед каждым movement query adapter синхронизирует полный набор живых participants: отсутствующая в projection capsule отключается на время смерти и детерминированно возвращается при respawn. Kinematic controller учитывает static surfaces и все остальные активные participant capsules, исключая собственную; stationary fixtures остаются fixed derived colliders и не получают push impulse.

Fixture participant position является feet anchor для renderer/hit-volume contract, поэтому collision projection добавляет profile-derived `halfHeight + radius` только по вертикали. Local-seat position уже хранит центр capsule. Это сохраняет одну simulation authority без переноса presentation origin в collision contract.

### Match configuration разрешает seed до runtime

Match configuration schema получает `arenaSize` и concrete `arenaSeed`. Setup хранит draft seed как nullable string: пустое поле при Start превращается в unsigned 32-bit seed, ручное поле валидируется. После создания configuration auto/manual различие не влияет на simulation.

`StarTournamentApp` генерирует и сохраняет `ArenaDefinition` рядом с exact configuration. Repeat меняет только session generation counter и передаёт прежнюю definition; выход в setup сбрасывает session, а следующий пустой seed разрешается заново.

### Явный fallback остаётся отдельной identity

`PROCEDURAL_FALLBACK_ARENA` является shipped validated definition с собственной generator identity. Generation failure остаётся на setup surface и предлагает повтор, новый seed и fallback. Fallback передаётся как фактическая definition; исходный failed seed не копируется в snapshot.

### Replay включает ArenaDefinition один раз

Replay schema bump добавляет `arenaDefinition`. Parser валидирует definition и точное совпадение с `initialSnapshot.arena`. Fixed-step snapshot не дублирует geometry. Collision reconstruction получает definition из replay adapter; несовместимая schema/profile/collision identity отклоняется до frames.

### Presentation остаётся аналитической и bounded

Renderer сохраняет `floor/wall/accent`, но получает только render projection из definition. Generator profile ограничивает surface/cover budgets, а renderer может объединять geometry по material только если representative large seed превышает текущие provisional draw-call gates. Performance telemetry остаётся presentation-only.

## Risks / Trade-offs

- [Graph считается проходимым, но geometry перекрывает маршрут] → gameplay validator выполняет capsule-clearance sampling между linked region portals и negative fixtures.
- [Asymmetric seed даёт mode advantage] → versioned team/FFA metrics, corpus по всем presets и browser review representative worst cases.
- [Small arena с восемью participants слишком хаотична] → small preset получает отдельный minimum opponent separation, а corpus и browser acceptance проверяют farthest-first placement восьми FFA participants; hard roster cap не вводится.
- [Ступенчатый perimeter оставляет доступную геометрию за контуром] → замкнутая wall chain проходит structural validation, а capsule sampling проверяет отсутствие перехода наружу через стыки.
- [Дверь видна, но capsule не проходит] → doorway interval и corridor width используют profile metadata, а gameplay validator проверяет clearance по всей связующей полосе.
- [Мёртвый participant остаётся невидимым препятствием либо respawn теряет collider] → projection содержит только живые capsules; adapter отключает отсутствующие runtimes и умеет повторно активировать либо создать capsule по semantic ID.
- [Schema bumps ломают старые snapshots/replays] → текущий prototype использует migration-by-rejection с точной unsupported identity error; production saves ещё не обещаны.
- [Large recipe увеличивает draw calls/collision cost] → generator budgets, representative large benchmark и возможность material batching без изменения definition.
- [Retry deterministic failure повторяет результат] → primary recovery — new seed либо explicit fallback; retry полезен для transient startup error и не скрывает прежнюю причину.

## Migration Plan

1. Обновить `GAME_SPEC`, generator/profile version и planning contracts вместе с rejection tests.
2. Адаптировать diagnostic/collision fixtures к v3 semantic model.
3. Включить `broken-ring-v2`, architectural validator и projections без изменения spatial-authority boundary.
4. Перевести initial snapshot, respawn, renderer и replay на generated definition.
5. Добавить setup size/seed/error flow и переключить production default.
6. Пройти strict OpenSpec validation, automated/build, browser performance и physical playtest; rollback выполняется откатом единственного change-коммита.
