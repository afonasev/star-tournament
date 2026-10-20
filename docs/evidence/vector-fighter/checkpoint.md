# VECTOR checkpoint — F1 visual revision

Change: `replace-player-with-vector-fighter`; owner `01a15a85-0aa3-74c3-a647-a4a7e1d1d6bc`.
Branch `codex/vector-fighter`; F1 baseline `d71baf3d37d7b5e54b9a35b762637ac592fa13e1`.
The exact saved revision is recorded in the shared planning delivery record.
Not integrated. Preserve this owned worktree for ongoing visual review.

User feedback F1 (2026-10-21): thinner first-person arm, smaller armor,
no cloth overexposure, and a fighter closer to the selected VECTOR concept.
No paid services. Blender remains the editable authoring source.

Changes:
- Authored forearm width/depth .102/.112 → .077/.085 m; shared body/FPS geometry.
- Front forearm armor width .124 → .087 m, with thinner shells and smaller shoulders.
- Contoured chest, jaw surround, collar, knee insets, flank and scapula details;
  repaired back/lumbar/helmet surface intersections; packed neutral coating grain.
- Nonmetallic fully rough cloth and reduced weave normals.
- Trooper @10 `view.fillBackMeters` moves only the per-seat fill behind the camera;
  preserves light intensity/layers, camera, skeleton, grips and gameplay.
- Separate ground WalkOffset from combined ViewOffset; original airborne-zero-step
  assertion retained. Historical @9 and older profile values/hashes remain supported.

Assets and evidence:
- `scripts/vector_fighter/vector-fighter.blend`: separate meshes, rig, weights, clips.
- `model-front.png`, `model-back.png`, `helmet.png`: actual final asset studio renders.
- `f1-fps/`: final real Unity FPS frames for four weapons, plus motion.csv.
- `f1-world/`: final actual skeletal fixture poses, not an ordinary jump trajectory.
- `glb-readback.json`, `render-source.json`, `f1-source.json`: asset/source provenance.
- `verification-plan.md`: check selection, results, failures and limitations.
- `fps-walk.mp4` and `unity-fps/` are the earlier d71baf3 iteration, not F1.

Checks: final EditMode 25/25; final graphical PlayMode 5/5; seven history cases
passed in the earlier 31/32 run (its one ground-step failure fixed and rerun).
The final .25 m fill is covered by the graphical profile-binding test.

Visual status: revised model ready for review; fidelity to the selected concept is
not accepted. It still has simpler surface detail, cloth tailoring and limb plate
shapes than the illustration. Full ordinary world action sequences and human,
physical-device and performance acceptance remain open. No Player build or release.

Next step: review the new actual FPS/model images; continue targeted art refinement
if needed, then finish the full world action visual check before integration.
