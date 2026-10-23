# F16 affected QA
Baseline codex/vector-fighter / a095e6ef17c9cb9e5e373101e3fcb27896567716.
Only restore pinned Orbital gloves with original geometry, UV, textures and weights; preserve rig, animation, arm/weapon placement, VECTOR forearms/body, approved Shotgun trim. Add optional world-model capture to existing native witness; no assertion/timeout/seed changes.
Dependencies: GLB import, skinned mesh deformation, material ownership, FPS/world representation.
Commands: import.sh then checks.sh. Shared Unity wrapper, -noaudio -muted from startup. PlayMode seven focused cases: four-weapon gait capture plus fixed-arm invariant, four per-weapon fire/jump phase witnesses, participant material ownership. EditMode TrooperImportTests (3). No gameplay changes, no full suite, Player/build/release/deploy. Native four-angle full-body captures per weapon accompany both-hand closeups.
Exact offline audit compares hierarchy, all seven animation arrays and weapon geometry against baseline. Source extraction checks bind skeleton and leaves retained glove vertices unmodified.

Initial direct-transfer native run interrupted after visible old-sleeve overlap/stretched stitching and disk exhaustion. No XML, NotRun/incomplete; raw geometry losslessly gzip-compressed and verified. Final capture omits optional heavy mesh dumps; all tests/assertions unchanged. Narrow adaptation removes old sleeve behind wrist and transfers only raised stitch weights from retained original cloth.

Material alias and world-camera path fixed; seven PlayMode cases and three import cases passed on raised-thread candidate. Visual review still showed thread islands spanning digits, so final geometry omits only separate raised thread islands; baked original stitches stay. Final affected rerun: six mesh/pose cases + import3; participant material ownership PASS retained because removing thread geometry changes no material ownership code or remaining material values.
