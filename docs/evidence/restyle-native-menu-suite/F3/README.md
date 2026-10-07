# F3 — справка на всю панель и одна подпись FPS

Реализация: a27eed0. UI-only: справка заполняет правую область без обрезания, обе стрелки под изображением переключают схемы. FPS — одна строка с галочкой, без повторных заголовка и описания.

Проверки пока НЕ завершены. Native кандидат F3 не собран; галерея F2 не показывает эту правку.

Ожидаемые команды: `unity/tools.sh prepare`, `make check-ui`, `unity/tools.sh test-play-filter SettingsGamepadNavigationTests`, `unity/tools.sh build`; затем muted native menu/pause1/pause4 на этой сборке. Все Unity-команды используют штатный unity-run.

На момент реализации runtime занят чужим эксклюзивным Player Star Racing. Сохранённый F2-кандидат не удаляется до проверенной замены.
