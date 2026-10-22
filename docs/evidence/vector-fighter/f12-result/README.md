# F12 — stable left support arm through grip

User correction to F11: keep the left arm in approximately the same position and correct the abrupt, unnatural wrist transition through the weapon grip.

Baseline: `codex/vector-fighter` / `610fef5bada4c7afeb9724197a4212a4379b7922`. Final source is identified by the containing Git commit and SHA-256 hashes in `verification.json`.

Factory trooper profile @18 changes RocketLauncher and Cutter palm contacts, pitch and yaw; both use wristAlignment=0. Rifle/Shotgun remain unchanged. No mesh, animation array, finger curl, camera, weapon transform or gameplay edits. The earlier elbow swivel is retained as an optional Lab control and in exact historical @17 snapshots.

Review `fps-four-weapons.png`, `before-after.png`, and raw `frames/*-fps.png`. Montages only crop and resize actual Editor Play Mode captures. `support-grips.png` shows both sides of all four support grips; full-resolution closeups are in `frames/`. These are native fixture captures, not a Player build or human acceptance.

`measurements.json` records angles from captured bones in weapon coordinates. The spread is the largest pairwise angle between elbow-to-wrist vectors across four weapons; it is not a dynamic switching-performance measurement. Wrist angle is between elbow-to-wrist and wrist-to-middle-knuckle vectors.

Focused checks and startup failures are retained in `verification.json` and `qa/`. The broader F5 suit refinement, proximal sleeve/elbow intersections, all-motion art review and human acceptance remain open. No full suite, build, merge, release or deployment was performed. Worktree, Library and all previous editable sources/evidence are retained for authorized continuation.

Final measured result: pairwise forearm spread 38.729° → 8.143°; RocketLauncher wrist 20.626° → 3.896°; Cutter wrist 24.171° → 3.811°. Rifle 8.470° and Shotgun 11.270° unchanged. Native4/4 PASS (48.8594s), history3/3 PASS (228.1896s).
