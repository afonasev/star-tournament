# 32 — Unity shot feedback и mouse sensitivity

Выполнен scoped change `add-unity-shot-feedback-mouse-sensitivity`: immutable combat endpoints питают renderer-only дробь/impact; обычные настройки сохраняют validated mouse degrees-per-pixel вне frozen match profile.

Проверено: strict OpenSpec, EditMode 87/87, PlayMode 66/66 и Mac Player build. Evidence: `docs/evidence/unity-shot-feedback-mouse-sensitivity-2026-09-20/report.json`.

Открыто до handoff completion: muted native Player PNG/JSON для flight/contact impact и default/min/max sensitivity в 1/4 viewport; physical mouse/focus, artistic и reference-hardware performance acceptance. Не архивировать, не интегрировать и не деплоить.
