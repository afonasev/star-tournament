# Native CUA smoke — 2026-09-20

Player запущен из worktree 292d без `-botPerceptionReview`; output 1920×1080, звук выключен runtime. Через CUA выбрана именно сборка этого worktree по полному пути (bundle id неоднозначен из-за сохранённой сборки 83e9).

Проверено по свежим screenshots: setup без perception banner; Space назначает keyboard P1; 4→3 seats освобождает четвёртое место; teams показывает A/B/A; существующая camera diagnostic запускает три viewport и постоянную team standings в четвёртой ячейке; Escape открывает pause; Repeat сбрасывает таймер на 05:00 и сохраняет состав; повторная пауза и меню возвращают setup A/B/A; Exit завершает процесс. Runtime exception/error в логе не найдено.

Обычный управляемый матч с тремя реальными устройствами не запускался: P2/P3 не имеют физических gamepads. Camera diagnostic не является доказательством movement/look/fire, reconnect, pointer capture либо TV acceptance. Shipping perception вообще не запускается автоматически — этот smoke проверяет отсутствие изменения существующего human flow.
