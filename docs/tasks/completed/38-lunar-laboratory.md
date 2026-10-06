# 38 — Лунная лаборатория

Canonical change: /Users/eaafonasev/Projects/star-tournament-planning/openspec/changes/add-lunar-laboratory-map
Owner: 01a1013f-f859-7b42-814f-388d36ecbf22
Branch/worktree: codex/add-lunar-laboratory-map / /Users/eaafonasev/Projects/star-tournament-moon-lab
Baseline: 817997efcca0998b8e9cd93c78cfd13b4087c3f0

Approved v4 geometry, style 2, four zone motifs and lunar wastes/mountains/Earth/Sun. Full scope includes an actual local macOS Player; no external publication. Existing maps and shared movement/combat/pickup rules remain intact. Full QA selected. Human acceptance separate.

Evidence: docs/evidence/lunar-laboratory and .local/unity-evidence. Initial ENOSPC recovered after user freed disk and requested continuation. See canonical delivery record for current stage.

Implementation includes authored collision/navigation, third-map selection, renderer-only textured kit/background, canonical optical glass queries, and Lab profile-history compatibility.

Focused PlayMode XPXJjX passed 2/2 (all anchors, 36 stair traversals, both storage portals, broken-window and balcony jumps, glass sight/shot masks). Intermediate Player v3 completed natural 8-hard-bot FFA, seed 20261003: time-limit, 157 kills, 140 pickups. Full gate and final shader/Player verification pending; see canonical record before using a candidate.

Final automated slice: make check passed (309 EditMode, 187 PlayMode; zero skipped/failed), then presentation-only sign placement/camera corrections passed 2 focused PlayMode tests and rebuilt. Final native offscreen candidate: PASS, natural FFA 8 hard bots, 157 kills / 140 collections, 17 screenshots. Source digest and reports are in docs/evidence/lunar-laboratory.

Remaining gate Q2: Mac is locked; visible menu/HUD smoke requires manual unlock. No merge, spec sync, technical finalization or human acceptance claimed. Keep owned worktree/branch and candidate for this continuation. Next: launch exact retained candidate through unity-run exclusive with -lunarReview -lunarVisualOnly; inspect menu/HUD frames, resolve Q2, then finish normal integration/spec sync/finalization.

2026-10-03 continuation: exact retained Player visible smoke passed after user unlocked Mac; menu, single/four-view HUD inspected, 18 PNG retained in docs/evidence/lunar-laboratory/visible. Q2 resolved. Runtime source and binary unchanged; integration/finalization in canonical delivery record. Human acceptance remains pending.
