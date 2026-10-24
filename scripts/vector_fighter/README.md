# VECTOR fighter — work in progress

The selected visual target is `docs/evidence/vector-fighter/approved-vector-concept.png`.
The current mesh is a rough implementation, not accepted art. See the verification
record in that directory for the remaining shape, detail and gameplay gates.

This local Blender pipeline preserves the existing compatible skeleton, authored
clips, weapon mount hierarchy and articulated glove derivative. The suit and closed
helmet are new geometry. Retained content is attributed in
`unity/Assets/StarTournament/Art/trooper-ATTRIBUTION.md`.

With Blender on PATH:

```sh
blender -b -noaudio --python scripts/vector_fighter/build.py -- --candidate
blender -b -noaudio --python scripts/vector_fighter/review.py -- --candidate
```

Candidate mode writes only `.local/vector-fighter/`. Without `--candidate`, the
builder replaces the two Unity GLBs and updates their manifest hashes. Do not run
that export while Unity is using this worktree.

The checked-in `vector-fighter.blend` is the editable snapshot. The builder saves `.local/vector-fighter/vector-fighter.blend` with separate armor
objects, skin weights, materials and the compatible animation rig before batching
render meshes. `--source-only` saves this editable source without changing GLBs.
All original source inputs are pinned Git blobs from
`00fbd33209eb755dd5f86499aa8ea12867a2efd0`; rebuilding never imports its own output.

`review.py` imports the actual GLB and renders front/back/helmet views using a
display-only teal override. `render-source.json` records the exact mesh hash.
These studio renders do not validate Unity rendering or gameplay.

The new runtime airborne and landing poses are presentation-only additions; the
existing physics-driven death remains authoritative for corpses. Focused Unity
checks and their results are recorded in the evidence directory.

F1 (2026-10-21) uses slimmer authored arm dimensions, thinner ceramic shells,
a contoured breastplate, revised jaw/shoulder/knee/back panels and packed coating grain.
The cloth is nonmetallic, fully rough, with a reduced normal-map amplitude.
Unity Trooper profile @10 places its existing per-seat equipment fill behind the
camera (`view.fillBackMeters`) to avoid near-field sleeve overexposure. This does
not scale or move the camera, weapon grips or skeleton. The earlier @9 profile
can still be reconstructed by the history registry.

F2 broadens the shoulder cups, upper arms and upper torso while retaining the F1
forearm dimensions/material. Chest and belt hardware, intake slots, engraved
markings and ear grille are authored geometry. Trooper @11 adds
`grip.primaryWristYaw`: the right wrist fits inward, but the sampled weapon world
pose is preserved. Restore the previous additive correction before graph sampling
so clips without wrist/mount channels remain idempotent. @10 history remains
reconstructible through BeforePrimaryGripFit.


F4/F5 recovery and rejected visual route (2026-10-21): `--details tailored`
and `--details technical` are development studies, not accepted detail upgrades.
After provisionally choosing technical armor, the user rejected the primitive
construction itself. Do not ship these attached service details.

`--candidate --forms planar` reconstructs garment continuity and new shell
cross-sections; `primary-form-study.blend` is its editable, **unaccepted** study.
This is still too schematic and is NOT the shipping model. Its real GLB renders
are in `docs/evidence/vector-fighter/f5-surface-study/`. The production source
`vector-fighter.blend` and shipping GLBs remain unchanged by these studies.
The remaining model work is primary form/art quality against the approved
VECTOR reference, not adding more objects or subdividing existing parts.

`--candidate --forms authored-cloth` is a diagnostic of retained source clothing.
It still includes source vest/pad geometry and visibly intersects the new shells;
it is NOT a proposed finished model. Do not use it as production or acceptance
 evidence. A clean authored garment extraction and proper surface remodeling
remain necessary; automated overlaying is not a completed substitute.

F6 (2026-10-22): `--candidate --forms remodel` extracts pinned sewn sleeve/trouser
components and authors new torso/shell surfaces. `f6-remodel-study.blend` is an
unaccepted study, NOT the production source. Its full-body renders still show
cloth/armor intersections and insufficient concept fidelity; production export
remains blocked by the candidate assertion. Do not promote it automatically.

`fit_grip_surface.py CAPTURE_DIR OUTPUT.txt` fits per-digit overrides against
Unity-baked glove vertices and measured rectangular support-grip bounds using
NumPy/SciPy. It is an offline art aid, not a mesh collision proof or execution test.
Its outputs require actual Unity capture review. Actual weapon triangle OBJ and
skin/axes data come from the optional CaptureGrip evidence path.

F7 repairs the existing F6 costume with `blender -b -noaudio --python
scripts/vector_fighter/refine_pose.py`. It creates no additional costume objects:
rigid panels retain their designed surfaces, covered cloth follows those panels,
and existing glove seam islands receive cloth-interpolated skin weights.
`f7-pose-repair.blend` is the editable source; real exported-model renders and
native execution results are recorded in `docs/evidence/vector-fighter/f7/`.
The body gait uses actual root displacement and planted world-space support feet;
only the visual pelvis/legs move, never the simulation actor or camera.

F8 repairs the preserved F7 glove with `blender -b -noaudio --python
scripts/vector_fighter/refine_glove.py`. Finger cloth follows its own anatomical
chain, existing seam islands conform to that cloth, and existing finger/palm
plate layers are seated on the garment. Each dorsal rim/coating pair is fitted
as one existing assembly with a narrower footprint and preserved layer spacing.
Object, vertex, bone and animation
counts are preserved. Editable source: `f8-glove-repair.blend`; diagnostic
exports go to `.local/vector-f8/`. This script does not promote GLBs or factory
parameters automatically. Trooper @16 supplies separately authored closed
RocketLauncher/Cutter grips; `BeforeClosedVerticalGrips` preserves exact @15
factory defaults for saved history. Actual Unity evidence and remaining visual
gates are recorded in `docs/evidence/vector-fighter/f8/`.
