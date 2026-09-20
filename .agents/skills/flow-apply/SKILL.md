---
name: flow-apply
description: Select and deliver an OpenSpec change or related small batch through verification, integration, authorized publication and cleanup; handle inline fixes.
---

Read `/Users/eaafonasev/Projects/star-tournament-planning/workflow/project.json`,
lifecycle.md and delivery.md. Call helper inbox; reconcile unfinished owned
finalization. If change not explicitly selected, show ready
candidates/dependencies and ask which to take, even if only one. Offer related
quick batches; preserve individual IDs. Do not begin blocked, paused or
initiative-only work as application implementation.

Claim selected change with actual session ID. Read installed OpenSpec status/instructions apply and relevant artifacts at the recorded revision. Use own code worktree, preserve unrelated work. Record owner/resources before editing. Work until delivered/finalized or genuinely blocked; do not stop at a plan. Persist questions before asking, incorporate answers and mark resolved. Never silently reduce scope. Expand quick to standard on new risks, retaining identity; ask only new product questions.

Tasks include checks, canonical spec sync, commit, merge, authorized deployment or report publication, smoke, durable evidence and cleanup. Observe leases. Record evidence and transition stage after each actual milestone. Missing runtime/model/tool availability is a real blocker, not proof of completion.

Prepare acceptance.md with version/URL, exact steps and expected results, or a reviewable report summary. Finalize resources then enter awaiting-acceptance and release claim. Never accept on user's behalf or archive early. Inline human acceptance uses flow-inbox procedure; inline defects get a persistent feedback ID and rework tasks or linked change. Fresh worktree for previously finalized work. See efficiency.md for model/context decisions.

References live at `.agents/references/flow/` relative to the code project root; resolve that root before reading. Helper: `python3 tools/flow.py --root <planning-root> --help`.
