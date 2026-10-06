## Why

В текущей игре противники неподвижны и не стреляют. Нужны активные боты с индивидуальной сложностью и расширяемой логикой, а также быстрые воспроизводимые матчи для оценки AI перед обязательной браузерной приёмкой.

## What Changes

- Боты `Салага / Боец / Ветеран`: поиск боя, навигация, strafe, ситуативные прыжки, ограниченное прицеливание, краткие отходы и временная помощь союзникам без постоянного строя.
- Добавление/удаление и отдельная сложность каждого бота до матча; формат имени `Vega · Боец` в live/results, сохранение roster в Repeat.
- Общий multi-participant simulation tick и расширяемые observation/planner/action interfaces; сериализуемые AI memory/timers/RNG, честные наблюдения и общие правила.
- Временное автоматическое пополнение до 20 при нуле, одинаково для человека и AI; значение хранится в weapon profile, cooldown сохраняется.
- Headless simulator с реальной симуляцией/collision, seeded bot-only матчами, отчётами, replay, метриками активности, сложности, прыжков и командного взаимодействия.
- **BREAKING**: versioned configuration/snapshot/replay/profile contracts расширяются bot identity/state; несовместимые старые данные явно отклоняются, без молчаливой миграции.

## Capabilities

### New Capabilities
- `bot-ai`: расширяемое детерминированное поведение и честная сложность.
- `ai-headless-evaluation`: ускоренные реальные матчи, воспроизводимость, статистические и поведенческие критерии.

### Modified Capabilities
- `match-session-lifecycle`: bot roster и общий tick всех участников, bot-only harness configuration.
- `match-loop-ui`: редактор ботов и подпись сложности в таблицах.
- `double-barrel-shotgun-combat`: пополнение при нуле и симметричный combat всех участников.
- `game-design-profile-core`: полные профили трёх сложностей и refill metadata.

## Impact

Затрагиваются GAME_SPEC §§2, 3, 5–8; input action routing, simulation/playableStep/snapshot/replay, combat queries, match configuration/standings, arena navigation projection, profiles, DOM setup/HUD, renderer projection движущихся противников и локальные CLI tests.

Используются существующие deterministic collision и ArenaDefinition, без новых внешних сервисов. Не входят новые оружия/бонусы/карты, обучение моделей, online transport, gamepad/split-screen и полный UI Balance Lab. Числовые поля AI сразу получают общий descriptor contract. Все продуктовые развилки обсуждённого среза закрыты; числовая настройка AI уточняется воспроизводимыми тестами внутри именованного профиля.
