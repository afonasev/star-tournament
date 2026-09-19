# 12 — Add local light shadows

> Статус: CLOSED — остаток работ CANCELLED / SUPERSEDED переходом на Unity, 2026-09-19, по явному решению пользователя. Исторические реализации и результаты ниже сохранены; незавершённая приёмка не объявляется успешной. Не возобновлять browser-разработку или интеграцию по этому handoff. Unity получает новую реализацию и тесты. См. `../../BROWSER_BACKLOG_CLOSURE.md`.

- Change: `add-local-light-shadows`.
- Worktree: `/Users/eaafonasev/.codex/worktrees/3863/star-tournament`.
- Branch: `codex/add-local-light-shadows`; base `a6444c5`.
- Scope: renderer-only local lamp shadows with `Low`/`Balanced`/`High`/`Ultra` budgets 0/1/2/4, a darkened profile-owned arena fill and a regular visible lamp rhythm across renderer wall surfaces.
- Checks: `npm test` (346), `npm run build`, strict OpenSpec validation; production browser performance gate passed for all four quality presets after the current rebuild.
- Repair: wall shell and wall presentation neither cast nor receive local shadows, removing the point-light temporal receiver path. Every GLB wall-bay is a thin exterior facade separated from the opaque canonical shell by 4 cm, eliminating depth competition while collision continues to use the unchanged shell. Collision-only gap seals no longer create WebGL meshes. Portal facade GLBs use only contracted structural segments, avoiding intersections with their unchanged full collision proxies. Shadows are restricted to the stable floor receiver; the profile radius is 0.5 for a crisper contact edge. Every renderer wall gets a lamp housing, emissive lens and ceiling strip; at most 16 local lights and 0/1/2/4 shadow casters are active by quality tier.
- Visual evidence: [Ultra PNG](../../../../visualizations/2026/09/06/01a07568-c1d3-7eb2-963c-d98d9a36e93e/wall-bay-front-clearance-ultra.png), seed `1398030674`. The in-app Browser opens the scene, but rejects pointer lock; it does not prove physical keyboard/mouse movement.
- Stand: `http://127.0.0.1:5182/?muted=1` (HTTP 200 checked 2026-09-06).
- Open: visual/physical acceptance of free camera movement around lamp fixtures. Do not merge main, archive or deploy in this task.
