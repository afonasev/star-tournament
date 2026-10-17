# Целевые проверки
Baseline: 67ef83a33cbb93d44cc5e7422147dc00040b6cf0, ветка codex/well-joints-idle.
Изменены только Well presentation, лифтовая state machine, Well Lab v4/migration и соответствующие specs/tests.

- `UNITY_RUN_MODE=shared unity/tools.sh test-edit-filter "WellArenaTests;WellSurfaceMeshTests"`: точный таймер, заполненность, остаток tick, restore/reset/validation, исторические Well Lab hashes и mesh coverage.
- Графический PlayMode с фильтром `WellArenaPhysicsTests.LiftCarriesRealCapsuleRestoreAndStopsWithPassenger;WellArenaPhysicsTests.BotBoardsExitsSecondStoreyAndEmptyTripCompletes;WellArenaPhysicsTests.UnavailableLiftsKeepStairFallbackAndMotorReachesSecondFloor;WellArenaMatchTests;WellJointStabilityTests`: только физическая капсула/restore, bot lift/stair fallback, игровой pause/restore/Repeat и render стыков.
- Render QA: direct shared Editor `-batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellJointStabilityTests`, с графикой, RenderTexture: реальные двери, лестницы и контактные стыки.

Wrapper проверен: filtered команды не собирают Player, задают -noaudio -muted с самого старта и изолируют QA Lab history. Полный прогон, Player build и deploy не запускать без отдельной команды.

Повторы после найденных проблем:
- `test-edit-filter WellSurfaceMeshTests` после отсечения скрытой части дверных рам; затем тот же fixture после union плоских участков металлических рам.
- PlayMode `WellArenaMatchTests;WellJointStabilityTests` после корректировки вручную создаваемого движущегося snapshot (IdleSeconds=0) и исправления дверной подшивки.
- PlayMode `WellJointStabilityTests` после удаления пересекающихся плоских участков угловых оконных рам. Уточнён аудит: учитывает фактическую плоскость треугольника, а не только normal одной вершины фаски. Порог площади и отсутствие перекрытий сохранены.

Успешные лифтовые проверки не повторялись после presentation-only исправлений: код state machine, физические solids и motor не менялись.

Последний render повтор дополнительно сохранил прежний ресурсный бюджет. Первый union превышал его; исправлены vertex reuse и дробление непересекающихся треугольников. Assertions и лимиты не ослаблены.
