## Why

После layouts и trooper v2 этапу 2 нужна командная основа gameplay. Утверждённые GAME_SPEC §2, §5, §7 и match-session-lifecycle уже определяют два состава и победу по сумме очков; переносим их отдельным внутренним срезом.

## What Changes

- Валидируемый immutable roster FFA/Team A/Team B для 2–8 participant slots, независимо от local seats.
- Командные суммы и отдельная team winner identity; atomic completion/overtime сохраняют правила FFA.
- Независимые сериализуемые DTO состава, личного и командного счёта.
- Native Player остаётся FFA для 2–4 людей; teams setup/HUD/colors/spawn/shot filtering, боты, solo, восемь native bodies и replay не входят в срез. Новых продуктовых решений этот data-only перенос не требует.

## Capabilities

### New Capabilities
- `unity-team-match-state`: командный reducer состояния матча.

### Modified Capabilities
Нет.

## Impact

Только Core roster/match reducer и focused tests; существующие runtime вызовы конструктора сохраняются. Без новых пакетов, ассетов и tuning. Проверки EditMode/PlayMode, Mac build и muted native FFA regression. Физическая и целевая performance-приёмка остаются открыты.
