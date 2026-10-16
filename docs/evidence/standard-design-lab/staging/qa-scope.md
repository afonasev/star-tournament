Baseline: f13a9661, branch codex/game-standard-alignment.

Изменение: Editor staging читает canonical project source, заменяет каталог all-and-only marked вместо append-only; atomic replace, неизменяемая published identity/date, semantic no-op, без Prepare/save сцены/Player build/publish.

Минимальный набор: новые staging cases LabReleaseCatalogTests (owned project Resources) и соседние LabAuthoringTests (проекция, исторические identity/default/prod). Команда: unity/tools.sh test-edit-filter 'LabReleaseCatalogTests.ExplicitStagingRetainsProfilesAndPreservesSourceHistory;LabReleaseCatalogTests.Staging;LabReleaseCatalogTests.EquivalentStaging;LabReleaseCatalogTests.MissingMarkedDefault;LabReleaseCatalogTests.FailedAtomicStaging;LabAuthoringTests'. Wrapper: python3 -m unittest tools.test_unity_tools. Все Unity запуски shared -noaudio -muted, без Player/full/gameplay/visual изменений.

После fixtures: явный реальный lab-stage-releases ожидается no-op на текущем author source; сравнить source/catalog/scene bytes до/после. Это source tooling, не сборка/публикация.

Дополнение: StagingEntrypoint и StagingRefuses — отказ произвольного пути до открытия сцены, отсутствие/повреждение author source без reseed или записи каталога.
