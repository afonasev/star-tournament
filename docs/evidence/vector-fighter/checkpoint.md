# VECTOR checkpoint — F3 all-weapon grips and local test build

Change replace-player-with-vector-fighter; owner 01a15f1c-f40b-7c41-a66c-6a091778d889.
Branch codex/vector-fighter, baseline 2f13175cf113f27d82275b26f7ab88b43d694e25.
No integration into main. F3 source commit and Player identity are recorded in f3-build.json after build.

F3 implements separate named contact profiles for Rifle, Shotgun, RocketLauncher and Cutter. The support palm reaches a fixed weapon-local contact using a two-bone arm solve without stretching. Palm yaw/roll, individual finger-segment curls and opposed thumb wrap replace the previous open hand alongside the barrel. The established primary wrist fit remains; all four primary contacts are checked in aim/fire/air/reset. Every modified local pose is restored before graph sampling, including unkeyed channels. Visible slot is selected before evaluating grip, including the switch apex and completion-tick muzzle reads. Trooper @12 reconstructs exact @11 and earlier saved history.

F2 thin forearms, matte cloth, broad shoulders, geometry/materials, cameras and weapon/gameplay balance remain unchanged.

Final affected EditMode 27/27; graphical PlayMode 4/4. Actual current FPS and both sides of both hands: f3-fps3/. Synthetic world pose evidence: f3-world3/. Baseline f3-before/ and rejected-fit examples f3-diagnosis/ are historical. Exact commands/results and compressed logs: verification-plan.md and f3-qa/. Final source hashes: f3-source.json.

Local macOS Universal Development Player built and retained at:
`/Users/eaafonasev/.codex/worktrees/vector-fighter/star-tournament/unity/Builds/StarTournamentProvingGround.app`.
Exact source commit: `36667859544f6fb214a4b1981749ef39cb07c8ef`; branch codex/vector-fighter. Hash manifest and build/smoke identity: f3-build.json and f3-build-files.sha256. Local main is not integrated (ancestry and patch equivalence checked).

Final exact-app native evidence: all four weapons jumped, landed and accepted fire, with 30 real camera frames (f3-player/). Native weapon-switch diagnostic passed 39 checks including 36 transitions at .1/1/3 seconds, pause, four seats and death/respawn; frames and video in f3-switch-player/. Old failures are preserved: inactive window, missing batch backbuffer, legacy base-root expectation. Development capture now renders cameras directly, and the switch diagnostic preserves exact equality to the accepted configured rest + ViewOffset contract. No air pose disabling, thresholds, switch durations or gameplay changes.

Guide: f3-test-guide.md. Sole current test app, editable Blender source, current branch/worktree and Library retained pending human review. Raw own F3 intermediates cleaned after preserving XML/compressed logs.

Open gates: human/visual/device/audio/performance acceptance; whole-model art fidelity remains separate. Overall change remains implementing, not merged or accepted. No install/release/publication/full-suite run. No further task-owned process remains after smoke.
