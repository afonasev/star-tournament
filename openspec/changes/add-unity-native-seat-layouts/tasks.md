## 1. Native roster and layout

- [x] 1.1 Реализовать active 2–4 seat setup/input и shared layout; EditMode проверяет rectangles, readiness, shrink/grow и уникальные bindings.
- [x] 1.2 Подключить exact-N combat/presentation/cameras/HUD и read-only persistent standings; PlayMode проверяет roster/capsules/cameras, отсутствие скрытых целей и stale rows.
- [x] 1.3 Проверить Repeat, killcam View, pause/disconnect/reconnect и extra-device isolation для переменного состава; integration tests сохраняют scene ownership и frozen configuration.

## 2. Verification and handoff

- [x] 2.1 Полные EditMode/PlayMode и Mac Development build PASS; сохранить XML/log summary.
- [x] 2.2 Muted native Player journey FHD/4K для 2/3/4 layouts, View/killcam, results/Repeat/menu; сохранить и визуально проверить PNG/state, отдельно назвать synthetic ограничения.
- [x] 2.3 Повторное read-only review, strict OpenSpec validation, roadmap/evidence/handoff20 и отдельный commit; physical/performance и integration/archive gates открыты.
