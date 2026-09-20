# Lifecycle and shared records

Source: one shared OpenSpec change directory. `delivery.json` is the machine record; tasks.md is the executable checklist; acceptance.md is the human-readable evidence/scenario guide. Do not duplicate status in handoff spreadsheets or unrelated task trackers. Helper operations are atomic under a planning-root file lock. Full multi-file edits/commits additionally use the planning lease.

Stages: draft → ready → implementing → verified → merged → deployed → finalizing → awaiting-acceptance → accepted → archived. A report/initiative uses verified → published → finalizing (no fake deploy). rework-required → implementing. cancelled is explicit, preserves reasons/history. blocked is derived from open/answered blocking questions and dependencies, not a replacement for stage. pauses are explicit and sticky.

`ready`: scope agreed, route reason recorded, tasks and required artifacts prepared, no unresolved question blocking ready. `verified`: exact commit/evidence of required checks. `merged`: helper verifies commit is ancestor of configured main ref in code repository; squash integration requires the actual resulting commit plus original mapping. `deployed`: immutable release identity and smoke evidence. `published`: report/result location and review evidence. `finalizing`: specs synchronized, evidence durable, owned resources removed, planning edits committed. `awaiting-acceptance`: all finalization evidence present, no blocking question. `accepted`: explicit user decision with conversation reference, all child work accepted/archived for an initiative. `archived`: actual OpenSpec archive directory exists and closing planning commit is recorded. Recheck final Git cleanliness after committing closure metadata. Plain OpenSpec all_done only means checked tasks, not lifecycle completion.

Questions are persisted before asking: ID, text, affected stages (`blocks`), status open/answered/resolved, answer, source, resolution. An answer can arrive in any session. Do not mark resolved until incorporated into the relevant artifacts/work. If answering would modify an active worker's scope, coordinate and reconcile its revision before continuing. Nonblocking questions are still visible and must be resolved or explicitly withdrawn before final acceptance.

Dependencies: each edge names a change and threshold deployed (software)/published (report) or accepted. A delivered dependency in rework remains delivered only if its recorded release still supplies the required contract; assess explicitly, do not silently assume. Missing dependency and cycles block readiness. Child lists contain IDs only; do not copy tasks. No parent→child acceptance edge on a child already required by that parent.

Corrections before acceptance: record a feedback ID in the change history/acceptance guide, add explicit tasks, set rework-required, create fresh code worktree when needed. New scope and defects in archived work create linked changes. Preserve previous releases and decisions. A report acceptance may be just a reviewed summary; every change still waits for a human.

CLI: `python3 tools/flow.py --root ROOT inbox`; run `--help` for all commands. JSON payload files are explicit reviewable inputs. Helper records are not proof of successful checks: inspect underlying evidence. Usage per run: model, input/cached/output tokens if available, elapsed time and retries; unknown is null, not zero. Never infer subscription cost from token count.

## Helper examples

Run from the code repository; ROOT is the one shared planning root. Scaffold with OpenSpec first.

```sh
python3 tools/flow.py --root ROOT init add-feature --route quick --kind software
python3 tools/flow.py --root ROOT evidence add-feature scope --json-file scope.json
python3 tools/flow.py --root ROOT transition add-feature ready
python3 tools/flow.py --root ROOT claim add-feature --owner SESSION_ID
python3 tools/flow.py --root ROOT transition add-feature implementing --owner SESSION_ID
python3 tools/flow.py --root ROOT question add-feature 'Should completion unlock the next mission?' --blocks verified
python3 tools/flow.py --root ROOT answer add-feature Q1 --answer 'Yes' --source 'user message reference' --resolution 'Requirement and task updated'
python3 tools/flow.py --root ROOT inbox
```

Use temporary payload files outside the source checkout; remove only those owned files afterward. Evidence values should be structured JSON with exact command/result/revision/path, not a bare 'done'. Evidence keys:

- scope: agreed outcome, route rationale, spec revision, user/source, readiness checks.
- resources: code repo, worktree, branch, baseline, owner, processes and durable evidence destination.
- commit: exact implementation revision (string or structured evidence).
- verification: checks, commands, exit codes, artifact paths and tested revision.
- merge: object with repo (absolute path), commit (actual integrated revision), main_ref (verified primary ref), plus original revision mapping for squash.
- release / smoke: exact published identity, URL, commands and observed results.
- publication: report/initiative artifact location and revision.
- spec_sync: canonical revision and applied delta IDs, or verified explanation of no requirement change.
- cleanup: owned resource inventory, actual Git/status/branch/worktree/process checks and durable links.
- acceptance_guide: relative path string, normally acceptance.md.
- archive: archive path, closing commit and validation evidence.

Use transition accepted --decision 'explicit user decision' --source 'conversation reference' only after actual human input. Cancel likewise needs explicit decision/source. Usage JSON includes run ID, model, input/cached/output tokens (null if unknown), elapsed seconds, retries and phase. Do not double-count cumulative totals; record deltas per run. Reports and initiatives use kind report/initiative. Initiative creation must specify --route initiative --kind initiative; child creation uses --parent PARENT_ID.

Helper locking uses POSIX flock (macOS/Linux). Windows requires WSL or an adapted, tested lock implementation. Unknown/corrupt records fail visibly rather than silently dropping tasks. For interrupted ownership, inspect the recorded worker and resources, explicitly release its claim using its owner ID, log recovery in the change, then claim with the new session. Never infer expiry from age alone.
