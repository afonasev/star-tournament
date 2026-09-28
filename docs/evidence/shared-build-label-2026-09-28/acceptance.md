# Одна общая надпись о билде

Небольшая явная UI-правка по запросу пользователя от 2026-09-28: надпись о билде слева сверху, одна для любого числа игроков. Inline path без отдельного нового OpenSpec change.

В Development Player один `Development Build` принадлежит общему ScreenSpaceOverlay canvas. Native Unity watermark отключён через `UnityEngine.Rendering.Watermark.showDeveloperWatermark`; BuildOptions.Development сохранён. Текст и отступ используют существующий `ui.fontSize`, размер прямоугольника вычисляется по preferred text dimensions. Перехват кликов отключён.

Ресурсы: owner `01a0e6a3-7039-72a1-bd53-b00aee60aea3`, baseline `57f68c7726daa2581427451c2dc8e2ac48047c7e`, branch `codex/shared-build-label`, worktree `/Users/eaafonasev/.codex/worktrees/shared-build-label/star-tournament`. Canonical requirement: planning revision `b41ced9`, `unity-native-hud`.

Документация API проверена для Unity 6000.3 и в XML установленного Editor 6000.3.23f1: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rendering.Watermark-showDeveloperWatermark.html

## Человеческая проверка

1. В меню надпись находится в левом верхнем углу экрана.
2. В матче с одним игроком и с четырьмя local viewport видна ровно одна надпись, в одном общем углу.
3. При открытии паузы она остаётся на том же месте.
4. Возле боезапаса справа снизу и по отдельным viewport повторов нет.

Human acceptance ожидается; synthetic Player checks не заменяют решение пользователя.

## Проверки

- Tested implementation: `d4c38d295146099da8f9da489b04456fbcca2b45`.
- `unity/tools.sh test-edit`: 121/121 passed.
- `unity/tools.sh test-play`: 116/116 passed.
- `unity/tools.sh build`: exit 0, `STAR_PROVING_BUILD_PASS 469877700`, Unity 6000.3.23f1, BuildOptions.Development. Editor commands использовали существующий licensing IPC `LicenseClient-eaafonasev-6000.3.23` через локальную UNITY_EDITOR-обёртку.
- Native muted Player `-pauseReview … -pauseReviewCount 1` и `4`: оба complete.txt PASS. Во всех семи снимках buildLabels=1, unityDeveloperWatermark=false; top-left anchors проверены. PNG меню, матча и паузы для 1/4 viewport просмотрены.
- Локальный кандидат: `/Users/eaafonasev/Projects/star-tournament/.local/player-candidates/shared-build-label/d4c38d2/StarTournamentProvingGround.app`.
- Human acceptance pending; synthetic gamepads не являются физической приёмкой управления.
