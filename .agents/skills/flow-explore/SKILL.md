---
name: flow-explore
description: Discuss features and recommend quick, standard or initiative OpenSpec routes; capture agreed specifications without implementing until asked.
---

Read `/Users/eaafonasev/Projects/star-tournament-planning/workflow/project.json`
and existing relevant specs. Remain in exploration until the user authorizes
implementation; explicit capture creates artifacts, not application code.

Recommend quick only when expected result is settled, bug cause/implementation path is known, scope is one cohesive slice, no risky protocol/data migration or cross-system contract change, and verification/deployment are bounded. File count is not a criterion. Recommend initiative for multiple independently useful stages with shared goals/dependencies; otherwise standard. Explain the route in 1-2 sentences, investigate uncertainty rather than asking the user to classify the work.

On agreement use `openspec new change NAME --schema flow-quick|flow-standard|flow-initiative` in configured planning home (with --store if selected). Follow status/instructions for artifacts; do not manually scaffold. Initialize delivery record with helper init. Proposal states route rationale, scope and acceptance; specs contain behavior deltas only when behavior changes. Pure fixes/refactors set supported skip_specs metadata with rationale, never invent requirements. See lifecycle.md for question persistence.

quick has no design artifact: keep proposal/tasks concise but preserve delivery, cleanup and human acceptance. Standard adds design. Initiative follows initiative.md. Recommend nearest stage rather than writing every future spec.

After capture, mark ready only when criteria satisfied. User saying 'делай' permits switching to flow-apply in this session, subject to project worktree and delivery authorization. Otherwise stop at agreed artifacts. Do not implement solely because a spec exists.

References live at `.agents/references/flow/` relative to the code project root; resolve that root before reading. Helper: `python3 tools/flow.py --root <planning-root> --help`.
