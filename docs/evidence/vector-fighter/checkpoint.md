# VECTOR checkpoint — F3 all-weapon grips and local test build

Change replace-player-with-vector-fighter; owner 01a15f1c-f40b-7c41-a66c-6a091778d889.
Branch codex/vector-fighter, baseline 2f13175cf113f27d82275b26f7ab88b43d694e25.
No integration into main. F3 source commit and Player identity are recorded in f3-build.json after build.

F3 implements separate named contact profiles for Rifle, Shotgun, RocketLauncher and Cutter. The support palm reaches a fixed weapon-local contact using a two-bone arm solve without stretching. Palm yaw/roll, individual finger-segment curls and opposed thumb wrap replace the previous open hand alongside the barrel. The established primary wrist fit remains; all four primary contacts are checked in aim/fire/air/reset. Every modified local pose is restored before graph sampling, including unkeyed channels. Visible slot is selected before evaluating grip, including the switch apex and completion-tick muzzle reads. Trooper @12 reconstructs exact @11 and earlier saved history.

F2 thin forearms, matte cloth, broad shoulders, geometry/materials, cameras and weapon/gameplay balance remain unchanged.

Final affected EditMode 27/27; graphical PlayMode 4/4. Actual current FPS and both sides of both hands: f3-fps3/. Synthetic world pose evidence: f3-world3/. Baseline f3-before/ and rejected-fit examples f3-diagnosis/ are historical. Exact commands/results and compressed logs: verification-plan.md and f3-qa/. Final source hashes: f3-source.json.

Next authorized step: local macOS Development Player build, real-session jump/fire evidence for all weapons and existing native weapon-switch/death/respawn smoke. Keep the sole test app pending human acceptance. No installation, release, publication or full suite is authorized. Guide: f3-test-guide.md.

Open gates: human/visual/device/audio/performance acceptance; whole-model art fidelity and ordinary full world-action acceptance remain separate. Do not mark the overall change complete or merge it merely for the test build.
