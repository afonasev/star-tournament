# F7 active checkpoint
Owner 01a1601c-bc6a-7c90-ac69-a383d5f881fb; same worktree/branch codex/vector-fighter. Baseline6d57c9e07a0edf7e1e49617888164becb1e1900c. User scope: posed costume and glove skin repair, RocketLauncher/Cutter grip completion, visible grounded short steps and jump/flight/landing. No new overlays/full suite/build/merge/deploy/resource deletion.

Current source checkpoint (execution gates below remain open):
- VectorGroundedGait locks support feet in world space, swings the other foot and lowers only visual pelvis to satisfy leg reach. No movement threshold for body walk state, first-person sway unchanged. Airborne pose precedes grounded foot solve; death/ragdoll bypasses it. @15 adds named stepSeconds/stepLengthMeters/stepHeightMeters with exact @14 predecessor.
- Initial gait fixture3/5 PASS,2/5 FAIL at2/6m/s due stance beyond straight-leg reach; preserved qa/initial-gait/results.xml. Reach pelvis correction is not yet executed by Unity. Second queued test cancelled BEFORE start to prepare changed assets first; NotRun.
- refine_pose.py regenerates existing50-object F6 study without erroneous radial shell deformation, conforms covered cloth to rigid existing shells, separates knee/shin bounds, transfers existing glove seam skin from cloth. No new model objects. Editable f7-pose-repair.blend, candidate-body/arms GLBs in .local/vector-f7.
- Initial shell projection collapsed inner/outer walls, failed render kept. Copying cloth weights to armor also failed (wrinkles). Final approach keeps armor rigid and modifies covered cloth instead. Latest real GLB renders model-final/. Not human art acceptance; minor shoulder transitions remain reviewable.
- New GLB candidate copied to actual Art paths and manifests updated. Native prefab refresh completed successfully (prepare.log); prefab mesh/material references refreshed. Unrelated scene serialization restored to baseline because prefab GUIDs did not change.
- fit_vertical_grips.py uses actual earlier Unity skin, stronger explicit joint wrap targets and increased shared curl range. .local/vector-f7/vertical-fit.txt is a candidate, NOT defaults. Needs fresh real Unity capture with repaired skin.
- Added focused VectorGroundedGaitTests (5 cases) and NativeShortStepsStrafeAndJumpKeepWorldContact PlayMode fixture (native motor, CSV, optional240 frames). No assertions weakened.

Next dependent steps: affected gait+import+weapon fixtures and new predecessor history are queued (.local/unity-evidence/focused-editmode.INSMGu); then real graphic all-weapon grip capture with immutable candidate and world native short-step/strafe/jump sequence. Inspect actual frames and fix demonstrated issues. Preserve source/evidence, commit and update canonical delivery via helper under planning lease. No integration authorized.

Resources retained: .local/vector-f6, .local/vector-fighter, .local/vector-f7, unity/Library and all source studies. Main/foreign planning changes untouched. No new chats/subagents/messages.

Gait correction also starts the first step at the beginning of the observed movement interval, lowers pelvis only by the geometric reach deficit, and uses square-root height scaling so very short motions visibly lift a foot. Corrected runtime QA still pending shared-runner admission.
