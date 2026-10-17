# Well lighting exploration, 2026-10-18

Baseline: main 1e1536c37201f764f06604d62b00a006ab9289a4.
Scope: review-only PlayMode render experiment; no game runtime/assets or planning mutations.
Existing accepted materials/signs/navigation/furniture retained. Authored map @4 exact JSON, 24 local lamps, 56 furniture solids, no presentation colliders asserted.

Minimum affected run:
`tools/unity.sh shared -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter StarTournament.ProvingGround.Tests.WellLightingStudyTests.CaptureComparableLightingDirections -testResults docs/evidence/well-light-study/playmode.xml -logFile docs/evidence/well-light-study/playmode.log`

Reason: real Unity/URP images are required to compare ambient/key/local light and postprocessing. One capture fixture, 4 treatments x 7 fixed views; no full suite, natural matches, Player build, performance or device acceptance. Graphics retained (no -nographics). Camera 1280x720, FOV 75, HDR target. Baseline and variants all use the same geometry/materials/cameras/default preference treatment. Test temporarily sets in-memory graphics preferences to defaults and restores exact prior keys; no Save call.

Treatments are experimental art candidates, not approved runtime changes. 0: current defaults. 1: neutral lab ambient .40/key 1.1/local 80. 2: directional daylight ambient .32/key 2.0/local 65. 3: practicals ambient .30/key .6/local 110, department lamp tints. Variants use existing 24 spots with soft shadows and Neutral tonemapping; baseline has original no-tonemapper path. All remain in the baseline Gamma color space. See fixture for full settings and render-state.txt for resolved values.

Iteration 2 follows visual rejection of the first, too-dark pass. First evidence retained under initial-pass/. Experimental spot intensity exceeds current Lab descriptor maximum 50: these are review-only direct Light values, not an approved range change. A/B/C exposure +.65/+.55/+.70 EV; no automatic exposure. No test assertions were weakened.

Final iteration: brightest second pass retained under bright-pass/. Existing spots now 80/65/110; ambient .40/.32/.30; key1.1/2.0/.6. Exposures +.35/+.35/+.45 EV. Final 1/1 Passed, 28 raw captures. Both earlier passes also Passed technically; visual rejection and parameter history are preserved separately.
