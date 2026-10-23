# F14 — fingers, glove continuity and approved Shotgun clearance

Baseline: `codex/vector-fighter` / `cdddf3717ec750dbb0865046674550bb96f2de36`; fixed-arm F13 baseline `0d67d93c916d06559d8cd2c544da8221c284fadb`.

User requested reworked fingers/grips with fixed arm placement, self-review of both hands on all four weapons, then closeups. Q5 explicitly approved reducing only the demonstrated Shotgun palm swell. This is an Editor review candidate; human art acceptance remains pending.

The glove now has phalanx-aligned sections, rounded tips, continuous metacarpal connections, a blended thumb web and curved closed shields. The generator asserts exactly two connected cloth surfaces. Factory @20 records the reviewed per-digit poses. Exact @19/@18 defaults and ranges remain reconstructable for saved Lab histories. All shoulder, elbow, wrist, weapon-mount and camera settings remain unchanged.

The Shotgun palm swell alone changes: 115 weapon vertices; all weapon triangle indices and other material primitive positions are exact. GLB hierarchy and all seven animation arrays are exact versus F13. See `qa/glb-verification.json`, `qa/weapon-change-audit.json` and `qa/fixed-arm-csv.json`.

## Review

`final/` contains actual Unity factory20 frames without grip overrides: 20 movement frames and 24 closeups (two hands, three angles, four weapons). Four `*-closeups.png` figures place the original side views next to each other without altering their pixels. The third angle is an underside diagnostic. FPS lighting is unchanged; closeups have the existing low-intensity diagnostic fill.

Self-review checked finger direction, closed tips, shield continuity, thumb opposition and visible contact for Rifle, Shotgun, RocketLauncher and Cutter. The Shotgun index/middle were adjusted to clear the receiver edge after the swell reduction. The side and underside views provide the clearest contacts; some reverse views are occluded by forearms. Neutral GLB-pose renders were used to expose cloth folds hidden by dark materials. These diagnostic renders are not native gameplay evidence. Remaining glove creases and the large authored palm proportions are visible in the native closeups; the user's aesthetic decision is still open.

## Checks and limitations

- Factory20 PlayMode: 5/5 (arm/mount invariance, preserved F13 thumb-only contract, frozen-pose accumulation, material identity, four-weapon capture).
- Factory20 EditMode: 5/5 (three import contracts and exact @19/@18 history migration cases).
- Final mesh-only palm adjustment: native capture 1/1 and import 3/3 PASS; see final-geometry XML results.
- Recorded arm positions match study3 exactly to six decimal places for all six arm bones and all four weapons; the PlayMode test separately checks rotations and weapon mounting.
- Full suite, Player, device, audio, performance, integration, release and deploy: NotRun. No human acceptance inferred.

Rejected evidence is retained: study4 numerical fit failed visual anatomy; studies5/6 exposed shield holes and palm folds; study7 shortened palm disconnected fingers. Export12 restored continuous metacarpal overlap; export13 restores fuller palm coverage while retaining the smooth skin transition. Nearest-normal signed-distance diagnostics are not a collision certificate for intersecting weapon components. The first offline Blender-bone preview used an incorrect basis and is invalid; the later GLB inverse-bind preview is diagnostic only.

All `.local/vector-*`, existing bytecode cache, source Blender files, Library and the worktree are retained by direct user instruction for continuation/review. No foreign resources are changed. Unity serializer-only whitespace is restored after runs. Broader costume F5 work is outside this bounded correction and remains open in shared planning.
