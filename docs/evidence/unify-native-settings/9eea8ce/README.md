# Приёмка F1: немедленное переключение разделов

macOS Player source `9eea8ce6df4459f58ad33f959d55f97915586f10`: `/Users/eaafonasev/Projects/star-tournament/.local/releases/unify-native-settings/9eea8ce/StarTournamentProvingGround.app`. Executable SHA-256 `57573f5326e17d76a97b9fc9d975c6aad4f79c770beee396c92d0ff0480b4921`. Только локальный кандидат. Предыдущий Player 93fda15 сохранён для сравнения feedback; текущая приёмка относится к 9eea8ce.

20 автоматических проверок прошли; build и muted native Player input/visual smoke 1/4 viewport завершились успешно. Synthetic input не заменяет физический геймпад и human acceptance.

1. Главное меню → Настройки. D-pad/стиком переходить по разделам: параметры появляются сразу без A; фокус остаётся слева.
2. Вправо или A перейти к параметрам. B вернуться к разделу; перейти к другому и убедиться, что параметры сразу сменились.
3. Повторить в профиле, личной паузе и all-AI операторских настройках. В нескольких viewport проверить, что переключение одного игрока сохраняет раздел и фокус соседей.
4. Справка/устройства, сохранение личных значений и общий режим экрана сохраняют прежний контракт.

Screenshots/state/logs: `/Users/eaafonasev/Projects/star-tournament/docs/evidence/unify-native-settings/9eea8ce`. Изменённые состояния: `one/00-menu-auto-control.png`, `one/04-match-auto-control-1.png`, `four/04-match-auto-control-4.png`. Полный сценарий исходного объединения: соседняя папка `93fda15/README.md`; приёмка всего change остаётся pending.

```sh
UNITY_PLAYER_APP='/Users/eaafonasev/Projects/star-tournament/.local/releases/unify-native-settings/9eea8ce/StarTournamentProvingGround.app' /Users/eaafonasev/Projects/star-tournament/unity/tools.sh run
```
