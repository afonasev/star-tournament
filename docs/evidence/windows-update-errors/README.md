# Windows update error feedback

Baseline: `05c03b72`. Исправление согласовано 2026-10-11.

Реализованы понятные причины и явный повтор в меню, окно Windows при ошибке до
запуска Player, журнал этапов/исключений, постоянный журнал попыток установки.
Неудачный handoff больше не запускает ту же автоустановку при каждом открытии
ярлыка; установленная игра остаётся доступной, повтор требует явного действия.
Ошибки IPC остаются видны при опросе статуса. Подпись, SHA256 и protected Windows
activation не ослаблены. Зависание заголовков/чтения ограничено 30 секундами.

Проверки:
- Feedback contracts: 33/33, actual production C# source; сообщения, повреждённые
  журналы, сохранение/сброс попыток, зависание headers/body, отмена и целостность.
- NativeUpdateMenuTests: 3/3, PlayMode, 27.591 секунды NUnit; consent/controller,
  installed identity, errors/retry, сохранение сообщения и отсутствие clipping.
- Maintenance compile: success, 0 warnings/errors, без publish/Player/installer.
- Strict validation OpenSpec и authored git diff --check.

Первый UI запуск: 2/3, новый тест не задал release identity; исправлен setup.
Второй UI запуск: 2/3, RenderTexture не создалась при capture с -nographics.
Повтор с графикой: 3/3; после улучшения читаемости финальный повтор тоже 3/3.
Контракт/ассерты не ослаблены. Холодный import/compile не является временем тестов.
Снимок previews/apply-error.png — offscreen Editor capture, не Windows/Player
приёмка. Сохранены исходные XML (первый failure — gzip без изменения байтов) и сжатые логи обоих неуспешных запусков и final.

Ограничения поставки:
- Стабильный maintenance находится вне current и не заменяется обычным пакетом
  игры. Его новые подробные сообщения требуют нового установщика; новый launcher
  предотвращает повторную автоактивацию и при старом maintenance.
- Реальные Windows MessageBox/UAC/установка/перезапуск/платформенная и визуальная
  приёмка ожидают отдельной проверки после явно заказанной сборки/поставки.
- Player, установщики, релиз, публикация и full suite не выполнялись.

Cleanup: временные Library/Logs/UserSettings, bin/obj и .local удаляются после
сохранения evidence; Unity whitespace-only настройки worktree возвращены к
baseline. Чужие изменения основного checkout сохранены.
