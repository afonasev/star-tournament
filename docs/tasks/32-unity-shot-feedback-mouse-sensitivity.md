# 32 — Unity shot feedback и mouse sensitivity

Выполнен scoped change `add-unity-shot-feedback-mouse-sensitivity`: immutable combat endpoints питают renderer-only дробь/impact; обычные настройки сохраняют validated mouse degrees-per-pixel вне frozen match profile.

Проверено: strict OpenSpec, EditMode 87/87, PlayMode 66/66 и Mac Player build. Evidence: `docs/evidence/unity-shot-feedback-mouse-sensitivity-2026-09-20/report.json`.

Muted native Player diagnostic из exact commit сохранён в `docs/evidence/unity-shot-feedback-mouse-sensitivity-2026-09-20/player-4/`: PNG/JSON для flight/contact impact и default/min/max sensitivity на 1920×1080 с четырьмя viewport. PNG осмотрены: impact читается, но burst дробин в одной точке слишком плотный для художественного approval.

Открыто до handoff completion: 1-viewport Player PNG/JSON и отдельный visual-tuning decision; physical mouse/focus, artistic и reference-hardware performance acceptance. Не архивировать, не интегрировать и не деплоить.
