# Context, models and budgets

No always-running coordinator. Humans choose worker-session count. Use gpt-6.1-sol/medium for ordinary scope and coordination; gpt-6-luna/medium for clear bounded implementation. Use Astra only for an explicit user choice or a named complex architectural/diagnostic decision; model-routing.md is authoritative. Explicit model choice wins. Validate model availability; no global model pins or credential/approval changes.

Delegate only when explicitly requested or applicable instructions authorize a concrete independent subtask. No blanket delegation requirement. Send minimum scope/spec revision/files/checks, not full discovery history. Parent owns acceptance of returned evidence. Batch only related small changes sharing context/checks, retain separate lifecycle records.

Select minimal affected checks from baseline/diff/dependencies using `../qa-scope.md`, including integration. Investigate uncertain impact and expand affected checks; full requires separate explicit human confirmation for this run. Rerun only for relevant changes/failures. After two unsuccessful attempts reassess approach/model/scope rather than loop. Use configured budgets when present; do not invent a token budget. Log usage externally reported by runtime with unknown fields null. Separate worker, review and coordination costs, cached and uncached input. Optimize accepted-result cost and rework, not just cheapest model per call.

Keep detailed logs outside conversation. Save compact handoff at context pressure: scope, revisions, verified facts, next checks, resources, questions. Replace a saturated session without dropping ownership or restarting completed work. Future runner must use same transitions, locks and evidence contracts.

## Default bounded work and waiting

Use GPT-6 Luna/medium for clear bounded development tasks. Explicit model/effort choices win; never use Luna below medium. Default subagent preferences are not a session-wide model pin.

For multi-chat coordination wait for completion/required-action events with wait_threads timeoutMs 120000 and saved afterCursor for each target. Unchanged timeout is not a reason for read_thread or a status message. Respect tool yielding and current interaction limits. After the second compaction finish only the current verifiable slice, required checks and handoff; do not start the next slice in the saturated session. The overall goal stays open after the handoff; automatic continuation does not authorize another slice in the saturated session. New chats and messages still require existing user authorization. See ../context-efficiency.md for reads/visual handoff and ../coordination.md for process waiting.
