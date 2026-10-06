# 16 — Unity: FPS и настройка видимости

Статус: завершено и проверено; отдельный commit. Ветка `codex/unity-fps-settings`.
Stage 0 перенесён из `dc25fdc` отдельным cherry-pick `6e73eeb` поверх актуальной ветки с asset policy. Основной checkout и предыдущий worktree не изменялись.

## Контракт

GAME_SPEC §5: общий FPS справа сверху, включён по умолчанию; native кнопка-переключатель в setup/pause. Выбор сохраняется через PlayerPrefs. Частота обновления 2 Гц — технический инвариант телеметрии; размер/отступ derived из существующего profile `ui.fontSize`. FPS = число кадров / суммарное unscaled время окна, длинные кадры не фильтруются. Overlay не принимает pointer events и не меняет состояние симуляции.

## Проверки

- EditMode: 8/8; расчёт среднего, длинный кадр, invalid delta.
- PlayMode: 9/9; Mac Development Player build PASS (324721027 bytes).
- Muted native Player: setup/live/pause вкл/выкл, resume и сохранение после перезапуска проверены. [Screenshots и hashes](../../evidence/unity-fps-2026-09-19/README.md).
- Открытые Stage 0 physical/performance gates сохранены. Число FPS в overlay не закрывает 60 FPS acceptance.

## Воспроизведение

`unity/tools.sh test-edit`, `unity/tools.sh test-play`, `unity/tools.sh build`.
Локальный Player: `unity/Builds/StarTournamentProvingGround.app`.

Первый вариант regression теста перегружал всю сцену additive и обнаружил старое отсутствие ownership runtime-root объектов (дубли EventSystem/AudioListener). Прогон остановлен; проверка настройки изолирована на создании UI-компонента. Полный scene lifecycle относится к следующему match-loop change.
