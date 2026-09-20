## Why

CombatLife существует отдельно от Player: выстрелы полигона пока не ранят, не запускают killcam и не возрождают участников. Этот срез подключает утверждённую боевую петлю GAME_SPEC §3–5 к четырём local seats на существующей арене.

## What Changes

- Shotgun pellet resolver с аналитическими head/torso/limb volumes и static-world occlusion, damage/death и отключением dead capsule.
- Per-seat health/ammo HUD, killcam конкретной жизни убийцы либо orbit при отсутствии убийцы, безопасный respawn по текущим occupancy/LOS/navigation.
- Scene-owned session lifecycle, очистка ввода и остановка gameplay на pause/focus/reconnect; тест повторной загрузки без дублей.
- Именованный профиль и metadata всех новых числовых параметров; тесты, Mac build, muted native evidence и отдельный commit.

## Capabilities

### New Capabilities
- `unity-native-combat`: подключённый бой и жизненный цикл native Player.

### Modified Capabilities

Нет.

## Impact

Unity runtime/core/input/motor/cameras/HUD, тесты и handoff. Зависит от Stage 0, FPS и CombatLife commits, перенесённых поверх актуального main. Scoring/results/repeat, teams/bots, остальные layouts, полный визуальный animation/effects pipeline, replay и сеть остаются последующими срезами roadmap. Покупки не нужны. Reference hardware/quality и физическая приёмка четырьмя устройствами/TV остаются открытыми; этот change их не закрывает.
