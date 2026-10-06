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

For acceptance read acceptance.md and release evidence. State actually tested release, expected outcome and needed devices. Current environment may be newer than original release: verify feature still included, record actual tested version and invalidate stale evidence where appropriate. User can accept a summary/report without manual test. Record an explicit attributable decision with helper accept, including exact result ref, accepted scope and message source, even while finalizing or another worker owns cleanup. The decision remains separate from the technical stage. Concept/mockup approval cannot establish Player acceptance. If review is postponed, use defer-acceptance and show it as deferred without asking again until the user resumes review. Distinguish wishes from failures.

Record same-scope defects as feedback IDs and rework tasks before archive, preserving previous acceptance in history; defects in archived work and new scope use linked changes. New wishes do not automatically reject working scope. Questions answered in original development sessions disappear here once resolved. Never duplicate entries.

After acceptance complete authorized technical finalization following delivery.md. Helper finalize uses the recorded decision without another approval; leave finalizing with its owner/resources when cleanup remains. Archive only after verification, publication, spec sync, cleanup, resolved questions and valid human acceptance. Acceptance and cleanup may happen in either order. Initiative acceptance additionally checks children and whole-result evidence. No application deploy is implied by opening inbox. Report remaining counts and exact blockers, not a claim that everything is done.

Star Tournament completion policy: after explicit acceptance, finish the authorized technical gates and cleanup, remove verified obsolete worktrees/branches and local builds under delivery.md retention rules, archive the OpenSpec change, then archive completed Codex chats with set_thread_archived. Do not ask again for routine cleanup/archive approval. Preserve active/dirty resources, pending acceptance candidates and the current accepted build per platform/channel; record blockers as finalization debt. Acceptance received in another chat triggers the same procedure for the recorded original resources/chats. Archive the current chat last and only after all its work is complete.

References live at `.agents/references/flow/` relative to the code project root; resolve that root before reading. Helper: `python3 tools/flow.py --root <planning-root> --help`.
