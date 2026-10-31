# Vector grip F01

Factory profile 23 fits each finger independently, preserves custom Lab values and
reconstructs the exact previous registry for saved hashes. `OrbitalGripPose` moves
the right elbow to align the wrist while keeping the weapon world pose.
The authoring scene's Shotgun contact values agree with the factory.

`repair_glove_weights.py --ref e690e9515c02ebb1988862ea16e0f01e994ca3db`
(requires numpy/scipy) reproduces the small surface repair from Git source GLBs.
It seats existing raised stitch strips within 0.35 mm of the anatomical textile,
transfers its barycentric weights, and appends accessors. Digit-pad assemblies
keep their original rigid skin and shape and move 2 mm (4 mm for the thumb)
outward as complete layers to clear glove folds. It preserves all original
binary payload, anatomical cloth and other armor geometry, triangle counts,
UVs, materials, textures,
binds, clips and the detailed weapon. No strip or weapon detail is deleted.
Use an unpatched input revision; the command above records the exact source.
The input GLBs are the editable source for this weight/surface patch; the existing
Blender weapon and character sources remain available and unchanged.

Native evidence and failed iterations live once in the shared planning evidence
for `refine-vector-shotgun`, F01. Offline fitting is an authoring aid, never a
substitute for native multi-angle review or human acceptance.
