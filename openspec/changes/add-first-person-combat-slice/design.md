## Context

См. `proposal.md` — Why и четыре delta specs. Main уже содержит fixed-step browser runner, canonical action/snapshot/replay primitives, immutable `prototype-v1`, oriented `ArenaDefinition` и принятый derived Rapier collision adapter. Текущий snapshot и renderer остаются диагностическими; input не владеет physical-device lifecycle, а combat state отсутствует.

## Goals / Non-Goals

**Goals:**

- Получить первый реально управляемый FPS-цикл в одном browser viewport, сохранив детерминированную simulation authority.
- Провести movement через backend-neutral capsule queries, а static hitscan occlusion — через тот же collision world.
- Сделать keyboard/mouse adapter, pointer lock, camera и HUD отдельными потребителями/поставщиками сериализуемого контракта.
- Доказать replay determinism и физическую играбельность movement/look/jump/fire в in-app Browser.

**Non-Goals:**

- Не вводить `LocalSeat`-roster, gamepad, split-screen layouts и несколько камер; единственный seat временно фиксирован bootstrap-конфигурацией.
- Не реализовывать match clock, scoring, смерть игрока, killcam, respawn, pickups, bots или online transport.
- Не выдавать primitive robot mannequins за production model/asset pipeline.
- Не менять collision backend identity и не добавлять dynamic rigid bodies.

## Decisions

### Playable runner связывает pure rules с collision port

Создаём versioned playable snapshot и синхронный `stepPlayableSimulation(snapshot, actionFrame, collisionPort, profile)`; rules package зависит только от backend-neutral query interface. Browser/headless runner владеет созданным Rapier world, перед tick проецирует capsule из snapshot и передаёт только frozen movement/ray results. Rapier world не становится authority, а acceleration, velocity, gravity, jump, orientation, ammo, targets и damage остаются в snapshot.

Альтернатива — обновлять player transform непосредственно в renderer/Rapier world — отклонена: replay, будущая сеть и split-screen observers получили бы скрытое mutable состояние.

### Snapshot schema заменяет diagnostic entity полноценным slice state

Новая schema хранит tick/RNG/profile/arena/collision identity, одного player (`position`, `velocity`, `grounded`, `yaw`, `pitch`, `health`), weapon (`ammo`, `shotSequence`, `nextAllowedShotTick`, previous fire state), ordered targets и события текущего tick. Старый diagnostic snapshot получает явную unsupported-schema ошибку; foundation tests мигрируются на новый initial builder.

Targets описываются отдельным versioned `combat-slice-scenario-v1` с semantic IDs, transforms и zoned hit volumes. Это проверяемый fixture с content hash, не balance profile и не procedural arena generator.

### Action map отделён от DOM events

Стабильные actions: `move-forward`, `move-right`, `look-yaw`, `look-pitch`, `jump`, `fire`. Keyboard/mouse adapter хранит held WASD/Space, накапливает `movementX/Y` только при pointer lock и формирует один canonical frame на simulation tick. Jump/fire являются transitions: adapter передаёт pressed state, а simulation сравнивает его с предыдущим snapshot state. При `pointerlockchange`, `blur` или `visibilitychange=hidden` adapter атомарно очищает held/accumulated state до остановки runtime.

Pointer lock запрашивается только явным click по start/resume overlay. Получение lock запускает fixed-step runner; потеря lock останавливает его. `Escape` не считается гарантированным DOM event — pause surface выводится из факта потери lock, как требует `GAME_SPEC` §5.

Gamepad APIs намеренно не читаются: их подключение/отключение не создаёт actions и не меняет единственный viewport.

### Coordinate convention и movement

World использует Y-up; yaw `0` смотрит вдоль `-Z`, positive yaw поворачивает вправо, positive pitch смотрит вверх. Input wish-direction строится в горизонтальной плоскости из snapshot yaw. Ground и air acceleration используют одинаковый ограничитель horizontal speed, но разные profile coefficients; отсутствие input применяет ground deceleration только в grounded state. Gravity интегрируется fixed delta, jump заменяет vertical velocity один раз, затем желаемый displacement передаётся capsule controller. Возвращённые position/grounded/contacts определяют новый snapshot; velocity пересчитывается из фактически разрешённого displacement, чтобы collision response не расходился с state.

Все принятые KCC settings перестают быть diagnostic-only: capsule radius/half-height, skin offset, autostep, snap и slope thresholds читаются из `prototype-v1` и передаются при создании controller. Collision adapter получает configuration argument и включает его в checkpoint/compatibility hash fixture этого runner.

### Camera является projection snapshot, не gameplay authority

Simulation применяет look deltas с profile sensitivity и clamp pitch; renderer ставит одну `PerspectiveCamera` в player position плюс profile eye height и выводит quaternion из yaw/pitch. FOV, near plane, far plane и визуальные feedback durations принадлежат profile descriptors, потому что влияют на camera/readability. Resize меняет только aspect/projection matrix.

Player body в собственном viewport не рендерится. Будущие remote/local-seat models будут отдельными presentation entities по semantic IDs.

### Shotgun rays и damage

Допустимый rising-edge fire увеличивает `shotSequence`, уменьшает ammo и создаёт directions из versioned concentric spread pattern. Pattern rotation/selection детерминированно выводится из canonical seed `{lifeId, shotSequence, tick}`; он не использует wall-clock или renderer RNG.

Для каждого normalized pellet сначала вычисляются analytically ordered intersections с zoned target volumes, затем static collision ray до profile range. Target hit применяется только если его distance строго меньше static hit distance с collision-contract epsilon. Результаты сортируются по pellet index, target semantic ID и zone; damage одного pellet равен `zoneFullDamage / pelletCount`. Damage каждого target суммируется до одной snapshot mutation, чтобы callback/order не влиял на floating-point sequence.

Hit volumes являются gameplay geometry: head sphere, torso box и paired limb capsules выводятся из scenario transform. Они не создают movement collision. Неактивные targets исключаются из subsequent hit queries и renderer показывает разрушенное состояние без collider mutation.

### Profile schema и descriptors

`prototype-v1` повышает schema/revision и добавляет стабильные paths:

- `rules.player.capsule.{radius,halfHeight,skinWidth}` и `rules.player.eyeHeight`;
- `rules.player.movement.{maximumGroundSpeed,groundAcceleration,groundDeceleration,airAcceleration,gravity,jumpSpeed,autostepMaxHeight,autostepMinWidth,snapToGroundDistance,maxSlopeClimbDegrees,minSlopeSlideDegrees}`;
- `rules.camera.{horizontalSensitivity,verticalSensitivity,maximumPitchDegrees,fieldOfViewDegrees,nearPlane,farPlane}`;
- `rules.weapons.doubleBarrelShotgun.{pelletCount,spreadDegrees,rangeMeters,cooldownSeconds,muzzleFeedbackSeconds,impactFeedbackSeconds,hitmarkerSeconds}`.

Каждое поле получает descriptor metadata и range/step validation; cross-field rules проверяют capsule/eye geometry, near/far camera relation, slope order и положительный pellet/range/cadence contract. Конкретные defaults живут только в shipped config и тестовых fixtures, не дублируются в narrative specs/design.

### Renderer и DOM

Three renderer строит static arena из того же `ArenaDefinition`, primitive robot targets из scenario и одну first-person camera. Transient shot events управляют muzzle flash, tracer/impact и target material state; renderer не изменяет health/ammo.

React получает глубокозамороженный gameplay UI snapshot: health, ammo, crosshair state, pointer-lock/pause state и optional debug diagnostics. Обычный HUD оставляет центр свободным; debug panel включается только query `?debug=1`, collision benchmark — существующим отдельным `?collisionBenchmark=1` и не запускается при обычной игре.

### Verification layers

- Unit/property tests: profile coverage, movement math, jump transitions, pitch clamp, pellet pattern, zoned ray intersections, damage/order, snapshot/replay round-trip.
- Integration tests: playable runner + real collision fixture, wall/step/slope/landing, static occlusion, lifecycle и render-cadence hashes.
- Browser: HTTP-confirmed branch stand; click-to-lock, WASD/strafe, mouse look, jump/land, fire/ammo, hit/destroy, lock loss/resume, resize, screenshot, DOM/canvas identities и clean console.

Autotests и виртуальные events не считаются доказательством physical mouse/keyboard playability; acceptance требует in-app Browser interaction.

## Risks / Trade-offs

- [Pointer lock automation отличается от ручного браузера] → проверять реальный lock state и visual/state deltas; при browser-policy ограничении явно оставить physical-input gate незакрытым.
- [Sync simulation step вызывает много Rapier queries] → переиспользовать один initialized world, профилировать fixed ticks и сохранить collision gate; не переносить authority в renderer ради скорости.
- [Floating-point damage зависит от hit order] → canonical group/sort, один ordered reduction на target и exact replay hashes.
- [Primitive mannequins создают ложное впечатление готовых bots/assets] → маркировать их target fixture в UI/debug и оставить AI/GLB отдельными changes.
- [Permanent debug chrome мешает игре] → production surface compact by default, diagnostics только по explicit query.
- [Snapshot schema migration ломает старые diagnostic replays] → versioned rejection и обновлённые fixtures; production save compatibility ещё не обещана.

## Migration Plan

1. Расширить profile schema/descriptors и playable snapshot/action contracts с миграцией tests.
2. Добавить pure movement/combat rules и scenario fixture, затем collision-backed runner.
3. Подключить browser input/pointer lock, first-person renderer и compact HUD за новым bootstrap.
4. Провести automated и in-app Browser acceptance, обновить `GAME_SPEC` фактическими параметрами identity/ограничениями.

Rollback — один change-коммит: возврат к diagnostic runtime не требует миграции пользовательских сохранений, поскольку production save ещё отсутствует.

## Open Questions

Нет. Visual polish, физический gamepad, split-screen layout, bots и match loop сознательно вынесены в следующие changes и не меняют контракт этого среза.
