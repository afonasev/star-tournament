# 12 — Add local light shadows

- Change: `add-local-light-shadows`.
- Worktree: `/Users/eaafonasev/.codex/worktrees/3863/star-tournament`.
- Branch: `codex/add-local-light-shadows`; base `a6444c5`.
- Scope: renderer-only local lamp shadows with `Low`/`Balanced`/`High`/`Ultra` budgets 0/1/2/4, validated presentation-profile controls and performance report evidence.
- Checks: `npm test` (346), `npm run build`, strict OpenSpec validation; production browser performance gate passed for all four quality presets and again on Ultra after the visual repair.
- Repair: canonical arena shell now only receives local shadows, preventing self-shadow flicker; each lamp fixture has an untonemapped emissive lens driven by the profile's local-light intensity.
- Visual evidence: [Ultra PNG](../../../visualizations/2026/09/06/01a07568-c1d3-7eb2-963c-d98d9a36e93e/local-lights-repaired-ultra.png). The in-app Browser opens the scene, but rejects pointer lock; it does not prove physical keyboard/mouse movement.
- Stand: `http://127.0.0.1:5182/?muted=1` (HTTP 200 checked 2026-09-06).
- Open: visual/physical acceptance of free camera movement around lamp fixtures. Do not merge main, archive or deploy in this task.
