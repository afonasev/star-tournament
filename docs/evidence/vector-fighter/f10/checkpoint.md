# F10 wrist/form investigation and Blender study

Owner 01a1626d-eef3-7091-8b66-b33d89833bd9; codex/vector-fighter; runtime baseline 20cce04d52da836a64e86a7ea70c367576610563.

User feedback: wrist bend too strong, needs more expressive detail, better material separation and no sharp corners. Attached wrist/edge screenshots preserved here.

Readback from existing real F9 grip CSVs (angle between elbow-wrist and wrist-middle-knuckle vectors): Rifle 8.5 degrees, Shotgun 11.3, RocketLauncher 39.7, Cutter 48.2. Analytic elbow-circle minimization with fixed shoulder/wrist/hand orientation gives 7.6, 11.0, 20.6, 24.2 respectively. This is a mathematical diagnostic, NOT an implemented correction or proof of clean elbow/body silhouette. It preserves bone lengths and exact palm/finger contact in the proposed solution; needs actual runtime witness.

Study: existing shell contour rings rounded; lower-sleeve surface relaxed; glove texture retained with moderated normals and textile/ceramic material separation. No added overlays. Studio-only neutral-pose Blender render; not a game result. Original wrist cuff still has geometric defects and unsuitable hard rings; the study is NOT finished art and must not be installed as-is. Earlier flat procedural glove material was rejected during study because it removed useful texture cues.

Recommended bounded implementation: adjust support elbow placement without moving fitted palm/digits; rebuild existing cuff surface into a continuous rounded transition; remove visible sharp/intersecting cloth contour through local topology/form repair; preserve clear rounded ceramic crown, fine woven cloth and matte flexible cuff with intentional fold/seam scale. No generic new plates/boxes or subdivision-only quality claim.

Runtime GLBs, rig, clips, profiles and code unchanged in this investigation. No Unity QA needed for this offline study; final actual four-weapon FPS remains NotRun for F10. Keep F9 and all prior continuation resources. User has not accepted the study or completed hand art.
