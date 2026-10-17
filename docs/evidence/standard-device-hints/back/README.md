# Q8/29: реальный возврат и подсказки действий

Baseline: `codex/game-standard-alignment@76b9e43d`. Макет29 согласован. Исходники соответствуют `final-source.json`; базовые тесты и порядок выбора проверок — `plan.md`. Проверки запускались через unity-run --shared, Unity6000.3.23f1, -batchmode -noaudio -muted; Player/full/installer/build/deploy не запускались.

Общий Back обрабатывается один раз за кадр и вызывает реальное действие текущего экрана. Активный геймпад определяется до навигации; первое East и небольшой допустимый right-stick нового устройства больше не теряются. Профили возвращаются в главное меню. Settings показывает B для фактического действия: параметр→раздел, раздел→закрытие; владельцы персональных настроек сохранены. Lab, roster, operator и независимые pause-confirmations используют собственные контексты. Cancel восстанавливает фокус вызвавшей команды. Escape завершает редактирование прежде перехода; диагностическая пауза не открывается повторно тем же потреблённым Escape. Glyph не перекрывает installed badge/dropdown arrow и не меняет значение/название.

## Проверки

- red.xml:2/2 реальных падения при исходном runtime (профили и первое East другого pad), exit2. red-tests.diff и red-source.json сохраняют исходную репродукцию.
- edit.xml:15/15 passed, exit0, без skipped. Тесты formatter/available shortcut/caption geometry.
- initial/play.xml:18 случаев,15 passed/3 failed, exit2. Исправлены предположения fixtures: допустимый East у unfocused Resume; processed right-stick после штатного deadzone; Xbox B в узкой кнопке с неизменённым правилом omission длинного generic glyph.
- targeted.xml:6 случаев,5 passed/1 failed, exit2. Все три уточнённых случая и assigned-controller authority прошли. Диагностический Escape воспроизвёл настоящий повторный вход в паузу.
- final.xml:2/2 passed, exit0, без skipped: исправленный диагностический Escape и соседний all-AI operator/resume. Единственная production-правка после targeted — запрет повторного использования Back этим диагностическим обработчиком в том же кадре.
- Итого20 разных PlayMode-методов прошли (passed-play-methods.txt), включая повторы через разные фазы; не сумма запусков. Остальные успешные проверки не повторялись после локальной диагностической правки.

В `images/` сохранены реальные рендеры Xbox/PlayStation/Switch (объявленные виртуальные layouts), Settings conditional Back и соседняя text-entry проверка; `neighbors/` содержит актуальный Lab discard. Просмотрены Settings, family main/profiles/keyboard. Это Editor render/virtual InputSystem proof; физические устройства, native Player, TV и человеческая приёмка остаются отдельными gates.

Owned runners завершены, scratch/history после переноса доказательств удалены. Собственная побочная сериализация ProjectSettings/ShaderGraphSettings и старый source-pinned Lab screenshot восстановлены из baseline. Активный worktree/Library сохранены для полного parent scope. Изменения не перенесены в main.

Следующий шаг: агент проводит итоговый аудит оставшейся Q8 matrix; затем Q9 (согласованный macOS DMG30) и обязательный автоматический переход Windows0.1.5/test.1. Выпуск ждёт проверки этого перехода; ручная переустановка исключением не является. Полная цель пяти пакетов, интеграция и платформенная приёмка остаются открыты.
