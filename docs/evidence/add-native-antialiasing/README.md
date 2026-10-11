# Сглаживание URP: Editor evidence

## Результат сравнения

Выбран штатный SMAA URP. Для значения по умолчанию используется Medium как умеренный режим для 1–4 split-screen камер; настройки предлагают Off, Low, Medium и High. API подтверждён установленными Unity 6000.3.23f1 и `com.unity.render-pipelines.universal` 17.3.0. SMAA задаётся через `UniversalAdditionalCameraData`; глобальный MSAA не включался и существующие AO renderer variants не менялись. Когда остальные эффекты выключены, AA сам включает camera post-processing, не создавая Volume.

Кадры сделаны на Apple M2 Pro / Metal из Unity Editor PlayMode, в одном и том же замороженном игровом ракурсе, четыре viewport 800×450 в итоговом PNG 1600×900. Для каждой арены сняты None и SMAA Medium. На исходниках заметно мягче тонкие диагонали решёток и кромки панелей; разница умеренная. Контуры оружия и видимых игровых объектов сохраняются без заметного размытия. HUD и прицел используют `ScreenSpaceOverlay`, остаются после camera post-process; это отдельно проверяется fixture. Настройки Image view захвачены в Editor UI.

[Общий обзор трёх сравнений](comparison-overview.png) — в каждом ряду показан первый viewport в полном разрешении.

### Исходные кадры в полном разрешении

- Combat Bowl: [None](captures/combat-bowl-v1-antialias-none.png) · [SMAA Medium](captures/combat-bowl-v1-antialias-smaa-medium.png)
- Industrial Tunnels: [None](captures/industrial-tunnels-v1-antialias-none.png) · [SMAA Medium](captures/industrial-tunnels-v1-antialias-smaa-medium.png)
- Lunar Laboratory: [None](captures/lunar-laboratory-v1-antialias-none.png) · [SMAA Medium](captures/lunar-laboratory-v1-antialias-smaa-medium.png)
- Настройка в «Изображение»: [Editor UI](captures/antialiasing-setting.png)

## Выбранные проверки

Baseline: `b08bed15d64f8834cf1cd2992257a83d23fef009`; implementation commit: `fa570338a46dbd5e3ff3203f75c13563e6c0b7e5`. В diff только общий preference, его settings UI, URP camera application и затронутые fixtures.

| Проверка | Результат |
|---|---|
| EditMode `VisualPolishSettingsTests.AntialiasingPreferencePersistsAValidSharedLevelAndRepairsInvalidValues` | Passed 1/1; default, persistence всех уровней и восстановление после невалидного значения |
| PlayMode `VisualPolishTests.GraphicsControlsPersistAndScrollToLastEffect` | Passed 1/1; settings UI, смена уровней, повторное открытие и прокрутка фокуса |
| PlayMode `IndependentPauseMenuTests.AntialiasingPropagatesAcrossOpenSeatSettingsAndAllCameras` | Passed 1/1; геймпад, 2 открытых personal views, автоматический scroll-focus и обе активные камеры |
| PlayMode `VisualPolishTests.AntialiasingIsIndependentAndComparesIdenticalViewsOnAllArenas` | Passed 1/1; реальные camera API, SMAA Low/Medium/High, AA-only post-processing, четыре камеры, три пары GPU кадров |

XML-результаты: [EditMode](test-results/editmode.xml), [settings UI](test-results/ui-playmode.xml), [две паузы](test-results/pause-playmode.xml), [GPU captures](test-results/capture-playmode.xml). [Unity log GPU capture](test-results/capture-playmode.log) фиксирует Editor/GPU и URP render path.

Команды выполнялись через `~/.local/bin/unity-run --shared --project /Users/eaafonasev/.codex/worktrees/native-aa-settings-7680/star-tournament/unity -- /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/eaafonasev/.codex/worktrees/native-aa-settings-7680/star-tournament/unity -runTests -testPlatform <EditMode|PlayMode> -testFilter <точное имя из таблицы> -testResults <путь в .local/unity-evidence/add-native-antialiasing> -logFile <путь рядом с XML>. `-nographics` не использовался. Player/release build, full test suite и deploy не запускались. Этот результат не измеряет FPS на целевых устройствах и не является human/Player acceptance.

## Повторение

Запустить соответствующий XML test с тем же filter в Unity Test Runner. Для трёх кадров использовать фильтр `VisualPolishTests.AntialiasingIsIndependentAndComparesIdenticalViewsOnAllArenas` и задать `STAR_TOURNAMENT_QA_LOADING_SCREENSHOTS` на каталог captures. Сравнение выполняется на GPU; не добавлять `-nographics`.
