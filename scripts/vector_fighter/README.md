# Current F17 assault model

Selected direction: concept 1, 2026-10-24; subsequent user refinement paints the complete pectoral chest plate in participant/team color. `f17-assault.blend` is the editable world source, reproducibly derived from immutable F16:

```sh
blender -b -noaudio --python scripts/vector_fighter/author_assault.py
blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant balanced --promote
python3 scripts/vector_fighter/audit_assault.py --output .local/vector-assault/audit.json
```

First-person geometry still starts from F16; only the neutral/identity material partition changes. Glove anatomy, named skeleton, weapon and image bytes are exact. Offline asset dimensions in author_assault.py are the selected sculpt proportions, not new gameplay/Balance Lab controls. The exporter writes the authored neutral glTF baseColorFactor explicitly because Blender drops the linked MixRGB constant; it shares the original texture bytes instead of baking new maps. Paint seams are cut again after mesh collapse so the final color boundaries follow continuous edges. This adds a small amount of topology within the original world/view caps.

The historical audit_runtime.py proves the old optimization-only contract at its original source revision. It deliberately retains material/source assertions; use audit_assault.py for F17, which permits only the two specified neutral material additions with exact maps/factors and retains strict compatibility/geometry budgets.

Historical source notes follow; early builders do not rebuild F17.

# VECTOR fighter — editable sources and runtime meshes

The accepted visual source is `f16-orbital-hands.blend`; current runtime assets are
derived by `optimize_runtime.py` as documented below. Retain this detailed source
and the established rig, gloves and grips. The original visual target is
`docs/evidence/vector-fighter/approved-vector-concept.png`.

The early builders and F1–F15 notes below are historical studies. Their rough or
unaccepted status does not describe the accepted F16 source. Do not replace the
shipping model by rerunning an early builder alone.

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

The checked-in `vector-fighter.blend` is the earlier editable snapshot. The builder saves `.local/vector-fighter/vector-fighter.blend` with separate armor
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

## Runtime mesh optimization (2026-10-24)

The accepted F16 source is `f16-orbital-hands.blend`. Keep this editable high-poly
source intact. The original optimization derived independent world and first-person
meshes from it; the original glove vertices are retained in first-person. This
avoids using the expensive first-person sleeves/gloves on every bot. Materials,
embedded textures, bind skeleton, weapon meshes and Unity's persisted clips remain
unchanged. No distance-based LOD or runtime camera/quality setting is introduced.

```sh
blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant conservative
blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant balanced
# Only after reviewing the generated candidates:
blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant balanced --promote
```

Candidates and per-object surface-error/count reports go to
`.local/vector-runtime/<variant>/`. `--promote` updates shipping GLBs and their
manifest hashes; it does not publish a release. After promotion run
`TrooperPreparation.RefreshExistingScene` through the Unity wrapper to regenerate
only the two persisted prefabs while retaining the accepted animation clips.
World and view meshes deliberately have distinct mesh names and resolutions;
sharing unequal same-name skins is forbidden.

For the optional `VectorMeshOptimizationReviewTests` graphical comparison, copy
each generated candidate pair to `Assets/StarTournament/Art/RuntimeMeshReview/`
as `<variant>-body.glb` and `<variant>-arms.glb`. Use an owned worktree, set an
absolute `STAR_TOURNAMENT_MESH_EVIDENCE`, run the explicit review through unity-run
with graphics and `-noaudio -muted`, and choose exclusive for timings. Remove
these temporary review imports after preserving the report/images. The fixture
remaps per-slot identity property blocks with the material roles; merely swapping
meshes can otherwise produce misleading colors. Frozen timing is diagnostic;
Windows Player/human acceptance remains separate.

`python3 scripts/vector_fighter/audit_runtime.py --output .local/vector-runtime/audit.json`
checks the actual exports without Unity: budgets, manifest hashes, named bind
matrices, normalized weights, material/texture identity, untouched weapon and
unchanged first-person glove triangles with UVs/normals/named skin weights. The
reference defaults to the last pre-optimization source commit. It also verifies
that the editable high-poly source remains byte-identical to that revision.

Set `STAR_TOURNAMENT_MESH_SHIPPING_ONLY=1` for a graphical capture of the actual
current prefabs without candidate imports or timing loops. That mode supports
unity-run shared; measured multi-variant comparison uses exclusive.

## Restored shotgun surface maps (2026-10-25)
After exporting the final runtime GLBs, run `python3 tools/restore-shotgun-surfaces.py`. This append-only pass reuses the original 128px shotgun metal map from double-barrel-shotgun-lod0.glb on vector-chassis/vector-titanium. It preserves all existing geometry, UV, skin, clip and image bytes, shares one new texture between the two materials per file, and updates asset-manifest hashes. Existing ceramic/glove maps remain intact.
