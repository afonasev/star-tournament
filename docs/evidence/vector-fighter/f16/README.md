# F16 — retained Orbital gloves on VECTOR

User selected the former Orbital/Strata hands and asked to inspect both the hand model and full player. Baseline: codex/vector-fighter / a095e6ef17c9cb9e5e373101e3fcb27896567716. F15 procedural anatomy is superseded, not accepted.

The generator reads pinned Orbital arms from 00fbd33209eb755dd5f86499aa8ea12867a2efd0 and the retained F15 editable suit. Original anatomical cloth vertex positions, UVs, textured material and skin are retained. Old sleeve geometry behind the wrist is excluded to fit VECTOR cuffs; separate raised thread islands are omitted because they stretch between independently posed digits; the stitching in the original texture remains. Distinct Orbital material names prevent the legacy prefab suffix resolver from substituting the glove texture for VECTOR sleeve fabric. Existing VECTOR forearms/body, rig/actions, attachment transforms and approved Shotgun trim remain. Both FPS and world GLBs share the same glove source. Existing ART_LOLL attribution remains applicable: docs/evidence/trooper-animation-pipeline/ATTRIBUTION.md.

Editable source: scripts/vector_fighter/f16-orbital-hands.blend. Reproduction: Blender --background --python scripts/vector_fighter/restore_orbital_gloves.py, then qa/import.sh. Scripts write candidates before import. Source inputs are immutable Git blobs, not a previous generated output.

## Review

review.html contains actual Unity FPS, both-hand three-angle captures and front/side/back full-player views for all four weapons, followed by fire/rising/falling/landing phases. World screenshots use an isolated diagnostic camera and fill light on the actual actor; they are not Blender concepts. Composite sheets only resize/place originals. Human art acceptance remains open.

## Verification and retained failures

Final affected PlayMode **6/6 PASS**, EditMode import **3/3 PASS**. Participant material ownership passed on the preceding material-equivalent candidate. See qa/scope.md and qa/verification.json. Visual findings and remaining plate/costume issues: review-notes.md. Exact GLB hierarchy, seven animation arrays and approved weapon positions/normals/indices are compared to F15. A separate editable-source audit checks all 45 unchanged non-glove mesh objects, including UVs and skin weights.

Direct-transfer candidate showed old sleeve overlap and stretched raised stitches. Its native run was stopped during disk exhaustion; no final XML, so it is incomplete/NotRun, not PASS. Original captures and losslessly compressed mesh dumps remain in .local/vector-f16/initial-direct-transfer. The final run omits optional heavy geometry dumps; no assertions, timeout or seed were changed. A subsequent native run passed 6/7 but its new world capture used the wrong actor path; the failed XML is retained. The next revision uses the existing player-1/trooper-presentation path. Two early preservation audit harness errors (aliased library-load list, then unevaluated library-object transforms) are retained; the corrected audit opens/evaluates each source scene before comparing it.

No full suite, Player build, release, deploy, physical-device or performance test. All prior source files, Library and .local/vector-* retained. No integration performed. Remaining full-costume F5 art work and human review are separate.

The material-corrected raised-thread candidate passed PlayMode7/7 and import3/3 but still showed stray geometric threads in closeups. Final export removes only those extra thread islands and reruns the six affected mesh/pose cases plus import3. Material-ownership PASS is retained from the preceding candidate; its remaining material definitions and runtime logic are unchanged.
