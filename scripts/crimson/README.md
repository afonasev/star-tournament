# Crimson shoulder launcher

Selected Crimson #1: stepped red shell, open graphite keel, diagonal titanium inlay and rear shoulder saddle. The editable packed source is `crimson-master.blend`; deterministic authoring is `build.py`. Geometry uses the existing Pulse semantic attachment frame. The projectile and effects are unchanged.

Build with Blender 5.2.1: `Blender -b -noaudio --python scripts/crimson/build.py`.
Render the actual exported GLB with `--python scripts/crimson/render.py`. Run with `-- --batch-audit` to compare the same source before/after lossless material joining. Temporary results go in `.local/crimson/`.

The authoring helpers come from the pinned repository file `scripts/pulse_mk2/build.py`; its original model generation is not executed. No network dependencies.

Optimization: 101 editable mesh objects become four material batches, retaining all 25,920 triangles. Four materials use eight 512x512 maps. `optimize.py` evaluates exact vertex welding and index locality without changing the shipped GLB unless explicitly passed `--promote`; the current candidate brings no size/vertex reduction and was not promoted. The existing LOD1 compatibility key retains the same silhouette; Pulse presentation currently does not activate an LODGroup. No frame-time improvement is asserted. No new collider or simulation logic.

Native Unity evidence, checks and acceptance scope: shared planning `docs/evidence/redesign-pulse-crimson-shoulder` and `openspec/changes/redesign-pulse-crimson-shoulder/acceptance.md`.
