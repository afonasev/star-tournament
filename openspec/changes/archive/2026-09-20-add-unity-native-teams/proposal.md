## Why

Внутренний reducer из4d0334f уже умеет командный счёт, но Player запускает только FFA. Подключаем утверждённый командный режим GAME_SPEC §2–5 к текущим2–4 human seats на фиксированной арене с trooper v2.

## What Changes

- Native setup: FFA/teams, явные assignments каждого выбранного места, обе непустые команды, выбранная ориентация существующей контрастной пары цветов.
- Союзники блокируют дробь без урона и scoring (ответ пользователя2026-09-20); all-live occupancy сохраняется.
- Team-aware initial placement/respawn, grouped live/results с суммами и team winner, сохранение состава/цветов/профиля в Repeat.
- Боты, solo,8 scene participants, генератор, сеть, replay и новые ассеты не входят в этот human-only срез. Physical/TV/performance acceptance не объявляется пройденной.

## Capabilities

### New Capabilities
- `unity-native-teams`: native командный матч2–4 local seats.

### Modified Capabilities
Нет.

## Impact

ProvingGround/setup, NativeCombatSession, SafeSpawnSelector, NativeStandingsView, именованный team profile и tests/Player diagnostic. Без новых внешних пакетов. Следующий отдельный commit поверх4d0334f, без main/archive/deploy.
