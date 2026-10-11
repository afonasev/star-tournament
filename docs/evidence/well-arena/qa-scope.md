Baseline: 1b0a7ca4ee3b53ddbc99751d0b0fbfb1501a42ab (main, 12.10.2026).

Diff: новая authored геометрия/каталог/меню, Well Lab profiles/migration, динамические платформы и motor carry, session/driver snapshot, bot lift adapter, presentation.

Проверки (до запуска):
- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter 'WellArenaTests;LunarLaboratoryTests;NativeNavigationTests;DesignLabHistoryTests'`: собственные geometry/lift state + соседние immutable map, navigation и Lab migration контракты.
- `UNITY_RUN_MODE=shared unity/tools.sh test-play-filter 'WellArenaPhysicsTests;NativeNavigationPhysicsTests;NativeCombatTests'`: реальная капсула/лестницы/окна/мосты/pickups/лифты/боты/restore плюс соседняя navigation и session lifecycle. 

Полный набор не разрешён. Player build/release/deploy отложены; physics и состояние проверяются в Editor PlayMode без сборки. Human visual/gameplay и device acceptance остаются отдельными.

Первый EditMode run компилировал исходный блокинг; до PlayMode уточнены два anchors после проверки габаритов оборудования и добавлены lift-authoring metadata/собственный migration контракт. После него повторяется WellArenaTests на актуальном source snapshot.

Первый запуск: 77/79; реальные ошибки исправлены (высоты новых spawns, тестовый shipped bundle). Следующий запуск прерван владельцем до завершения для включения этих исправлений, NotRun. Актуальный EditMode фильтр: `WellArenaTests;NativeNavigationTests;DesignLabHistoryTests.CompleteRegistryHasUniquePathsAndValidDefaults;DesignLabHistoryTests.StartupMigrationKeepsDiskHistoryUntilExplicitLabMutation;DesignLabHistoryTests.TamperedSnapshotHashFailsWithoutDestroyingFile`.

Final affected additions: NativeBotPlannerTests and the two physical navigation Restore/repath scenarios, because preserving NextRepath changed shared navigation restore. Full capsule boarding has an explicit serialized Boarding phase; bot transit and real Match/Pause/Repeat rechecked. New empty-terminal-arrival rearm case is checked in WellArenaTests. Final visual-only sign fixes use the existing depth-tested LunarSign shader; physics geometry stayed unchanged. XML and compressed logs, including failed/NotRun iterations, are retained in tests/; latest pictures belong only to well-visual-3.
