## Why

Один пользователь должен проверять обычное отображение на 2/3/4 экранах без дополнительных устройств. Сейчас view требует LocalHuman, поэтому настоящего бота нельзя назначить отображаемому месту.

## What Changes

- Выбор human/AI и сложности AI на каждом отображаемом месте обычного setup; один human с AI views либо all-AI.
- Разделение view ownership и источника действий; устройства нужны только людям, меню доступно оператору независимо от состава.
- Существующие команды, combat/lifecycle, frozen Repeat и общий TrooperVisual сохраняются.
- GAME_SPEC §2,3,5,11: replay отложен, поставка отдельно, финальная пользовательская приёмка после реализации; автоматические tests/build/muted Player QA остаются обязательными.

## Capabilities

### New Capabilities
- `unity-bot-controlled-seats`: AI-controlled отображаемые места и независимый операторский ввод.

### Modified Capabilities

Нет: предыдущие native changes ещё не архивированы; новый контракт расширяет их поведение.

## Impact

NativeBotSetup, NativeMatchComposition, SeatInputCoordinator и ProvingGround; tests и focused native review. Зависимость — f29c449, существующий bot planner и TrooperVisual. Без replay, новых ассетов, сети, archive, main integration и deploy. Новых продуктовых развилок нет; physical/art/reference-performance gates открыты.
