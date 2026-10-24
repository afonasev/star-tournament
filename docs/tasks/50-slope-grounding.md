# Keep walking on inclines grounded

Baseline: `main@8e03bac53587202be9fccd7d4d99bf2144e1a516`.
Branch: `codex/slope-grounding`. Implementation verified; not integrated into main.

Walking on an incline could lose CharacterController's Below flag. Audio treated upward displacement during that gap as a jump, and the body/view animation entered airborne state. The next movement tick could also reuse measured uphill displacement as upward flight velocity.

CharacterMotor now reacquires nearby walkable support through a downward native sphere cast and controller move, only after a grounded non-jump tick loses Below. The probe uses the existing capsule skin, step offset, world/movement layers and slope limit. Grounded remains based on actual native contact; intentional jumping is excluded. No new balance constants, audio clips or animation tuning.

## Affected verification

- Baseline `unity/tools.sh test-play-filter SlopeGroundingTests`: FAIL, contact lost at 15 degrees descending, 60 Hz, tick 1. Preserved as regression evidence.
- `unity/tools.sh test-play-filter 'SlopeGroundingTests;ProvingGroundTests;GamepadLookAssistanceTests;VectorAirbornePresentationTests'`: PASS 13/13.
- Graphics-enabled shared Editor PlayMode, `-noaudio -muted`, `-testFilter SlopeGroundingTests`: PASS 2/2; includes the previous ramp test and a new actual body animation/edge test. Fourteen unique tests across both passing runs.
- Ascending/descending 15/30 degree ramps at 30/60 Hz remain grounded on every movement tick; accepted real jumps leave ground and land.
- Actual trooper model remains in grounded animation while climbing; walking off the ramp ends support and falls. Captured and inspected walking-up-ramp.png.
- Neighbor checks cover acceleration/braking/jump height, walls/headroom/participant collisions, physical stairs/ramps/navigation and gamepad look assistance/airborne presentation.
- Full suite, Player build/release, audible human playtest and physical device acceptance: NotRun; not claimed by these focused checks.

Evidence (baseline failure, passing XML/logs, screenshot, scope and SHA256 manifest): `/Users/eaafonasev/Projects/star-tournament/planning/docs/evidence/slope-grounding-20261025/`.

Next: integrate the verified branch when authorized, then human gameplay/audio acceptance. Existing main checkout changes and other worktrees were preserved.
