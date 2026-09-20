## Why

Подключённый native бой пока не завершает матч и не показывает счёт. Следующий согласованный срез GAME_SPEC §3–5 превращает полигон в повторяемый четырёхместный FFA матч.

## What Changes

- Native статистика урона, assists и kill chains по существующему match-session-lifecycle, без новой формулы.
- Настройки длительности/опциональной цели из именованного профиля; таймер, atomic end-of-tick completion и overtime.
- Live standings по Tab/View, включая killcam; результаты, Repeat и выход в setup.
- Fresh combat/presentation/input lifecycle при сохранении assignments и конфигурации Repeat.
- EditMode/PlayMode, muted Mac Player evidence Full HD/4K, handoff 19 и отдельный commit.

## Capabilities

### New Capabilities
- `unity-native-match-loop`: подключённый четырёхместный FFA match lifecycle.

### Modified Capabilities

Нет.

## Impact

Unity core/session/input/UI/presentation, tests и docs. База e735de7 перенесена fast-forward поверх main 26ce3ff без редактирования других worktrees. Teams/bots, прочие layouts, assets, сеть/replay и browser deploy не входят. Stage 0 physical/performance gates и выбор reference hardware/quality остаются открытыми.
