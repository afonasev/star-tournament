---
name: flow-inbox
description: Collect unanswered human questions, pending acceptance and interrupted finalization across OpenSpec changes; record decisions and follow-up fixes.
---

Resolve shared planning root from
`/Users/eaafonasev/Projects/star-tournament-planning/workflow/project.json`
and call helper inbox. Read lifecycle.md. Present blocking questions first, then
pending result acceptance, then finalization debt, grouped by initiative and
required setup. Surface paused work but do not resume it. Build view from all
active and archived records; never rely on this conversation's memory.

Ask one coherent group at a time. Persist answer and source on existing question ID. Mark resolved only after applying the decision to artifacts or recording a verified handoff requiring worker acknowledgement. Do not race an active writer; use planning lease and coordinate revision changes.

For acceptance read acceptance.md and release evidence. State actually tested release, expected outcome and needed devices. Current environment may be newer than original release: verify feature still included, record actual tested version and invalidate stale evidence where appropriate. User can accept a summary/report without manual test. Only an explicit attributable decision advances accepted; distinguish wishes from failures.

Record defects as feedback IDs and rework tasks in an unaccepted change when same scope; otherwise scaffold linked change. New wishes do not automatically reject working scope. Questions answered in original development sessions disappear here once resolved. Never duplicate entries.

After acceptance perform authorized OpenSpec archive/finalization following delivery.md, or leave accepted with explicit archive debt. Initiative acceptance additionally checks children and whole-result evidence. No application deploy is implied by opening inbox. Report remaining counts and exact blockers, not a claim that everything is done.

References live at `.agents/references/flow/` relative to the code project root; resolve that root before reading. Helper: `python3 tools/flow.py --root <planning-root> --help`.
