> Статус: CLOSED / CANCELLED — 2026-09-19. По решению пользователя оставшаяся browser-реализация и приёмка сняты из-за перехода на Unity. Исторические checkbox сохранены (6/9); 3.1–3.3 не пройдены. Delta specs не синхронизируются в основные specs.

## Why

Канонический сценарий Star Tournament — локальная игра друзей на одном экране, но текущий browser slice поддерживает только один keyboard/mouse viewport и не принимает геймпады. Change делает утверждённый сценарий 1–4 local seats запускаемым и даёт каждому человеку явную, не конфликтующую привязку к физическому устройству.

## What Changes

- Добавить setup local roster из 1–4 human seats и до общего лимита восьми участников с ботами.
- Назначать каждому local seat либо единственную keyboard/mouse пару, либо уникальный конкретный подключённый gamepad; показывать доступность и конфликт привязок до старта.
- Добавить standard gamepad action map: левый/правый stick — движение/обзор, `RT` — огонь, `A` — прыжок, удерживание `View` — live-таблица, `Menu/Start` — пауза; `LB` остаётся резервом.
- Выводить один, два, три либо четыре local viewport с независимыми camera/HUD и общим deterministic match tick.
- Для трёх local seats выводить три равных игровых viewport в сетке 2×2 и постоянный live-счёт в свободной ячейке.
- При disconnect назначенного gamepad и потере browser focus приостанавливать общий local match с понятным восстановлением или переназначением устройства.
- **BREAKING**: existing single-seat runtime/input/UI contracts расширяются до per-seat contracts; сохранённые конфигурации прежней схемы должны получить явный compatibility path либо отклоняться с объяснением.

## Capabilities

### New Capabilities
- `local-split-screen-controls`: Локальные seats, конкретные device bindings, standard gamepad action map, reconnect и 1–4 viewport layout.

### Modified Capabilities
- `first-person-player-control`: Единственный keyboard/mouse seat и исключение gamepad заменяются устройственно-независимыми action frames нескольких local seats.
- `match-session-lifecycle`: Конфигурация и lifecycle матча принимают 1–4 local seats наряду с ботами.
- `match-loop-ui`: Setup, HUD, scoreboard и pause flow поддерживают несколько local viewport и их устройства.

## Impact

Затрагиваются `docs/GAME_SPEC.md` §§2, 4, 5, 7, 8 и 10, `src/input`, `src/match`, `src/simulation`, browser runtime, Three.js renderer и React DOM UI. Внешние сервисы и сетевая модель не входят в scope. Нужны автоматические проверки deterministic action frames/configuration и muted in-app Browser playtest для keyboard/mouse, каждого назначенного gamepad, pause/reconnect и раскладок 1–4 игроков; физическая проверка реальными контроллерами остаётся обязательной.
