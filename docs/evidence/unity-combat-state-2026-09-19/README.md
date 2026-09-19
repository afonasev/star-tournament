# Unity combat-state regression — 2026-09-19

15/15 EditMode, 9/9 PlayMode, Mac Development Player build PASS. Unity 6000.3.23f1; screenshot profile `unity-proving-ground-v1` version 1. Domain profile `unity-combat-state-v1` version 1 не подключён к сцене.

Muted native setup → keyboard/mouse join → diagnostic live → pause/resume проверены; FPS сохранён после перезапуска. `setup.png`, `live.png`, `pause.png` — регрессия существующего UI, не демонстрация боевого адаптера. Ошибок Exception/Error в Player.log не найдено. Counts/hashes/build bytes в `summary.json`. Неизменяемая локальная копия XML и логов: `.local/unity-evidence/combat-state-final/`; верхний каталог `.local/unity-evidence/` является rolling output и перезаписал ранние FPS-run logs, исторические counts/hash FPS остаются в его summary.

Четыре реальных устройства, TV и performance reference gate не закрыты. FPS на картинке — только UI evidence.
