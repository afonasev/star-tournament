# Historical snapshot inventory

Star Tournament is Unity-only as of `31dfd44` (2026-09-21). The only
executable game in this checkout is [`unity/`](../unity/). Nothing in this
inventory is a second runtime, a deployment input, or an outstanding browser
acceptance obligation.

## Retained for reproducibility

| Location | Contents | Status and handling |
| --- | --- | --- |
| `openspec/` | Frozen copy of historical OpenSpec changes, specs, archives and config. | Keep unchanged. The canonical live planning store is `/Users/eaafonasev/Projects/star-tournament-planning` (`star-tournament-planning`); do not create or update changes here. |
| `docs/evidence/`, `docs/qa/`, `docs/AI.md` | Browser-era screenshots, measurements, AI notes and later Unity evidence. | Preserve as evidence/reference. Browser material is not executable proof for Unity. |
| `docs/tasks/completed/`, `docs/tasks/cancelled/`, `docs/BROWSER_BACKLOG_CLOSURE.md` | Completed, cancelled and superseded handoffs, including the browser backlog decision. | Preserve their recorded result and status; do not reopen cancelled browser work automatically. |
| `artifacts/` | Historical arena navigation screenshots, JSON reports and pointer-lock notes. | Preserve as historical diagnostics only; they are not Unity assets or runtime configuration. |
| `balance/history/` | Versioned browser prototype/profile history. | Preserve as provenance; it is not the active Unity Balance Lab profile or an input to the Player. |
| Git history and old worktrees/branches | Removed browser code and its original commits. | Keep history intact. Do not restore browser code merely to run an old gate. |

## What is active

- `unity/` is the native Unity project and the sole runtime.
- `docs/GAME_SPEC.md` is the approved product/architecture overview; canonical
  requirements and lifecycle records remain in the planning home.
- `scripts/trooper/` is an offline asset-authoring pipeline. It can create
  derivatives and evidence, but it is neither a runtime nor a distribution
  command. Read its README and use the confirmation-gated Make targets.

Native Player QA, physical-device acceptance, target-hardware performance and
any distribution decision remain independent Unity gates. See
[`unity/README.md`](../unity/README.md) and
[`docs/PERFORMANCE_GATES.md`](PERFORMANCE_GATES.md).
