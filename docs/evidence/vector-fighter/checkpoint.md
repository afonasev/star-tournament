# VECTOR checkpoint — F2 grip and shoulder revision

Change: `replace-player-with-vector-fighter`; owner `01a15a85-0aa3-74c3-a647-a4a7e1d1d6bc`.
Branch `codex/vector-fighter`; F2 baseline `41c8773d375039d92af5038590f3b2d6abf9606e`.
The final saved commit is recorded in shared planning delivery.json. Not integrated.

User feedback F2: hand does not wrap the grip; increase model detail and broaden shoulders slightly.

- Right wrist fits inward by the named Trooper @11 `grip.primaryWristYaw` parameter.
  The gun and supporting hand retain their actual sampled world poses. Previous
  additive correction is restored BEFORE graph evaluation to avoid accumulated
  or doubled offsets on channels not keyed by a clip. Existing finger curls remain.
- Broader deltoid cups, upper arms and upper chest; less pinched waist. F1 slim
  forearms, thin shells and matte cloth/fill remain. Skeleton proportions unchanged.
- Added pectoral hardware, four-slot intakes, serial markings, shoulder brackets,
  segmented belt fittings and helmet ear grille/screws.
- Existing 57-joint rig and seven actions retained. Editable Blender source preserved.

Current evidence:
- `f2-source.json`: exact GLB/Blender hashes; `render-source.json` binds studio images.
- `model-front.png`, `model-back.png`, `helmet.png`: actual F2 shipping mesh studio renders.
- `f2-fps/`: final actual Unity FPS and close-up grip captures for all four weapons.
- `f2-world/`: actual skeletal presentation states, not ordinary jump trajectory.
- `f2-diagnosis/`: original gap and visually rejected first fit preserved. Existing
  tests passed that rejected candidate; new contact/weapon-invariance check added.
- `verification-plan.md`: selected QA, results and limitations.
- F1 and earlier evidence remains historical and does not depict the latest mesh.

Visual fidelity remains open: the model still has simpler surface detail and
fabric tailoring than the concept. Full ordinary world action sequences and
human/device/performance acceptance remain open. No Player build/full suite/release.
Next: review the actual F2 model and grip; then continue art/world action review
before integration. Owned active worktree/Library retained for this continuation.

Final F2 focused results: EditMode 27/27, graphical PlayMode 6/6. Profile @10 saved-history compatibility passed. Actual grip test covers aim/fire and unchanged weapon/support hand; scene tests cover frozen samples, walking, airborne states, identity and death/respawn/repeat. These checks are not full gameplay or human acceptance.
