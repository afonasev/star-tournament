# Shared OpenSpec planning home

The canonical OpenSpec planning home for Star Tournament is
`/Users/eaafonasev/Projects/star-tournament-planning`, registered as
`star-tournament-planning`. It owns current specs, future changes, schemas,
delivery records, leases and their Git history.

The `openspec/` directory in this repository is a frozen historical snapshot
kept only so old commits and task records remain reproducible. It is not a
second planning source: do not create or modify changes there. Use explicit
`--store star-tournament-planning` on supported OpenSpec commands.

`docs/GAME_SPEC.md` remains the approved product and architecture overview.
Its normative requirements are reconciled into the planning home's canonical
specs; it links to that home rather than duplicating delivery lifecycle state.
Historical browser changes are not resumed by this adoption. The closure in
`docs/BROWSER_BACKLOG_CLOSURE.md` still governs them.

Deployment is not authorized by this profile. The former browser `make deploy`
command was removed with the legacy runtime on 2026-09-21. Native Unity
distribution, target environment, release identity and smoke command remain
unconfigured pending an explicit owner decision.
