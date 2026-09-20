# Native CUA regression

Обычный Mac Development Player без navigation-review flags: Space подключил keyboard/mouse; через UI число seats изменено 4→3, FFA→Teams; запущена существующая «Диагностика: 3 камеры · без управления». Escape открыл паузу; Repeat создал новый матч; затем pause → главное меню → Exit. Лог `ordinary-player.log` в полном архиве. Проверка выполнена на предыдущей сборке с тем же gameplay-кодом; финальное изменение затронуло только opt-in navigation diagnostic measurement.

Реальные P2/P3 gamepads отсутствуют, поэтому controlled human match и физическая device acceptance не заявляются. Synthetic pads navigation harness не заменяют эту проверку.

Финальный Player отдельно запущен в FHD и 4K. CUA активировал окно клавишей Space; screenshot/AX наблюдение подтвердило return-setup после каждого полного journey; Exit закрыл каждый процесс. Все 32 JSON подтверждают focus/mute, и все PNG соответствуют запрошенному output resolution.
