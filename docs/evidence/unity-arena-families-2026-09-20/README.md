# Unity ARENA-2 evidence — 2026-09-20

Статус: automated/native diagnostic evidence, не user/physical/art/performance acceptance.

## Проверки

- `unity/tools.sh test-edit`: 86/86 passed.
- `unity/tools.sh test-play`: 65/65 passed.
- `openspec validate add-unity-arena-families --strict`: passed.
- `unity/tools.sh build`: `STAR_PROVING_BUILD_PASS 459366953`, Development Mac app создана.

## Muted native Player

Один и тот же Development build запущен в 1920×1080 с четырьмя diagnostic cameras и `AudioListener.volume = 0` для explicit `-arenaFamily 0`, `2` и `4`. Каждый `report.json` подтверждает `unity-arena-families-v1@1`, resolved family ID, полную arena identity, foreground на всём measurement и доступные CPU/GPU timings.

Осмотрены три PNG:

- `wide/player.png` — сохранён ARENA-1 wide-hall baseline, четыре viewport читаемы, clipping/blank frame не видны.
- `balconies/player.png` — split balcony/bridge silhouette различим в нижних viewport, четыре viewport и HUD читаемы, clipping/blank frame не видны.
- `basement/player.png` — basement support identity подтверждена JSON; текущая spawn-facing композиция визуально близка baseline, четыре viewport/HUD читаемы, clipping/blank frame не видны.

Показатели frame time остаются diagnostic на Mac14,10 / Apple M2 Pro и не закрывают reference-hardware performance gate. Synthetic stationary cameras не заменяют physical devices, TV или игровой playtest. Artistic acceptance не заявляется.
