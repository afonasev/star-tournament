## 1. Подключённый бой

- [x] 1.1 Добавить native combat профиль/metadata, аналитический pellet resolver и tests зон/смешанного урона/occlusion/ближайшей цели.
- [x] 1.2 Подключить session к четырём motors/CombatLife, held/tap input и pause/release gate; integration tests доказывают damage/death/capsule и отсутствие автоогня.
- [x] 1.3 Реализовать current-state безопасный spawn и batch reservation; tests проверяют occupancy, floor/LOS/navigation и восстановление full life.
- [x] 1.4 Добавить health/ammo HUD, life-scoped killcam и timed non-colliding body; tests и native screenshots показывают death/respawn и paused timers.
- [x] 1.5 Исправить scene ownership/cleanup; повторный additive load/unload не оставляет runtime objects или NavMesh.

## 2. Проверка и передача

- [x] 2.1 Выполнить полный EditMode/PlayMode набор и Mac Development Player build; сохранить XML/log summaries.
- [x] 2.2 Выполнить muted native Player playtest боевой петли и setup/pause/resume; сохранить screenshots и отделить diagnostic от physical/performance acceptance.
- [x] 2.3 Обновить roadmap/handoff/evidence, выполнить strict OpenSpec validation и отдельный commit; сохранить открытые Stage 0 gates и следующий срез.
