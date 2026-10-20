# VECTOR checkpoint — implementation remains open

Change: `replace-player-with-vector-fighter`.
Owner: `01a15a85-0aa3-74c3-a647-a4a7e1d1d6bc`.
Branch: `codex/vector-fighter`, baseline `00fbd33209eb755dd5f86499aa8ea12867a2efd0`.
The exact saved revision is recorded in the shared planning delivery record.
Not integrated. Keep this owned worktree for the remaining art work.

The user selected concept 01 VECTOR, authorized implementation and all existing
actions/FPS arms, and excluded paid services. Blender is the local authoring route.
No external generator produced a model or received a paid job.

Editable source: `scripts/vector_fighter/vector-fighter.blend`, with separate
armor objects, rig, weights and clips. The builder is reproducible from pinned
Git source blobs and generates the Unity body/FPS GLBs and manifest hashes.
Retained rig, clips and gloves remain attributed.

Technical implementation:

- Compatible 57-bone skeleton, seven persisted clips and existing weapon mounts.
- New body/FPS geometry; verified identical shared meshes and material ownership.
- Session-clock symmetric airborne tuck, landing compression and FPS offset.
- Existing passive ragdoll, motor, damage and input remain in control.
- Closed sleeve geometry and upper-arm fabric continuation in FPS.

Evidence:

- `editmode-final-pass.xml`: 15/15 import and weapon-switch checks.
- `editmode-21-pass.xml`: earlier 21/21 including six unchanged history migration
  cases; final asset checks were rerun separately after geometry changes.
- `airborne-ragdoll-final-pass.xml`: 2/2, with real symmetric airborne pose enabled.
- `playmode-final-pass.xml`: 5/5, color coverage/ownership, FPS walking, shared
  assets and airborne/reset. No full suite was run.
- `fps-walk.mp4`: 264 actual Unity frames, 1600×900, 50 fps, silent, 5.28 seconds.
  Rifle → Shotgun → RocketLauncher → Cutter; walking, two-seat presentation.
- `unity-poses/`: actual rendered skeletal fixture poses, not an ordinary
  player-controlled jump trajectory.
- `render-source.json` and `glb-readback.json`: exact studio-render and asset hashes.
- Earlier FAIL and disk-full incomplete evidence are retained. Diagnostic
  zero-amplitude ablation was restored; the final source uses the real pose.

**Visual gate: FAIL / unfinished art.** The real model remains substantially
simpler than `approved-vector-concept.png`. Visible gaps: helmet surface detail
and cheek/crown shapes; simple chest and limb plates; bulky shoulder transitions;
overbright cloth near the FPS camera. Do not present concept/reference images
as implementation or call the model maximally close to the selected design.

Next step: refine these shapes and materials in Blender while preserving the
validated rig and material roles; inspect real renders and then affected Unity
states. Full world action sequences and human visual acceptance remain open.
Player, release/deploy, physical-device and performance acceptance were not run.

Foreign main-checkout files were not edited. Unrelated Unity-generated settings
and scene changes were removed from this worktree's diff.
The shared planning change remains `implementing`; no merge, acceptance or
archive is claimed.
