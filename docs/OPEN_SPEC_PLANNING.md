# Shared OpenSpec planning home

The canonical OpenSpec planning home for Star Tournament is
`/Users/eaafonasev/Projects/star-tournament/planning`, registered as
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

## Location and independent Git state

`planning/` lives inside the primary checkout and is ignored by the code
repository via `/planning/`. It retains its own `.git`, history, branches and
uncommitted work. Commit planning changes from that repository; never force-add
it to the code repository or turn it into a submodule. All code worktrees use
the same canonical absolute path above, rather than a worktree-local copy.

```sh
git -C /Users/eaafonasev/Projects/star-tournament/planning status
python3 tools/flow.py --root /Users/eaafonasev/Projects/star-tournament/planning inbox
openspec store doctor star-tournament-planning
openspec list --store star-tournament-planning --json
```

The store ID stays `star-tournament-planning`. After any future relocation,
update `workflow/project.json` and working instructions, then switch the local
registration with `openspec store unregister star-tournament-planning` followed
by `openspec store register <new-home> --id star-tournament-planning`.
`unregister` only forgets the local registration; do not use `store remove`,
which deletes files. Run doctor and the read-only list/inbox commands afterwards.
Do not delete or clean `planning/` as generated content: it holds independent
source work even though the code repository ignores it.

For the 2026-10-15 move, preservation checks and the historical-path mapping
are recorded in `planning/workflow/relocation-2026-10-15.md`. Historical
evidence/receipts keep their original bytes; replace the old planning-home
prefix with the current home when following an old absolute evidence path.
