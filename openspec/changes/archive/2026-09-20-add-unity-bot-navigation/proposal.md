## Why

Unity уже хранит честные знания бота, но не умеет превращать допустимую цель в физическое движение. Следующий связный срез переносит навигацию через существующие NavMesh и CharacterMotor, чтобы будущий planner использовал проверенный исполнитель обычных действий.

## What Changes

- Добавить исполнитель маршрутов к статическим либо наблюдаемым/запомненным целям без доступа к скрытому состоянию противников.
- Проверить реальное движение вокруг препятствий, лестницы и рампу в обе стороны, overlapping floors, занятые проходы и ограниченный collision-preserving recovery.
- Сохранить сериализуемое намерение/прогресс отдельно от native NavMesh handles и параметризовать новый tuning через metadata профиля.
- Включить компонент в native development Player journey и сохранить измерения/скриншоты с явной границей diagnostic evidence.
- Вести матрицу полного переноса: этот срез не завершает bots или этап 2. Playable roster, planner/combat/support, solo/восемь участников и остальные механики следуют отдельными changes.

## Capabilities

### New Capabilities

- `unity-bot-navigation`: физическое следование маршруту ordinary actions, честные goal inputs, ограниченное восстановление и проверяемое состояние.

### Modified Capabilities

Нет. Утверждённое поведение `bot-ai` и `multi-level-arena-navigation` переносится в native adapter без изменения игровых правил.

## Impact

GAME_SPEC §§2–3, 7–8; AI Navigation 2.0.14, CharacterMotor, новые AI navigation/profile contracts, EditMode/PlayMode и native review. База `6f8ed15`, отдельная ветка `codex/unity-bot-navigation`.

Новые продуктовые решения этому срезу не нужны. Network model, детальный Unity replay, reference hardware/quality и native distribution остаются открытыми. Покупки, browser runtime, телепортация, stat bonuses, полный AI planner и physical acceptance в scope не входят. Реализация следует read-only Astra decision memo.
