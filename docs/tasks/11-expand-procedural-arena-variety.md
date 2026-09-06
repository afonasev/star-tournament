# 11 — Expand procedural arena variety

- Change: `expand-procedural-arena-variety`.
- Worktree: `/Users/eaafonasev/.codex/worktrees/4f74/star-tournament`.
- Branch: `codex/expand-procedural-arena-variety`; base `7589f94`.
- Scope: approved recipe graph, elevations/ramps, surface query masks, window/barrier/relief presentation, local manifest lamps, profile and legacy compatibility.
- Implementation and automated checks ready for review. Evidence: [актуальный отчёт исправления](../qa/expand-procedural-arena-variety/repair/README.md).
- Open: OpenSpec 5.3, physical keyboard/mouse acceptance. In-app Browser rejects pointer lock; ordinary match initializes but remains at 05:00. Manual checklist: medium seed 1 and large seed 3; walk both ramp directions, collide with low/high barriers, shoot through barriers and relief, confirm windows block shots, inspect route readability.
- Stand: `http://127.0.0.1:5180/?muted=1` (HTTP 200 checked 2026-09-06).
- Keep this handoff active until physical acceptance. Do not merge main, archive or deploy in this task.

2026-09-06: после пользовательского playtest исправлены масштаб wall-bay, canonical portal solids и UV/ORM coverage. Начать новый матч с `arena-recipes-v4`; medium seed 1 имеет hash suffix `aeb3500d`. Проверить стойки, дуги, стеновые панели и фактуру пола.
