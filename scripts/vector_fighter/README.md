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
