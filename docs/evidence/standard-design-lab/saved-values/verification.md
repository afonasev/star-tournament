# Сохранение собственных значений Lab

Baseline: codex/game-standard-alignment@478417db. Pin:0c2d4562683252d54736f294dc1af463050eac17, LAB-UPGRADE-01/02. Q2: user reply call_0837c76c7a3645b691acee3f5741a42e,17.10.2026 — «мои правки баланса доложны сохраниться».

Изменение: own local/project snapshots обходят factory retune/clamp, даже если сохранённое значение совпало с прежним default. Авторский Default не заменяется factory current при migration. Missing fields и structural mappings сохраняют существующий маршрут; trusted validation обязательна.

Проверка выбрана до запуска: EditMode точные affected методы OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes (6 legacy generations, local/project/author Default); два footstep history и serialized-scene сосед; blood history (2 cases), visual history и сцена; LabAuthoringTests.UpgradeFillsMissingFieldsFromLatestReleaseDefaultAndKeepsZeroFalseAndOriginal (2 modes); LabCompatibilityDiagnosticsTests (5 methods/6 cases). Зависимости: текущие defaults/immutable history/reload, новые поля0/false, type/range/resource diagnostics. Shared wrapper, -noaudio -muted -nographics; без Player build/full suite/rendered QA.

Команда и итог фиксируются рядом после исполнения. Существующие factory/scene retune expectations остаются; заменены только own-profile ожидания согласно новому решению.

Первый запуск initial.xml:14 passed/5 failed,19 cases. Четыре ошибки в подготовке/ожиданиях новых тестов (public reflection, точный trusted predecessor, ожидание отсутствовавшего поля); BeforeWellDetail выявил несовместимый с нынешним шагом well.fill=.8. Изменённые во время первого запуска исходники diagnostics не входили в его скомпилированный snapshot; первый запуск не считается проверкой финального snapshot.

После разбора добавлены две явные refusal-проверки: прежний angle145 выше current max75 и well.fill=.8 вне нынешней сетки default.42/step.05. Подготовка использует точные trusted registries. Они должны сохранить bytes и вернуть readonly Default с причиной; catch миграции очищает частично загруженную selection, не отдавая её runtime. Новые поля visual.ambientLift и visual.bowlLocalLightScale отсутствовали в источнике, поэтому прежние assertions актуального Default сохранены.

Финальная команда:
```sh
tools/unity.sh shared -batchmode -noaudio -muted -nographics -runTests -testPlatform EditMode -testFilter 'DesignLabHistoryTests.OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes;DesignLabHistoryTests.FootstepMixUpgradePreservesSavedOldDefaultAndHistory;DesignLabHistoryTests.FootstepMixUpgradeKeepsCustomOldInterval;DesignLabHistoryTests.SerializedV10ProfileGetsNewStepDefaultsWithoutOverwritingCustomInterval;BloodProfileTests.PreviousIntensityHistoryPreservesAllSavedValuesAndSnapshots;VisualPolishSettingsTests.ArenaHarmonizationUpgradePreservesHistoryAndCustomTuning;LabAuthoringTests.UpgradeFillsMissingFieldsFromLatestReleaseDefaultAndKeepsZeroFalseAndOriginal;LabCompatibilityDiagnosticsTests' -testResults docs/evidence/standard-design-lab/saved-values/editmode.xml -logFile docs/evidence/standard-design-lab/saved-values/editor.log
```
Фактические output аргументы абсолютные внутри owned worktree; команда не собирает Player.

Финальный основной запуск:20/20 Passed (editmode.xml), без skipped. Body408.375s; это проверка истории/serialization, не performance benchmark. Source runtime после этого запуска не менялся.

По анализу общего UpgradeSnapshot до следующего запуска affected-набор расширен точными старыми контрактами: FirstPersonWalkTests.EquipmentReadabilityHistoryPreservesCustomValuesAndOriginalRevision (3 cases), WellArenaTests.PreviousWellArtRefusesIncompatibleGridWithoutChangingSavedValues (2 формата, byte-exact отказ), WellArenaTests.PreviousWellDepthLoadsWithoutChangingSavedHashes (2 формата, сохраняется daylight.5), WeaponSwitchPresentationTests.LabUpgradePreservesPreSwitchAndOlderProfileHistory (2 cases ×9 predecessors; low145 явно отказан, medium25 сохранён, отсутствующее новое поле60). Это замена approved own-history policy, не удаление assert ради green; исходные hashes/bytes/остальные настройки/reopen по-прежнему проверяются.

Neighbor command: тот же shared -batchmode -noaudio -muted -nographics -runTests -testPlatform EditMode, -testFilter 'FirstPersonWalkTests.EquipmentReadabilityHistoryPreservesCustomValuesAndOriginalRevision;WellArenaTests.PreviousWellArtRefusesIncompatibleGridWithoutChangingSavedValues;WellArenaTests.PreviousWellDepthLoadsWithoutChangingSavedHashes;WeaponSwitchPresentationTests.LabUpgradePreservesPreSwitchAndOlderProfileHistory', absolute outputs neighbors.xml/neighbors.log рядом.

Первый neighbor attempt:NotRun, компиляция теста CS0103 (helper в другом namespace); исправлена только полная ссылка на существующий ReadFixture. XML не создан, попытка не считается тестами.

Neighbor executable attempt:7 passed/2 failed (neighbors-initial.xml). Два weapon-switch cases ещё до migration читали compact codec через JsonUtility<LabHistoryFile>, получая пустой Snapshot и KeyNotFound view.y при подготовке старого placement override. Fixture исправлен на существующий ReadFixture(path,legacy), который декодирует настоящий codec без записи; direct historical override/hash assertions сохранены. Остальные7 не менялись и не требуют повтора. Повторяются только2 WeaponSwitchPresentationTests.LabUpgradePreservesPreSwitchAndOlderProfileHistory cases (все9 predecessors каждого сохраняются), outputs weapons.xml/weapons.log. Runtime после20/20 не менялся.

Итог:29 unique affected EditMode cases Passed:20 в editmode.xml,7 неизменённых passing cases в neighbors-initial.xml,2 исправленных fixture cases в weapons.xml (все18 predecessor branches). Первые failed/compile-NotRun попытки сохранены отдельно и не входят в29. Нет skipped; full/Player/build/device/visual/audio acceptance/deploy не запускались. Runtime implementation после основного20/20 snapshot не менялась.

Временные owned run dirs будут удалены после проверки сохранения исходников/доказательств в Git; активные worktree и Library остаются для продолжения полной сверки. Canonical author source и реальные пользовательские файлы не изменялись.
