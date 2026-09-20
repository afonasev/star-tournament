## 1. Подключённый матч

- [x] 1.1 Реализовать match profile/config/state и scoring; EditMode доказывает applied damage, assists/chain boundaries, target/time/overtime и frozen result.
- [x] 1.2 Подключить atomic combat snapshot/reduction и end evaluation; PlayMode доказывает mutual lethal и полный последний tick.
- [x] 1.3 Реализовать native setup duration/target, timers/standings/results и phase-specific menus; PlayMode проверяет Tab/View, killcam, focus/pause и navigation.
- [x] 1.4 Реализовать fresh Repeat/setup lifecycle с frozen configuration/assignments; repeated PlayMode проверяет input/corpses/lives/stats/clocks и отсутствие EventSystem/AudioListener/NavMesh дублей.

## 2. Проверка и передача

- [x] 2.1 Полные EditMode/PlayMode и Mac Development build PASS; сохранить XML/log summaries.
- [x] 2.2 Muted native Player проверка FHD/4K для setup/live/killcam standings/overtime/results/Repeat/menu; сохранить screenshot/state evidence и ограничения diagnostic.
- [x] 2.3 Обновить roadmap, evidence и handoff 19; strict OpenSpec validation и отдельный commit. Physical/performance и integration/archive остаются явно открытыми.
