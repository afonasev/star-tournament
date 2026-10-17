# Q8/29: мастер матча и состав

Baseline `codex/game-standard-alignment@9a4e083c`. Согласованный29; новое визуальное направление не вводится. Стабильные названия «ДАЛЕЕ», «НАЧАТЬ МАТЧ», «+ ИГРОК», «+ БОТ» отделены от pad glyph. Start использует тот же CanAdvanceSetupInput, что обработчик: сохраняет phase/editor/pending identity/transition-frame/invalid roster guards. Нижняя кнопка по-прежнему подтверждает текущий фокус. X/Y у roster actions удалены: actual West AddBot не открывает редактор, а экранная кнопка открывает; North Join тоже не тождественен AddRosterHuman. Ложные shortcut не добавлены, input mappings не менялись.

## Проверки

Команды/выбор до запуска — plan.md; source snapshots — red/green/final-source.json. Unity6000.3.23f1 через unity-run --shared, -batchmode -noaudio -muted; red дополнительно -nographics, green/final с offscreen render.

- red.xml:1 настоящий Failed (caption «ДАЛЕЕ · A / Start»), runtime baseline неизменён, exit2. Тест-only diff сохранён.
- green.xml:3/3 passed, exit0: SetupShortcutHintsFollowDeviceAndEditorAvailability; StartHoldAndSimultaneousSubmitCannotSkipSteps; KeyboardKeepsEnterTransitionsAndGamepadTakesContentFocusWithoutRebinding.
- final.xml:1/1 passed, exit0: расширенный первый метод после удаления roster X/Y; чистые подписи, отдельный Start, keyboard hides, editor blocks, invalid roster blocks и фактический South add-bot→editor сохранены.
- Итого3 unique PlayMode метода; final — повтор с дополнительными assertions, не четвёртый метод. Full suite не запускалась. Player/installer/build/deploy не запускались.

Actual rendered images/04-map-start-hint.png и05-roster-start-hint.png просмотрены; исходный roster с permanent X/Y сохранён в initial-images для сопоставления. Это Editor/synthetic input evidence, не physical/native/human acceptance. Семейный formatter уже покрыт core8 EditMode; здесь переиспользован без изменения. Обновлены4 строки Q8 общей матрицы и audit.json, is_completion_certificate=false. Нормативные feature-файлы последнего local template9cc4415 идентичны pin60fe3d2/origin-main: добавился только AGENTS.md с правилами визуального выбора.

Owned Unity93259/94639/96935 завершены (exit2/0/0). После сохранения доказательств scratch удалён, incidental ProjectSettings/ShaderGraphSettings восстановлены. Активные owned worktree/Library нужны незавершённой полной задаче; чужие checkout/process/cache не затронуты.

Следующий шаг: согласованный Q9 macOS DMG30 (source pipeline вместо PKG, first-install preflight, публикационные контракты), затем обязательный automatic Windows0.1.5/test.1 bootstrap/handoff/recovery/native proof и финальная полная matrix/integration/acceptance. Нельзя выдавать этот срез за выпуск или за проверку старого Windows-перехода.
