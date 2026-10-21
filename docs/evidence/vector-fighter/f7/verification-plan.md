# F7 verification scope
Baseline codex/vector-fighter 6d57c9e07a0edf7e1e49617888164becb1e1900c.
User: fix posed costume geometry and glove skin, fit RocketLauncher/Cutter; actual small movements must step, jump and land visibly. No new overlays; retain resources; no full suite.
Dependencies: shared body/FPS GLB skin, manifest/prefabs, grip defaults/history, TrooperVisual/VectorAirbornePose presentation. Motor/camera/weapon dimensions unchanged.
Minimum: actual GLB studio poses; focused import/weapon fixtures; targeted new grounded distance/stance/pause/airborne fixture; exact predecessor history if profile changes. Actual Unity grip captures for all four weapons, and world short-step/strafe/jump/landing sequence with moving root and measured stance contact. All Unity via shared runner, -noaudio -muted, isolated history.
Technical fixture PASS does not imply visual/human/device acceptance. Preserve failed attempts. No Player/full/build/integration/deploy.

Capture repair: native-final metrics passed but arena geometry occluded the body. Re-run ONLY NativeShortStepsStrafeAndJumpKeepWorldContact with identical input/assertions, actor layer11 plus a non-colliding review grid at y=-.001 and diagnostic fill light. This is a labelled native-motion diagnostic, not an ordinary camera/gameplay view. Runtime unchanged. Command: UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter NativeShortStepsStrafeAndJumpKeepWorldContact; isolated history and STAR_TOURNAMENT_WORLD_STEPS_EVIDENCE in .local/vector-f7/native-motion.
