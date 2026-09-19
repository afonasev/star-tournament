## Why

Native scene пока связывает индекс участника с камерой и устройством, хотя утверждённый GAME_SPEC §2,5,7 требует 1–4 local seats и до восьми участников. Эта связность блокирует solo viewport, ботов без фиктивных устройств и корректный lifecycle общего состава.

## What Changes

- Отделить immutable participant roster и local-seat mapping от устройств, камер и HUD; сохранить одинаковые combat/life правила для всех участников.
- Создавать 2–8 физических участников при 1–4 локальных видах, поддержать fullscreen layout одного места и стабильные participant identities независимо от номера seat.
- Перевести presentation/killcam/standings/action routing на явное соответствие seat→participant; смерть нелокального участника не создаёт дополнительный viewport.
- Сохранять exact roster/mapping/profiles при Repeat и освобождать весь session lifecycle при выходе в setup.
- Проверить mixed mapping/восемь тел opt-in native development journey, ясно отличая fixtures от playable bots. Обычный human-only setup остаётся работоспособным.

Non-goals этого коммита: shipping bot planner/weapon/support/difficulty/evaluation, generated arenas, полная replay-схема, physical/TV acceptance и target-hardware performance. Они остаются в матрице полной миграции. Новых продуктовых развилок для этого внутреннего prerequisite нет; network/distribution/reference hardware остаются открытыми отдельно.

## Capabilities

### New Capabilities

- `unity-participant-roster`: native participant/local-seat separation, routing, presentation ownership и lifecycle для 1–4 local views и 2–8 участников.

### Modified Capabilities

Нет изменения исторических browser contracts.

## Impact

Native roster/session, ProvingGround orchestration, input adapter, local layout, CombatPresentation и standings; EditMode/PlayMode/native Player review. База — navigation commit `45e7f27`; код browser и main checkout не меняются. Реализация следует read-only Astra decision memo до изменения контрактов нескольких модулей.
