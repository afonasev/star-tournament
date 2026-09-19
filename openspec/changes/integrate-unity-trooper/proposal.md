## Why

Пользователь разрешил использовать trooper v2 и его руки/анимации в следующем native Unity срезе. Сейчас Player показывает прежнего rigid робота и оружие без этих рук. Основание: GAME_SPEC §2, §3, §7–9.

## What Changes

- Восстановить существующий v2 pipeline из исходного GLB и сохранить устойчивую копию вне disposable worktree.
- Подключить skinned body, семь клипов, first-person руки и проверяемый хват существующего shotgun.
- Подготовить shipping derivative с attribution, общими ресурсами и профильными настройками.
- Проверить lifecycle, pause/Repeat, 2/3/4 seats, muted Mac Player, EditMode/PlayMode и сборку.
- Не включать teams/bots/solo/восемь участников, сеть и browser deploy. Художественная, физическая и target-hardware performance приёмка остаются открытыми.

## Capabilities

### New Capabilities
- `unity-trooper-presentation`: native skinned body и first-person руки от существующего v2, presentation-only анимация и проверяемая поставка.

### Modified Capabilities
Нет.

## Impact

Unity Art/manifest, Editor preparation, native presentation и профиль, tests/review fixture; scripts/trooper и evidence. Исходный ART_LOLL GLB неизменен. Новые пакеты и покупки не предполагаются. Существенных открытых продуктовых развилок внутри разрешённого среза нет; общий hardware/TV/quality и художественная приёмка остаются открытыми.
