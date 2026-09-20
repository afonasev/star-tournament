# 32 — Unity shot feedback и mouse sensitivity

Выполнен scoped change `add-unity-shot-feedback-mouse-sensitivity`: immutable combat endpoints питают renderer-only дробь/impact; обычные настройки сохраняют validated mouse degrees-per-pixel вне frozen match profile.

Проверено: strict OpenSpec, EditMode 87/87, PlayMode 66/66 и Mac Player build. Evidence: `docs/evidence/unity-shot-feedback-mouse-sensitivity-2026-09-20/report.json`.

Muted native Player diagnostics сохранены в `docs/evidence/unity-shot-feedback-mouse-sensitivity-2026-09-20/camera-mount-player-1/` и `camera-mount-player-4/`: PNG/JSON для default/min/max sensitivity и actual shot; 1-view JSON фиксирует ровно одну active camera, 4-view — четыре. PNG осмотрены: camera-space muzzle mount даёт отдельные cyan capsule streaks от оружия к фактической цели, а contact marker остаётся компактным. Суперседированные capture-наборы удалены из репозитория.

Открыто после handoff: physical mouse/focus acceptance, artistic review, reference-hardware performance и явная пользовательская приёмка. Не архивировать, не интегрировать и не деплоить.
