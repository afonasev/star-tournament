## Why

После native human teams нужен честный источник знаний для будущего planner. Внутренний слой восприятия позволяет отдельно проверить отсутствие скрытой информации до подключения roster, AI-действий и интерфейса ботов.

## What Changes

- Нативные FOV/PhysicsScene LOS-наблюдения, ограниченная память и отложенные сообщения союзников с исходным временем наблюдения.
- Независимые сериализуемые DTO знаний и очереди сообщений; очистка собственной памяти при смене жизни.
- Именованный профиль восприятия трёх сложностей с общими metadata для Inspector и проверки диапазонов.
- EditMode/PlayMode, muted native diagnostic в FHD/4K и bounded performance evidence.
- Игровой setup остаётся human-only. Planner, навигация, стрельба AI, playable solo, восемь тел и полный replay не входят в срез. Новых продуктовых развилок нет: реализуется часть GAME_SPEC §2, §6–8 и существующего bot-ai contract.

## Capabilities

### New Capabilities
- `unity-bot-perception`: ограниченные знания бота и Unity LOS adapter.

### Modified Capabilities
Нет; historical browser bot-ai остаётся эталоном требований.

## Impact

Unity Core, новый scene adapter и development-only review; существующие human gameplay, assets и UI не меняют поведение. База 4521a7d. Без новых пакетов, transport, main integration, archive и deploy. Физическая и полная performance-приёмка остаются открытыми.
