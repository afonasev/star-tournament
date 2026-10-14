# Тихий запуск Star Tournament

Тихий режим выбирается параметром `-muted` (Unity также получает engine-флаг `-noaudio`). Перед первой сценой приложение проверяет аргументы запуска и устанавливает нулевой уровень игрового listener до вызова `Awake`, инициализации `NativeGameAudio` и воспроизведения меню. Этот latch действует до конца процесса; `-audioReview`, переходы меню и review-компоненты не могут его снять. Оба тихих параметра (`-noaudio` и `-muted`) имеют приоритет над `-humanAudio`. Настройки эффектов/музыки в PlayerPrefs и системная громкость не меняются.

Команды:

- Unity Editor: `./unity/tools.sh editor -noaudio -muted -projectPath "$PWD/unity"`
- Development Player из `unity/Builds/StarTournamentProvingGround.app`: `./unity/tools.sh run -noaudio -muted`
- Прямой Player: `./tools/unity.sh player /absolute/path/to/StarTournamentProvingGround -noaudio -muted`
- Запуск с человеческой проверкой звука: `./unity/tools.sh run -humanAudio` (убедитесь, что запуск действительно предназначен для прослушивания человеком).

Инструментальные wrappers по умолчанию добавляют `-noaudio -muted`, включая Editor и Player; явный `-humanAudio` выбирает проверку человеком только для интерактивного запуска без признаков QA/review; batchmode, тестовые и `*Review` аргументы всегда остаются тихими. QA launcher (`--qa-unity-runner` или `STAR_TOURNAMENT_QA_UNITY_RUNNER`) удаляет этот человеческий override и добавляет оба флага `-noaudio -muted`. Launcher передаёт аргументы Player и сохраняет выбранный режим при перезапуске после обновления.

`-audioReview` не является разрешением на вывод звука. Для анализа аудиологики используйте захват в WAV/проверку файла без слышимого вывода; если Unity `-noaudio` отключает аудиорендеринг и проверка не может сформировать файл, результат такой проверки — NotRun, а не успешный аудиотест.
