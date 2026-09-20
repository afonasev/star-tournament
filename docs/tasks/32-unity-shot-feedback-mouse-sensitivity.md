# 32 — Unity shot feedback и mouse sensitivity

Выполнен scoped change `add-unity-shot-feedback-mouse-sensitivity`: immutable combat endpoints питают renderer-only дробь/impact; обычные настройки сохраняют validated mouse degrees-per-pixel вне frozen match profile.

Проверено: strict OpenSpec, EditMode 87/87, PlayMode 66/66 и Mac Player build. Evidence: `docs/evidence/unity-shot-feedback-mouse-sensitivity-2026-09-20/report.json`.

Muted native Player diagnostics сохранены в `docs/evidence/unity-shot-feedback-mouse-sensitivity-2026-09-20/player-4-v2/` и `player-1/`: PNG/JSON для default/min/max sensitivity и actual shot; 1-view JSON фиксирует ровно одну active camera. После presentation-only aggregation contact burst не засоряет 4-view кадр. PNG осмотрены: flight trace пока недостаточно заметен в обоих layout, поэтому visual requirement не закрыт.

Открыто до handoff completion: сделать flight trace заметным без clutter и повторить Player QA; physical mouse/focus, artistic и reference-hardware performance acceptance. Не архивировать, не интегрировать и не деплоить.
