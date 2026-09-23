# Матрица полного переноса Star Tournament на Unity

Уточнение пользователя 2026-09-20: проверенный отдельный срез — `add-unity-bot-controlled-seats` (handoff 29; 81 EditMode / 63 PlayMode, Mac build, 26 focused/muted PNG+JSON FHD/4K), ordinary human/AI на 1–4 views, включая all-AI и один human + 1/2/3 AI, максимум восемь участников. Replay пока отложен; поставка обсуждается отдельно. Финальную пользовательскую приёмку пользователь проведёт после реализации; каждый срез сохраняет tests/build и muted native Player QA. Physical/art/reference-performance acceptance этим не закрываются.


Аудит 2026-09-20, база `6f8ed159d3c09c3a189e2ed365d42b79b1bcf2b7`. Полная цель активна. Статусы ниже — requirement-by-requirement backlog, не процент завершения. `Реализовано` означает найденный native код и committed evidence, но не закрывает перечисленные физические/художественные gates. Исходные handoffs 15–24 прочитаны; результаты 50 EditMode / 36 PlayMode и build из handoff 24 — исторические до нового запуска.

Источники: `docs/GAME_SPEC.md` (GS), `docs/UNITY_MIGRATION_ROADMAP.md` (RM), `docs/AI.md`, `openspec/specs/<имя>/spec.md` (OS). Новое пользовательское поручение полного переноса имеет приоритет над формулировками границ прежних срезов. Browser backlog отменён, а не успешно принят; его реализацию не возобновляем.

## Управление, движение и бой

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| INPUT-1 GS §2,7; first-person-player-control | Human-only input mask, sparse ownership, operator Escape/all-AI Start; automated disconnect/reconnect (handoff29) | Mapping1–4 после отделения participants проверен; physical devices ниже | Реальные K+3 pads / 4 pads, focus, cursor, reconnect/clear held |
| VIEW-1 GS §2,5; RM этап 2 | Human/AI 1–4 views, mixed/all-AI 2/3/4 FHD/4K verified; обычные камеры/руки/HUD и persistent standings (handoff29) | Обычный solo с ботами подключён handoff28; физическая приёмка открыта | Player 1/2/3/4, FHD/4K, readable HUD, отдельный ввод |
| MOVE-1 GS §3; first-person-player-control | CharacterMotor: acceleration, strafe, jump, air control | Контроль темпа/ступеней на generated maps | Physical feel, run/strafe/stairs/headroom без скрытого бонуса |
| MOVE-2 GS §2; multi-level-arena-navigation | Физические stairs/ramp fixture, тесты | Плавное вертикальное camera smoothing с headroom/aim parity | PNG/video + authoritative shot alignment, обе стороны |
| COLL-1 GS §3; collision-query-foundation | Live capsules solid, dead disabled; World/MovementOnly layers | Проверка всей generated geometry | Живой blocker, death/respawn, no capsule overlap |
| COLL-2 GS §2; architectural-panel-collision | Фиксированные window/barrier/slab probes | Canonical стены/окна/barriers/panels/portals, headroom, niches | Movement vs projectile/LOS fixtures; реальная проходимость |
| COMBAT-1 GS §3; double-barrel-shotgun-combat | ShotgunResolver + CombatLife: pellet zones, ammo/refill/cooldown/edge | Подтвердить на final arena/assets/8 participants | Hit zones, mixed damage, occlusion, release latch, parity people/bots |
| COMBAT-2 GS §3; handoff 23 | Союзник блокирует без damage/score, не пропускает дробь | Сохранить при ботах/8 participants | Actual query blocker, downstream enemy untouched |

## Матч и жизнь

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| LIFE-1 GS §4; match-session-lifecycle | Общий death/killcam/respawn; natural viewed AI lifecycle FHD/4K verified (handoff29) | Проверить все состояния solo/bots/8 | Player death, killer respawn, no-killer, Tab during killcam |
| LIFE-2 GS §4; hit-driven-death-presentation | Noncolliding trooper corpse, session-clock animation | Полное corpse lifetime/event evidence и directional response audit | Corpse не меняет shots/LOS/spawn; expiry/pause/Repeat |
| SPAWN-1 GS §4; procedural-arena-generation | SafeSpawnSelector fixed candidates, occupancy/team threat | Regions/multiple slots/nav distance/LOS priority/fairness | Batch ID order, occupied region fallback, immediate best slot |
| MATCH-1 GS §5; match-session-lifecycle | NativeMatchState scoring, assists/chains/time/target/overtime | Сохранить при полном составе | Atomic tick/tie/target precedence, precise damage/rounded UI |
| MATCH-2 GS §2,5; handoffs 22–23 | Ordinary 2–8 participants, 1–4 human/AI views; all-AI 8-participant teams verified (handoff29) | Обычный bots/solo/8 setup подключён handoff28; final-arena acceptance открыта | Native solo/human-bot/team/8 end-to-end matches |
| MATCH-3 GS §5; match-loop-ui | Human/AI, per-view difficulty/team и extra bots; frozen composition/seed/profiles Repeat verified (handoff29) | Bot difficulty/add-remove подключены handoff28; arena size/seed/profile selection открыты | Invalid setup recovery, exact frozen roster/config/arena Repeat |
| UI-1 GS §5; match-loop-ui | Ordinary human/AI setup, HUD/live/pause/resume/Repeat/exit; 26 inspected PNG FHD/4K (handoff29) | Имена/сложности подключены handoff28; final presentation acceptance открыта | FHD/4K each layout, grouped totals/winner, View/Tab hold |
| LIFE-3 GS §5,7; match-session-lifecycle | Human-only disconnect checks и operator pause all-AI; frozen clocks/clean Repeat/menu verified (handoff29) | Подключить будущие generation/Lab lifecycle; сохранить проверенные AI/presentation границы | No advancing gameplay during pause/results; fresh Repeat/reset |

## AI — навигация не равна playable bots

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| AI-1 GS §2,7; bot-ai honest observation | NativeBotPerception/ObservationProvider, FOV/LOS/time memory, delayed direct-only reports | Обычный lifecycle подключён handoff28; final-scene validation открыта | Hidden movement/death/respawn, expiry, no relay, pause/Repeat |
| AI-2 bot-ai active navigation; multi-level-arena-navigation | Fixture routes/support/ordinary actions/bounded recovery (handoff 25) | ARENA-3: generated-definition traversal всех пяти families; ordinary planner integration сохранён | 2 transitions × both directions × 3 lanes/family, overlap-XZ, owned-context, blocker, replan |
| AI-3 GS §2; bot-ai active behavior | Planner verified in automated checks and focused FHD/4K Player pilot (handoff27) | Обычный setup подключён handoff28; generated-arena validation открыта | Real mixed-difficulty matches, contact/idle/stuck/jump cadence |
| AI-4 bot-ai three difficulties | Behavior profile + reaction/aim/decision/personality без stat bonuses; internal Player pilot verified (handoff27) | Independent statistical acceptance | Equal stats contract + independent paired difficulty evaluation |
| AI-5 bot-ai temporary support | Bounded support/retreat, allowlisted human/bot allies; internal Player pilot verified (handoff27) | Native pilot и final-map team metrics | Team support/disband/close-group metrics, FFA isolation |
| AI-6 bot-ai extensibility/state | Planner/policy + validated RNG/timers/intent/native-route DTO (handoff27) | Replay отложен пользователем | Extension fixture, independent snapshots/validated restore |
| AI-7 ai-headless-evaluation | Native runner отсутствует | Same-runtime accelerated bot-only suite, artifacts/telemetry | Paired sides, all sizes/FFA/teams/2–8, confidence intervals, holdout |
| AI-8 GS §5; match-loop-ui | Ordinary human/AI view setup и extra bots, 2–8 participants; per-AI difficulty/team; genuine planners, frozen Repeat (handoff29) | Physical controls/TV acceptance | Ordinary Player human-versus-bot, death/respawn/results/Repeat |

## Арены

R7 (`replace-procedural-arenas-with-combat-bowl-ring`) native candidate `combat-bowl-v1@7`: authored-only 2–8, новое внешнее кольцо и решётка, hidden-first all-live spawning, независимые pickup instances. Старые ARENA rows ниже — историческая реализация, а generator-only gaps отменены новым решением; не возобновлять их. Общие collision/navigation/freeze/validation contracts перенесены на authored fixtures. Точные результаты проверок и human acceptance ведутся в delivery.json change.

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| ARENA-1 GS §2,7; unity-procedural-arena-foundation | Canonical `unity-two-level-fixture-v1` definition из independent seed/version/frozen profile fingerprint; collision/navigation/spawn/presentation derived от неё | ARENA-2 size/families, ARENA-4 fairness validators, ARENA-5 retry/fallback/replay lifecycle | EditMode identity/validation, PlayMode physical projection/allocator, muted 4K Player diagnostic; physical/art/performance acceptance остаются открыты |
| ARENA-2 GS §2; architectural-height-semantics | Five explicit canonical families in `ArenaDefinition` | Architectural/art and comprehensive topology validation | Floor plans/sections/structural matrix/first-person views |
| ARENA-3 GS §2; multi-level-arena-navigation | Fixture declared supports/transitions, two links and 3 lanes | Definition-derived traversal/physical route proof for every ARENA-2 family | 2 transitions × both directions × 3 lanes/family, feet/support/headroom, no XZ shortcut |
| ARENA-4 GS §2,4; procedural-arena-generation | Fixed spawn safety | Complete topology/spawn/LOS/mode fairness validators, all sizes | Seed corpus/property tests + Player readability/playability |
| ARENA-5 GS §7; match-session-lifecycle | Fixed arena Repeat | Exact generated definition/config frozen across Repeat | Auto/manual seed and retry/new seed/fallback journeys |

## Presentation и настройки

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| ART-1 GS §2,3; handoff 21 | ART_LOLL trooper v2, seven clips, shared resources, hands | Художественная приёмка gait/grip/death, final integration | Native close-ups 1–4 seats, attribution/hash, manual visual approval |
| ART-2 participant-weapon-presentation; shotgun-kinetic-presentation | Trooper weapon and basic feedback | Dual burst/smoke/pellet tracers/recoil/material impacts audit/implementation | Visible-muzzle alignment while strafe; unchanged combat |
| ART-3 GS §2; participant-wall-clearance | Не доказано | Forward-only visual retraction at wall/decor envelopes | Side/front wall, near plane, still visible/fire unchanged |
| ART-4 arena-presentation-style; arena-glb-asset-pipeline | Fixture flat primitive materials | Native clean-future-sport kit/manifest/LOD/shared lifecycle | Scale/pivots/proxies/shipping GLB/material/LOD Player audit |
| ART-5 arena-material-coverage; architectural-panel-collision | Нет final architecture | PBR meter UV/texture coverage, no overlap/z-fighting, collision proxies | Surface/portal/wall/camera motion views, collision alignment |
| ART-6 local-light-shadows; GS §2 | Basic fixture lighting | Native fixture lights/shadows/rim budget and quality-dependent detail | Readability, shadows, four-camera CPU/GPU/budget evidence |
| SET-1 graphics-quality-settings; GS §2 | Нет native quality UI | Low/Balanced/High/Ultra + scale/textures/LOD/decals/post | FHD/4K output/HUD unchanged, retained prefs, measured budgets |
| SET-2 GS §5; handoff 16 | Global persistent FPS toggle реализован | Regression only | Setup/pause/restart, unscaled average, no gameplay authority |
| SET-3 GS §5; RM §3 | Audio/fullscreen native completeness не доказана | Audit/settings implementation; audio mix decision отдельно | Native fullscreen/focus + persisted audio, audio-specific check then mute |
| SET-4 GS §2 app-icon-v1 | Требует native packaging audit | Production icon + distinct dev border where supported | Built app icon/identity evidence |

## Профили, запись, поставка и gates

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| LAB-1 GS §6; game-design-profile-core | Separate profiles/metadata/Inspector | Composite immutable GameDesignProfile, identities/hash/catalog | Exact revision freeze, complete descriptors/range validation |
| LAB-2 GS §6; game-design-lab | Нет native runtime Lab | Draft/save/new revision/search/groups/compare/changelog/import/export | Menu-only edit, explicit discard, no paused mutation |
| LAB-3 GS §6 | Нет native catalog workflow | releaseRef/exact published snapshots/protected delete/local history | Atomic save/release, immutable revision, reset selection after delete |
| STATE-1 GS §7 | Partial copied gameplay/perception DTO | Complete serializable gameplay/AI/config state | Round-trip/aliasing/invalid input, ownership audit |
| REPLAY-1 GS §2,7,10 | Не реализован; детальный контракт открыт | Отложен пользователем; будущий state-recorded replay после отдельного решения | Recorded-state playback evidence; browser bitwise hash не требуется |
| NET-1 GS §10 | Correctly absent | Выбор модели остаётся открытым | Не добавлять transport/service автоматически |
| QA-1 GS §8; game-qa | Historical EditMode/PlayMode/build/PNG evidence 15–24 | Every new change verified on own build | Native muted Player + screenshots, full automated regressions |
| QA-2 GS §2,8,10 | Short Mac diagnostics only | Reference hardware/quality/internal scale/method + long active 60 FPS | FHD–4K 4-player sustained frame/CPU/GPU/memory, rare frames |
| QA-3 handoff 23 | FHD worst ~1009 ms не объяснён | Расследовать воспроизводимость/причину | Raw trace + cause/fix evidence; чистый повтор не доказательство |
| QA-4 GS §8; RM этап 6 | Physical/TV acceptance открыта | Real 4 devices/TV/reconnect/focus/HUD/long match | Реальная проверка; synthetic input не заменяет |
| SHIP-1 RM этап 6; GS §10 | Mac dev builds; main `26ce3ff`, verified Unity branches сохранены | Approved platform/distribution decision, gated integration/archive | Verified commits + final audit; не заменять browser release автоматически |

## Порядок продолжения

Navigation → planner/combat/support и participant separation → shipping bot setup/solo/8 → bot-controlled split-screen seats → arenas/validators → presentation/settings → complete Balance Lab → evaluation → full audit/physical/performance gates. Replay отложен, distribution обсуждается отдельно. Порядок может уточняться по зависимостям; ни один пункт не снимается автоматически. Каждый implementation change получает отдельную ветку/worktree, tests/build/Player evidence и commit. Следующие пользовательские sidebar-задачи не создаются без явного запроса.
