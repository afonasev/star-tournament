# F4 — чистая подготовка матча

Реализация: 42beb5f. Удалена кнопка смены цветов команд; вкладки без номеров; подсказка Y/X отдельно над вкладками. Стандартная палитра и gameplay не изменены. Тест командной идентичности проверяет отсутствие удалённой кнопки и стандартные цвета при запуске, смерти, повторе и возврате в FFA.

Scope: UI. Обязательные проверки: make check-ui, RosterCardsTests и NativeTeamsTests, сборка и muted native menu/setup. Сценарий menu снимает карты, оба режима правил и состав; setup покрывает клавиатурную навигацию.

Проверки: make check-ui — 3 EditMode + 12 PlayMode Passed; RosterCardsTests + NativeTeamsTests — 10 Passed, без пропусков. Build: STAR_PROVING_BUILD_PASS 501487278. Проверенная source revision — 42beb5f. Полный gameplay gate для локальной UI-правки не повторялся.

Native menu/setup Passed: 26 + 15 кадров. Визуально проверены 06b-rules-teams и 07-players: кнопки смены цветов нет, вкладки без номеров, подсказка Y/X над ними без пересечения. [Галерея](gallery.html). Exact candidate и SHA256 — candidate-manifest.json; результаты — verification.json. Временные задержки вызваны общей очередью Unity; чужие процессы не останавливались. Визуальная и физическая приёмка остаются отдельными от автоматической проверки.
