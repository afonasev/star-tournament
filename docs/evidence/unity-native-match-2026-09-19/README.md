# Native match loop — 2026-09-19

**DIAGNOSTIC_NOT_PHYSICAL_OR_PERFORMANCE_ACCEPTANCE**. Проверена именно native сборка текущей ветки `codex/unity-native-match-loop` поверх `e735de7`. [Summary с hashes исходников и executable](summary.json).

EditMode **25/25**, PlayMode **21/21**, Mac Development build **PASS**. Unity 6000.3.23f1, URP 17.3.0. Профили: `unity-proving-ground-v1@1`, `unity-combat-state-v1@1`, `unity-native-combat-v1@1`, `unity-native-match-v1@1`. Арена `unity-two-level-fixture-v1`, authored layout без random seed.

## Native Player evidence

| Состояние | Full HD 1920×1080 | 4K 3840×2160 |
|---|---|---|
| Setup: duration/target | [PNG](fhd/setup.png) | [PNG](4k/setup.png) |
| Live: четыре таймера | [PNG](fhd/live.png) | [PNG](4k/live.png) |
| Killcam + View таблица | [PNG](fhd/killcam-standings.png) | [PNG](4k/killcam-standings.png) |
| Pause: Resume/Repeat/menu | [PNG](fhd/pause.png) | [PNG](4k/pause.png) |
| Paused clock | [JSON](fhd/paused-clock.json) | [JSON](4k/paused-clock.json) |
| Результат по цели | [PNG](fhd/target-results.png) | [PNG](4k/target-results.png) |
| Repeat: чистая жизнь/таймер | [PNG](fhd/repeat.png) | [PNG](4k/repeat.png) |
| Overtime после равенства | [PNG](fhd/overtime.png) | [PNG](4k/overtime.png) |
| Победитель overtime | [PNG](fhd/time-results.png) | [PNG](4k/time-results.png) |
| Возврат в setup | [PNG](fhd/menu.png) | [PNG](4k/menu.png) |
| Новый матч из setup | [PNG](fhd/new-match.png) | [PNG](4k/new-match.png) |

Рядом с каждым PNG хранится state JSON. Снимки просмотрены: колонки читаемы и выровнены, per-seat таблица не закрывает другие viewport, итоговая таблица и действия помещаются. Во всех snapshots audio muted, один EventSystem, AudioListener и arena owner. Killcam snapshot содержит dead life; pause/paused-clock имеют одинаковые match/combat clocks. Пять убийств дают cumulative 1200, target завершает матч; Repeat сбрасывает все stats, shot count, жизни и corpses. После time tie следующий kill завершает overtime с исходным time trigger. Возврат в setup очищает тела; новый Start начинает новый матч.

`NativeMatchReview` — CLI-only Editor/Development harness. Четыре synthetic gamepads проходят настоящий input adapter и weapon resolver. Вход review назначает synthetic devices, а setup/menu actions вызывают реальные native UI handlers; Resume/Repeat из результатов активируются gamepad Submit. Начальное позиционирование — явный fixture. Для time-limit сценария пустые native ticks ускорены; это **не benchmark**. Первый exploratory run ошибочно ожидал one-shot kill при shipping spread (фактически 95 damage); harness исправлен на ожидание фактической смерти, gameplay tuning не менялся. Исключённый прогон хранится только локально в `match-fhd-initial-fixture-failed`.

Дополнительно обычный Player без CLI review проверен через native UI: Space назначил keyboard/mouse, мышь изменила duration 5→6, запустила diagnostic match, Escape открыл pause, Repeat восстановил 06:00/full health/ammo, menu сохранил setup duration и assignment, Exit закрыл Player. Это UI smoke, не четыре физические устройства.

## Воспроизведение и ограничения

Последовательно: `unity/tools.sh prepare`, `unity/tools.sh test-edit`, `unity/tools.sh test-play`, `unity/tools.sh build`. Затем:

```sh
open -n unity/Builds/StarTournamentProvingGround.app --args \
  -matchReview -matchEvidence /absolute/evidence/path \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

Повторить при 3840×2160. Окно должно получить focus. Полные XML/logs — `.local/unity-evidence/match-final/`, Player logs — `match-fhd/` и `match-4k/`. Evidence JSON подтверждает фактический output, а не только CLI intent. В local sample inspection DTO null match в setup сериализуется Unity как пустой объект; он не обозначает запущенную session.

Synthetic input не закрывает physical four-controller/TV acceptance. FPS overlay и ускоренные ticks не доказывают минимум 60 FPS: reference hardware/quality/internal scale и длительный foreground/per-frame budget ещё открыты. Replay/restore, teams/bots/layouts, integration/archive/deploy не входят в проверку.
