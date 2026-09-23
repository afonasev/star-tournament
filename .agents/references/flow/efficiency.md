# Context, models and budgets

No always-running coordinator. Humans choose worker-session count. Use GPT-6 Sol at medium effort as the default implementation model. Use GPT-6 Luna for bounded, well-understood tasks with clear expected results. Use GPT-6 Astra for architecture, complex debugging, material AI/contract uncertainty, or an explicit user request. Explicit model choice wins. Validate model availability; no global model pins or credential/approval changes.

Delegate only when explicitly requested or applicable instructions authorize a concrete independent subtask. No blanket delegation requirement. Send minimum scope/spec revision/files/checks, not full discovery history. Parent owns acceptance of returned evidence. Batch only related small changes sharing context/checks, retain separate lifecycle records.

Start focused checks; run required full checks at integration and rerun only for changes/failures. After two unsuccessful attempts reassess approach/model/scope rather than loop. Use configured budgets when present; do not invent a token budget. Log usage externally reported by runtime with unknown fields null. Separate worker, review and coordination costs, cached and uncached input. Optimize accepted-result cost and rework, not just cheapest model per call.

Keep detailed logs outside conversation. Save compact handoff at context pressure: scope, revisions, verified facts, next checks, resources, questions. Replace a saturated session without dropping ownership or restarting completed work. Future runner must use same transitions, locks and evidence contracts.
