# Affected QA
Baseline: cfce72ba541a2d35003f11367969e2d36366d553.
Diff: WellArenaPresentation sign placement/scaling only; authored geometry, collision and all Lab data unchanged.
Run: UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellSignPlacementTests -testResults <owned>/playmode.xml -logFile <owned>/playmode.log
Reason: real TextMesh glyph bounds/rendering require Unity. Audit all 24 signs at signScale 0.5/1/2, full rectangular backing and absence of structural intersections, exact map equality and no new colliders. Save default-scale Editor views of every sign. No Player build, full tests, release or deploy.
