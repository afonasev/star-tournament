# Affected QA
Baseline: 9874096b79d14cce564f129bb6f24418c49d4a14. Approved three imagegen concepts: user chooses all sequentially, 17.10.2026, chat 01a14a63-684f-7e71-bc3a-91178ffa2c2e.
Presentation-only materials, opaque mesh batching/splitting, cosmetic floor graphics and existing fixture motifs. Exact map @4, all 56 furniture solids, collision, navigation and gameplay unchanged.
After each layer: UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter WellStoreyPresentationTests -testResults <owned>/stage.xml -logFile <owned>/stage.log
Final: add WellSignPlacementTests and WellCorridorVisualTests for actual text bounds and architecture no-overlap audit. No natural matches because simulation/physics not modified. No Player build, full suite or deployment requested.

Additional affected visual check: WellLabDetailVisualTests (same source snapshot, shared -noaudio -muted) binds NativeVisualPresentation and confirms material registration, current effects/environment, full shader/texture contracts, 33 real views including close-ups of all three department modules. This is necessary because new surface materials participate in NativeVisualMaterials, which the bare geometry fixture does not exercise.

The full-effect visual close-ups exposed a pre-existing department classification error: FloorToInt of a computed 14.4m wall bottom could return floor 1 from float error. Wall and furniture department selection now uses nearest authored storey, with per-vertex diagnostic-mesh bounds checked against the correct storey. Rerun all four affected fixtures on the corrected runtime source, retaining the initial source/result and revealing screenshot for audit.
