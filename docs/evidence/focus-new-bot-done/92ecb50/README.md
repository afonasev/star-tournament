# Приёмка: быстрое добавление ботов

Source `92ecb5017cb60deb5b17527c615ce15fae91ed21`. macOS Player `/Users/eaafonasev/Projects/star-tournament/.local/releases/focus-new-bot-done/92ecb50/StarTournamentProvingGround.app`. Executable SHA-256 `2558428e8fd4ffeec060bea153c4c742ed44c35c449e6848c30258bac156a39e`. Только локальный кандидат.

14 автоматических UI checks, build и muted native Player smoke прошли. Synthetic input не заменяет физический геймпад/human acceptance.

1. Геймпадом открыть подготовку матча → Игроки → «+ Бот». Фокус сразу на «Готово», сложность «Боец».
2. Нажать A: диалог закрывается, бот сохраняется со средней сложностью, фокус возвращается к карточке. Повторить добавление нескольких ботов.
3. Открыть существующую карточку: фокус на сложности; изменение сложности и прочих параметров остаётся доступным.

Evidence: `/Users/eaafonasev/Projects/star-tournament/docs/evidence/focus-new-bot-done/92ecb50`. Human acceptance pending.

```sh
UNITY_PLAYER_APP='/Users/eaafonasev/Projects/star-tournament/.local/releases/focus-new-bot-done/92ecb50/StarTournamentProvingGround.app' /Users/eaafonasev/Projects/star-tournament/unity/tools.sh run
```
