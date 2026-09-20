# Native layouts — 2026-09-19

**PASS функционального diagnostic:** 28/28 EditMode, 24/24 PlayMode, Mac Development Player build (324813891 bytes), два завершённых native сценария при фактических 1920×1080 и 3840×2160. По 21 PNG/state в каждой папке, hashes и проверки в `summary.json`. База 001824b, ветка codex/unity-native-seat-layouts.

## Что проверено

Матчи 4→2→3→4 через real native adapter с synthetic gamepads. Camera/HUD/participant/capsule counts соответствуют составу; P2/P3 View остаётся внутри своего viewport. При трёх игроках четвёртая ячейка постоянно показывает счёт; после native shotgun kill видны один kill, одна death и 100 score. Killcam View и постоянная таблица получают один snapshot. Pause/Repeat и results/Repeat сохраняют состав, сбрасывают счёт/clock/shot count. Disconnect третьего gamepad останавливает матч, reconnect с явным Resume восстанавливает. Arena/EventSystem/AudioListener остаются единичными. Неактивных участников нет.

Примеры:

- [Два viewport](fhd/2-live.png), [View только P2](fhd/2-view.png).
- [Три viewport и live-счёт](fhd/3-live.png), [killcam и счёт](fhd/3-killcam-view.png).
- [Результаты 4K](4k/3-results.png), [чистый Repeat](4k/3-results-repeat.png), [возврат четырёх](4k/4-return.png).
- [Setup](fhd/3-setup.png), [pause](fhd/3-pause.png), [disconnect](fhd/3-disconnect.png).

Основной агент просмотрел native окно и ключевые PNG обоих разрешений: границы, readable headers/numbers, лишние строки, health/ammo/timers, отдельные View, результат и меню. Дополнительно обычный Player **без helper** через native UI: mouse 4→3, Space keyboard join, 3→2 (P1 ready/P2 waiting; Start blocked), 2→3, diagnostic Start, Escape, Repeat с 05:00/full health/ammo и тем же layout, menu и Exit. Настоящих двух/трёх физических gamepads не было; управление игровыми seats проверено synthetic input, а обычный UI Start с одним устройством корректно не запускает human матч.

## Метод и ограничения

Профили не менялись: unity-proving-ground-v1@1, unity-combat-state-v1@1, unity-native-combat-v1@1, unity-native-match-v1@1. Арена — unity-two-level-fixture-v1; procedural seed отсутствует. FHD duration 5 min. При активации 4K окна mouse click попал в duration-plus: accepted setup duration 6 min, сохранённая на протяжении этого прогона и Repeat. Это configuration change через UI, не tuning override. Target off. Во время gameplay source выстрела — реальный resolver; poses перед kill — явная fixture. Finish ускорен пустыми native ticks; не измеряет реальное течение 5/6 минут.

Все 42 сохранённых state имеют focused=true/muted=true; первая попытка 4K не вышла из ожидания focus и исключена (`.local/unity-evidence/layout-4k-focus-wait/`). Перезапуск и переключение активного приложения восстановили фокус. Первый PlayMode startup сорвался в Bee с closed pipe до тестов; повтор и финальный regression suite прошли.

Это **не performance или physical acceptance**. Screenshot FPS на 4K включает значения ниже 60 и нагрузку PNG capture; из него нельзя получить frame-time gate либо объявить целевые 60 FPS. Reference PC/TV, quality/internal scale, долгий foreground playtest и физические устройства остаются открытыми. Teams/bots, восемь участников и playable solo не входят. Stage 0 не архивирован; main/integration/archive/deploy не выполнены.

Воспроизведение: `unity/tools.sh prepare`, `test-edit`, `test-play`, `build` последовательно. Player аргументы: `-layoutReview -layoutEvidence /absolute/path -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`, затем 3840×2160. Окну нужен focus. Логи/XML: `.local/unity-evidence/layout-final/`; Player logs — `layout-fhd/`, `layout-4k/`, `layout-ui/`. Полные screenshot/state assets сохранены здесь без изменения разрешения.
