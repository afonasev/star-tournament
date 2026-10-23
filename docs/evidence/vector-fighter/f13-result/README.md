# F13 — anatomical gloves and grip, fixed F12 arm placement

Baseline `codex/vector-fighter` / `b55259a5a01b709b3a6260a9d54d163cbe149937`. User asked to correct anatomy and grip while retaining arm placement, and show all four weapons.

The final candidate rebuilds the neutral glove textile as a closed continuous volume around the unchanged skeleton: tapered palm sections, thenar volume, finger phalanges and rounded tips. Neighboring skin segments blend at the joints; the palm follows Hand. Existing ceramic/chassis details remain. Detached inherited stitch-strip geometry was removed because it tore over the reconstructed web. Source: `scripts/vector_fighter/refine_hand_anatomy.py` and editable `f13-hand-anatomy.blend`, derived from preserved F11 source.

Factory profile @19 changes right-thumb joint defaults only. Exact @18 and previous saved Lab snapshots remain reconstructible. Upper/lower arms, wrists, left support contacts, camera, weapons, gait and gameplay are unchanged. Binary verification compares rig hierarchy and all seven sampled animation arrays to F12.

`fps-four-weapons.png` and `frames/*-fps.png` are actual Unity Editor Play Mode renders. `hand-closeups.png` contains both sides of both hands for every weapon. `rifle-before-after.png` uses the same crop/scale from F12 and F13 actual captures. No retouching. Blender rest preview is a studio diagnostic only.

Focused evidence and rejected studies are recorded in `verification.json` and `qa/`. Full tests, Player build, device/performance acceptance, merge, release and deployment were not run. Broader F5 suit refinement remains outside this correction; final art acceptance remains human.
