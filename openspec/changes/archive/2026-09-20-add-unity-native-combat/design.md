## Context

См. proposal.md и GAME_SPEC §3–5, §8. Domain CombatLife уже проверен; Player использует только shot probe. Read-only Astra review подтвердил несовместимость edge/held ввода и отсутствие scene ownership.

## Goals / Non-Goals

**Goals:** подключённая native петля на четырёх seats, тестируемые scene queries и безопасные переходы жизни.
**Non-Goals:** см. proposal; JSON state не является replay/restore API, diagnostic input не является физической приёмкой.

## Decisions

- NativeCombatSession координирует core, motor и resolver: timers → движение живых → queries/damage в стабильном seat порядке → readiness/respawn. CombatLife остаётся владельцем lifecycle, CharacterMotor единственным владельцем gameplay Transform. Отдельный presenter читает результат. Встраивание всего в ProvingGround отвергнуто из-за смешения ответственности.
- ShotgunResolver использует аналитические yaw-local head sphere, torso box и limb capsules, scene-local PhysicsScene world rays и ближайшую цель каждой дробины; wall wins ties. Дробины одного shot суммируются до damage, что исключает потерю overkill/повторные смерти. Native profile отдельно от неизменённого unity-combat-state-v1. Все dimensions и tuning входят в общий registry. Collider на animated bones отвергнут как зависимость gameplay от GLB.
- Input сохраняет короткий Fire edge плюс FireHeld через fixed ticks. Adapter передаёт held-or-latched-tap core; lifecycle boundaries блокируют физически удержанный trigger до release. Pause/focus/reconnect останавливают session clock, включая corpse/killcam. Device assignment и четырёхместный start сохраняются.
- SpawnSelector использует authored regions/slots обоих этажей, support ray, floor-constrained NavMesh sample, capsule overlap с movement mask и актуальными участниками. Рейтинг: скрытый от всех живых противников → наибольший минимальный complete NavMesh path → stable slot order. Доступный открытый slot применяется немедленно; отсутствие любого валидного slot — явная invariant error, не небезопасный teleport. Стабильный последовательный respawn резервирует occupancy включённой капсулой.
- Killcam хранит death anchor и killer participant/life. После смены жизни убийцы остаётся последняя точка старой жизни. Без killer — медленный orbit с world clipping. Body — отдельная timed non-colliding GLB presentation, видимая всем seats; полноценная анимация/эффекты развиваются на этапе 4. HUD health снизу слева, shotgun/ammo снизу справа, сообщение смерти по центру; roster доступен без объявления scoring готовым.
- Все roots создаются в scene владельца и parent session root до добавления компонентов; unload освобождает cameras/UI/listener/corpses/NavMesh/materials. Глобальный timestep восстанавливается при выходе. QA audio остаётся muted.
- Проверить реальные adapter paths, local PhysicsScene, repeated additive load/unload; Player diagnostic сценарий использует тот же adapter и явно подписан. Четыре физических устройства/TV и reference performance остаются открытыми.

## Risks / Trade-offs

- NavMesh.SamplePosition не проверяет препятствия/этаж → отдельные support/clearance/floor checks. https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AI.NavMesh.SamplePosition.html
- Physics по глобальной сцене ломает isolated fixtures → queries по PhysicsScene владельца.
- Удержание при resume/respawn может дать ложный edge → release gate и input tests.
- Ограниченный native срез не полный матч → handoff сохраняет scoring/results/teams/bots/layouts следующими.

## Migration Plan

Изолированная ветка codex/unity-native-combat поверх трёх cherry-picks, main не меняется. Build/evidence/test и отдельный commit; интеграция и архив только по существующим acceptance условиям, browser deploy не используется.

## Open Questions

Reference PC/TV, quality/internal scale, physical controllers и длительная performance acceptance остаются открытыми и не блокируют реализацию этого согласованного среза.
