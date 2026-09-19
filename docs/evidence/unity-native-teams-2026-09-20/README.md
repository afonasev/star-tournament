# Native Unity teams — 2026-09-20

База `4d0334f` поверх native/trooper `9b21406`; ветка `codex/unity-native-teams`, worktree 83e9. Новый профиль `unity-native-team-v1@1`; movement/combat/match/trooper профили сохранены. Фиксированная proving arena без procedural seed.

## Результат и проверки

- Native Player: FFA и две непустые Team A/B для 2–4 human seats. Setup assignments/цвета, initial spawn, friendly blocker, командные live/results и Repeat подключены.
- EditMode 41/41, PlayMode 32/32; XML приложены. Четыре новых PlayMode tests проверяют 22 непустых team assignments при 2/3/4 seats, initial clearance/separation, actual shotgun blocker, direct-damage guard/FFA, team respawn danger versus occupancy, simultaneous allied respawn, invalid draft/profile recovery, shrink/grow, цвета body/arms/corpse, result Repeat и возврат FFA.
- Mac Development build PASS, reported 459154141 bytes. Binary hash: `build.json`. Последовательные команды: `unity/tools.sh prepare`, `test-play`, `test-edit`, `build`; повторные прогоны после исправлений отражены ниже. Финальные runtime и Player не менялись после build/native QA.
- Strict OpenSpec validation PASS; read-only Astra review до реализации и после неё. Закрыты замечания к ArgumentException при profile step, false-positive blocker diagnostic без shot/ammo assertions и отсутствующему team-respawn regression.
- Первый PlayMode 30/31: тест цвета ошибочно читал `_BaseColor` у glTF-материала; исправлено чтение поддерживаемого `baseColorFactor`. Второй 31/31, после review добавлен четвёртый regression и итог 32/32. Shipping материалы не менялись.

## Native Player QA

`-teamReview -teamEvidence <absolute-dir>` при output 1920×1080 и 3840×2160. По 31 PNG/JSON на разрешение: invalid/setup/live/held standings/pause/Repeat/killcam/results/result Repeat/FFA для 2/3/4 seats плюс actual allied-blocker shot. Все 62 JSON подтверждают focus и mute. Native script использует synthetic gamepads и реальные UI/session paths; для результатов вводит явный resolved diagnostic damage и ускоряет пустые ticks. Это не physical input либо frame-time benchmark.

В Git находятся все FHD состояния, все 4K JSON и пять representative 4K PNG. Полный 62-PNG архив, manual PNG, три performance reports/PNG, XML и полные логи независимо сохранены в `/Users/eaafonasev/Documents/Codex/qa-vault/star-tournament/native-teams-2026-09-20`. `vault-checksums.json` содержит SHA256, readback подтверждён. Локальная исходная evidence: `.local/unity-evidence/native-teams-final/`.

Обычный запуск без CLI-review дополнительно проверен CUA: Space join, mouse mode toggle, пустая Team B блокирует start, восстановление Team B, смена цветов, diagnostic start, Escape pause, Repeat, menu и Exit. `manual/` содержит screenshot evidence. Windowed CUA clicks возвращали `noWindowsAvailable`; штатное macOS fullscreen через AX button восстановило click path. Смена fullscreen была только частью QA, игровые input adapters не правились.

### Просмотр

- [Setup и пустая команда](fhd/4-invalid-setup.png)
- [Два игрока](fhd/2-live.png)
- [Три игрока, grouped и permanent standings](fhd/3-standings.png)
- [Четыре игрока 1v3](4k/4-standings.png)
- [Союзник блокирует дробь: ammo19, health100, score0](4k/3-ally-blocks.png)
- [Team A победила](fhd/4-results.png)
- [Повтор результата](fhd/4-result-repeat.png)
- [Возврат FFA](fhd/4-ffa.png)

## Foreground diagnostic

Четыре stationary animated troopers, 3s warmup + 10s sample, текущий Mac14,10 / Apple M2 Pro. CLI `-diagnostic -teamsDiagnostic -probeEvidence <dir>`. Каждый report имеет focus=true, output size, CPU/GPU percentiles, memory и profile hash. Во время чистых samples Unity Editor/Blender не работали и CPU jobs не запускались.

| Запуск | Frame p95, ms | p99, ms | worst, ms |
| --- | ---: | ---: | ---: |
| Первый FHD, с CUA focus action | 9.109 | 13.454 | 1008.856 |
| Повтор FHD, без UI-инструментов | 9.208 | 9.352 | 16.453 |
| 4K, без UI-инструментов | 9.164 | 9.306 | 16.726 |

Первый секундный выброс сохранён и не исключён из исходного отчёта. Причина не установлена; повтор не доказывает её устранение. Это короткая diagnostic нагрузка, не long 60 FPS acceptance и не сравнение одинаковой сцены с предыдущим commit. Native team initial spawn отличается от FFA; прежние stationary цифры не являются строгим A/B baseline.

Shared resources: 7 skinned meshes / 16 materials / 27 textures, 28 skin instances при 4 seats. GPU timing доступен. FPS на QA PNG включает screenshot overhead и не используется как performance evidence.

## Открытые gates

Реальные gamepads/TV, художественная приёмка trooper, reference hardware/quality/internal scale и длительный foreground 60 FPS остаются открытыми. Bots/solo/8 scene participants, procedural arena migration, replay/network не реализованы этим срезом. Main/archive/deploy не выполнялись.
