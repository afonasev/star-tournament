# 18 — Unity: подключённая боевая петля

Статус: реализация и native diagnostic проверены; отдельный commit. Physical/performance gates Stage 0 остаются открытыми. Change `add-unity-native-combat`, ветка `codex/unity-native-combat`.

## Контур

Продолжение этапа 2 по GAME_SPEC §3–5: четыре local seats на существующей фиксированной арене; shotgun pellets/head-torso-limb/occlusion, damage/death, отключение dead capsule, health/ammo HUD, life-scoped killcam, non-colliding timed corpse и current-state safe respawn. Scoring/results/repeat, teams/bots и 1–4 layouts — следующие отдельные срезы.

Исходные `6e73eeb`, `f283e99`, `beef8b9` отсутствовали в worktree и не имели patch-equivalent commits. Перенесены поверх main `26ce3ff` как `4f65038`, `12316f3`, `a3a95e9`. Старый worktree `/Users/eaafonasev/.codex/worktrees/3c73/star-tournament` и main не редактировались.

## Архитектура

- `NativeCombatSession`: timers → живые motors → shots/damage в stable seat order → ready respawn. `CombatLife` владеет lifecycle, motor — gameplay Transform; renderer не участвует в damage.
- `ShotgunResolver`: аналитические yaw-local volumes и ближайшая world/target граница каждой дробины. Урон суммируется по целям до life-scoped damage; wall wins ties. Body и movement-only geometry не блокируют дробь.
- `SafeSpawnSelector`: slots обоих этажей; support, slope, floor-local NavMesh, полная capsule clearance и текущая occupancy; hidden first, затем минимальная complete navigation distance до противников. Последовательное включение capsule резервирует respawn slot. Нет валидного slot — явная invariant error, никакого небезопасного fallback.
- `CombatPresentation`: health/ammo и killcam читают lifecycle. Точка камеры фиксирована у смерти; tracking прекращается на смерти/смене killer life. No-killer orbit ограничен world geometry. Тело отдельно от новой жизни, без colliders, проецируется на физическую опору и удаляется по session clock. Полная анимация и эффекты остаются этапу 4.
- Все runtime objects принадлежат scene/root полигона. Unload очищает EventSystem/AudioListener/cameras/body/NavMesh/materials. Pause/focus/reconnect очищают ввод и останавливают gameplay/corpse/death time. `FireHeld` сохраняется между fixed ticks, `Fire` сохраняет короткий tap; resume/respawn не создают автоогонь.

Профили: `unity-proving-ground-v1@1`, `unity-combat-state-v1@1`, новый `unity-native-combat-v1@1`. Editor Inspector и диапазонная validation используют одни descriptors, включая integer pellet count. Настройки боя копируются в session при запуске.

## Проверки

EditMode 17/17; PlayMode 17/17. Проверены mixed hit через session, wall/movement-only occlusion в isolated PhysicsScene, смерть/capsule, full-life respawn, batch occupancy, hidden upper-floor navigation, stale hit, pause clock, первое нажатие после respawn, airborne corpse support, killcam tracking и два additive reload.

Mac Development Player build PASS. Full HD и 4K diagnostic выстрел → 30 HP → death → killcam → pause с неизменным session clock → respawn full life подтверждены в Player. Отдельный world-damage fixture проверил no-killer orbit. Обычные keyboard join/live/pause/resume проверены через native UI. [Evidence, screenshots, hashes и ограничения](../../evidence/unity-native-combat-2026-09-19/README.md).

## Следующий срез и открытые gates

Scoring/match completion/results/repeat отдельным change и commit, затем teams/bots/layouts по roadmap. Stage 0 остаётся неархивированным: physical четыре устройства/TV, reference hardware/quality/internal scale и длительный foreground performance не закрыты. Этот change не выбирает сеть/replay и не выполняет browser deploy.
