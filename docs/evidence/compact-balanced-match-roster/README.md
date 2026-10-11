# Подготовка участников — QA scope

Baseline: 5447059799465b31edce2514036c6591edc9acbb.
Изменения: profile picker geometry, roster team placement on new views/bots, main-menu activation device priority. Simulation, combat and match outcomes не меняются.

Минимальный набор:
- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'SeatInputTests;RosterCompactionTests;NativeBotSetupTests'`: ownership, compaction и независимая модель roster.
- `unity-run --shared --project <worktree>/unity -- Unity -batchmode -projectPath <worktree>/unity -runTests -testPlatform PlayMode -testFilter 'RosterCardsTests;DefaultRosterTests;SetupGamepadNavigationTests;NativeParticipantRosterTests' -testResults <worktree>/.local/compact-roster-play/playmode.xml -logFile <worktree>/.local/compact-roster-play/playmode.log`: реальные UI/input/lifecycle сценарии, стартовое устройство, состав/команды и навигация. Переменная STAR_TOURNAMENT_SETUP_QA_DIR сохраняет offscreen Editor PNG.

Player build, full, physical controller/visual acceptance и release/deploy отложены. Никаких natural matches: матчевые правила не меняются.

PlayMode запускается с графикой для offscreen RenderTexture capture, без системного ввода/фокуса: shared. Изолированный QA Lab history: `.local/compact-roster-play/lab-history.json`.
Первая попытка остановилась на компиляции нового теста (неверный factory call каталога профилей); исправлено на существующий constructor, тесты тогда не исполнялись.

## Результаты

EditMode: 15/15 Passed, XML duration 0.164 s (без первичного импорта).
PlayMode: 26/27 Passed первоначально; единственный новый Y/reconnect тест ошибочно создавал пустую вторую команду при повторном Y. В fixture добавлен сохраняющий TeamB бот, assertions и runtime не ослаблены/не изменены. Точечный повтор: 1/1 Passed. Все 27 выбранных сценариев подтверждены, skipped=0. XML и исходные логи сохранены рядом; runtime hashes в results.json связывают оба прогона с одними исходниками.

`compact-profiles.png` визуально проверен: восемь различимых профилей, фокус, занятый профиль и дальнейшая прокрутка. Остальные PNG — соседние map/rules/roster navigation states. Все изображения — offscreen Editor, **не** Player/human/physical acceptance.

QA не создавала Player. Выполнены shared Editor PlayMode/EditMode, scoped diff check. После успешных проверок удаляются созданные cache/log/history ресурсы и интегрированный managed worktree; raw evidence сохраняется в Git. Основной checkout содержит чужие Unity ProjectSettings edits, они сохранены.
