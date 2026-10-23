# F14 work in progress — NOT an accepted correction

User explicitly rejected F13 fingers and requested: redo fingers and grip normally, keep arms fixed, self-review anatomy/contact on all four weapons, then show closeups.

Baseline `codex/vector-fighter` / `0d67d93c916d06559d8cd2c544da8221c284fadb`. This checkpoint preserves rejected/intermediate work for autonomous continuation; it is not a completion or visual PASS.

New `refine_finger_phalanges.py` loads preserved F11 source, builds continuous phalange shafts, flatter pads and rounded caps, thenar volume, and compact per-phalanx ceramic/chassis plates. `f14-hand-anatomy.blend` is editable unjoined source. The GLBs in this checkpoint are study3 geometry. Factory profile is STILL @19: candidate grip values exist ONLY in `qa/study-grip-overrides.txt` and were used in study captures. Do not describe runtime defaults as finalized.

Three native capture tests each1/1 PASS, but visual acceptance FAIL/unfinished. Study1 had bad terminal cap rings and excessive diagnostic light; corrected in study2. Study2 revealed oversized original finger armor and contact errors. Study3 replaces those plates. The geometry and poses still need proper review/refinement. Diagnostic closeups have a .15 point fill; normal FPS lighting unchanged.

Known discrepancies: Shotgun right fingers penetrate the actual grip; Cutter right little finger has a gap; left Rifle fingers intersect receiver/underframe; some finger shields remain offset; right thumb/web silhouette remains questionable. Do NOT repeat F13 mistake of treating smoke PASS as anatomy PASS.

`qa/study-2-contact-review.json` is a nearest-surface signed-normal diagnostic, not a robust collision certificate (overlapping parts can affect signs). It exposed large errors missed by approximate boxes. Next approach: use the actual weapon surface instead of the simple box fitter; `.local/vector-f14/surface/` holds measured distance grids for all four weapons, built by `distance-field.py`. Preserve joint bend limits/planes and fixed wrist/forearm transforms. Venv `.local/vector-f6/venv/bin/python` has numpy/scipy.

All sources, .local/vector-* resources, original bytecode cache, Library and worktree remain retained by explicit user request. No full tests/Player/build/merge/deploy. All owned Unity/Blender commands have completed. Broad F5 art scope remains outside this correction.
