# Матрица полного переноса Star Tournament на Unity

Аудит 2026-09-20, база `6f8ed159d3c09c3a189e2ed365d42b79b1bcf2b7`. Полная цель активна. Статусы ниже — requirement-by-requirement backlog, не процент завершения. `Реализовано` означает найденный native код и committed evidence, но не закрывает перечисленные физические/художественные gates. Исходные handoffs 15–24 прочитаны; результаты 50 EditMode / 36 PlayMode и build из handoff 24 — исторические до нового запуска.

Источники: `docs/GAME_SPEC.md` (GS), `docs/UNITY_MIGRATION_ROADMAP.md` (RM), `docs/AI.md`, `openspec/specs/<имя>/spec.md` (OS). Новое пользовательское поручение полного переноса имеет приоритет над формулировками границ прежних срезов. Browser backlog отменён, а не успешно принят; его реализацию не возобновляем.

## Управление, движение и бой

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| INPUT-1 GS §2,7; first-person-player-control | SeatInputCoordinator: keyboard/mouse, unique gamepads, actions | Проверить 1–4 seats после отделения participants | Реальные K+3 pads / 4 pads, focus, cursor, reconnect/clear held |
| VIEW-1 GS §2,5; RM этап 2 | LocalSeatLayout 2/3/4, три viewport + постоянный счёт | Solo fullscreen и связь seat→participant | Player 1/2/3/4, FHD/4K, readable HUD, отдельный ввод |
| MOVE-1 GS §3; first-person-player-control | CharacterMotor: acceleration, strafe, jump, air control | Контроль темпа/ступеней на generated maps | Physical feel, run/strafe/stairs/headroom без скрытого бонуса |
| MOVE-2 GS §2; multi-level-arena-navigation | Физические stairs/ramp fixture, тесты | Плавное вертикальное camera smoothing с headroom/aim parity | PNG/video + authoritative shot alignment, обе стороны |
| COLL-1 GS §3; collision-query-foundation | Live capsules solid, dead disabled; World/MovementOnly layers | Проверка всей generated geometry | Живой blocker, death/respawn, no capsule overlap |
| COLL-2 GS §2; architectural-panel-collision | Фиксированные window/barrier/slab probes | Canonical стены/окна/barriers/panels/portals, headroom, niches | Movement vs projectile/LOS fixtures; реальная проходимость |
| COMBAT-1 GS §3; double-barrel-shotgun-combat | ShotgunResolver + CombatLife: pellet zones, ammo/refill/cooldown/edge | Подтвердить на final arena/assets/8 participants | Hit zones, mixed damage, occlusion, release latch, parity people/bots |
| COMBAT-2 GS §3; handoff 23 | Союзник блокирует без damage/score, не пропускает дробь | Сохранить при ботах/8 participants | Actual query blocker, downstream enemy untouched |

## Матч и жизнь

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| LIFE-1 GS §4; match-session-lifecycle | Death/killer life, killcam/no-killer orbit, respawn | Проверить все состояния solo/bots/8 | Player death, killer respawn, no-killer, Tab during killcam |
| LIFE-2 GS §4; hit-driven-death-presentation | Noncolliding trooper corpse, session-clock animation | Полное corpse lifetime/event evidence и directional response audit | Corpse не меняет shots/LOS/spawn; expiry/pause/Repeat |
| SPAWN-1 GS §4; procedural-arena-generation | SafeSpawnSelector fixed candidates, occupancy/team threat | Regions/multiple slots/nav distance/LOS priority/fairness | Batch ID order, occupied region fallback, immediate best slot |
| MATCH-1 GS §5; match-session-lifecycle | NativeMatchState scoring, assists/chains/time/target/overtime | Сохранить при полном составе | Atomic tick/tie/target precedence, precise damage/rounded UI |
| MATCH-2 GS §2,5; handoffs 22–23 | Reducer 2–8; сцена 2–4 humans, FFA/teams | Participant/local-seat separation, bots, solo, 8 тел | Native solo/human-bot/team/8 end-to-end matches |
| MATCH-3 GS §5; match-loop-ui | Teams/цвета/duration/target setup; frozen Repeat | Bot difficulty/add-remove, arena size/seed/profile selection | Invalid setup recovery, exact frozen roster/config/arena Repeat |
| UI-1 GS §5; match-loop-ui | HUD/live/results/pause/settings/menu | Полные bot labels и human highlighting audit | FHD/4K each layout, grouped totals/winner, View/Tab hold |
| LIFE-3 GS §5,7; match-session-lifecycle | Pause/focus/disconnect + clean Repeat/menu | Подключить AI/generation/Lab/presentation lifecycle | No advancing gameplay during pause/results; fresh Repeat/reset |

## AI — навигация не равна playable bots

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| AI-1 GS §2,7; bot-ai honest observation | NativeBotPerception/ObservationProvider, FOV/LOS/time memory, delayed direct-only reports | Shipping sensor lifecycle | Hidden movement/death/respawn, expiry, no relay, pause/Repeat |
| AI-2 bot-ai active navigation; multi-level-arena-navigation | Verified native routes/support/ordinary actions/bounded recovery (handoff 25) | Shipping planner integration и generated-scene routing | Stairs/ramp both directions × 3 lanes, overlap-XZ, blocker, replan |
| AI-3 GS §2; bot-ai active behavior | Нет planner | Search, combat approach/strafe, bounded retreat, situational jumps | Real mixed-difficulty matches, contact/idle/stuck/jump cadence |
| AI-4 bot-ai three difficulties | Только FOV/memory различия | Reaction/aim/decisions/personality без stat/hidden-info bonuses | Equal stats contract + independent paired difficulty evaluation |
| AI-5 bot-ai temporary support | Только reports | Temporary human/bot support, alternate angles, personal space | Team support/disband/close-group metrics, FFA isolation |
| AI-6 bot-ai extensibility/state | Perception + validated navigation/goal/transition snapshots | Full planner/weapon policy, RNG/intention state | Extension fixture, independent snapshots/validated restore |
| AI-7 ai-headless-evaluation | Native runner отсутствует | Same-runtime accelerated bot-only suite, artifacts/telemetry | Paired sides, all sizes/FFA/teams/2–8, confidence intervals, holdout |
| AI-8 GS §5; match-loop-ui | Human-only setup | Add/remove/difficulty labels/frozen roster | Ordinary Player human-versus-bot, death/respawn/results/Repeat |

## Арены

| ID / source requirement | Текущее native состояние | Оставшаяся реализация | Необходимое доказательство |
| --- | --- | --- | --- |
| ARENA-1 GS §2,7; procedural-arena-generation | Одна fixed proving arena | Seed/size/version/profile canonical definition + independent generation | Same identity→same definition, bounded failure and explicit fallback |
| ARENA-2 GS §2; architectural-height-semantics | Два уровня fixture | Five spatial families, closed floors predominance, basement/rooms/tunnels | Floor plans/sections/structural seed matrix/first-person views |
| ARENA-3 GS §2; multi-level-arena-navigation | Verified fixture declared supports/transitions, two links and 3 lanes | Generated-map links/width/connectivity | Both directions/lanes, real headroom/feet support, no XZ shortcut |
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
| REPLAY-1 GS §2,7,10 | Не реализован; детальный контракт открыт | State-recorded replay после выбора cadence/events/seek/restore | Recorded-state playback evidence; browser bitwise hash не требуется |
| NET-1 GS §10 | Correctly absent | Выбор модели остаётся открытым | Не добавлять transport/service автоматически |
| QA-1 GS §8; game-qa | Historical EditMode/PlayMode/build/PNG evidence 15–24 | Every new change verified on own build | Native muted Player + screenshots, full automated regressions |
| QA-2 GS §2,8,10 | Short Mac diagnostics only | Reference hardware/quality/internal scale/method + long active 60 FPS | FHD–4K 4-player sustained frame/CPU/GPU/memory, rare frames |
| QA-3 handoff 23 | FHD worst ~1009 ms не объяснён | Расследовать воспроизводимость/причину | Raw trace + cause/fix evidence; чистый повтор не доказательство |
| QA-4 GS §8; RM этап 6 | Physical/TV acceptance открыта | Real 4 devices/TV/reconnect/focus/HUD/long match | Реальная проверка; synthetic input не заменяет |
| SHIP-1 RM этап 6; GS §10 | Mac dev builds; main `26ce3ff`, verified Unity branches сохранены | Approved platform/distribution decision, gated integration/archive | Verified commits + final audit; не заменять browser release автоматически |

## Порядок продолжения

Navigation → planner/combat/support и participant separation → shipping bot setup/solo/8 → arenas/validators → presentation/settings → complete Balance Lab → evaluation/recorded replay после необходимых решений → full audit/physical/performance/distribution gates. Порядок может уточняться по зависимостям; ни один пункт не снимается автоматически. Каждый implementation change получает отдельную ветку/worktree, tests/build/Player evidence и commit. Следующие пользовательские sidebar-задачи не создаются без явного запроса.
