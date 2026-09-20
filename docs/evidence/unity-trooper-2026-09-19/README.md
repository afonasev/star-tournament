# Unity trooper v2 integration — native evidence

Срез `integrate-unity-trooper`, native база `fc85267`, существующий ART_LOLL source SHA256 `53add01552d98244b75039c33319293c53e2eecc046b11890d6fa247fd13ef53`. Все Player-запуски muted. Это техническая интеграция; художественная, physical-controller/TV и длительная performance-приёмка остаются открытыми.

## Воспроизведение

1. Восстановить оригинал и v2 по `scripts/trooper/README.md`. Устойчивая копия — `/Users/eaafonasev/Documents/Codex/asset-vault/star-tournament/trooper-v2`.
2. Запустить `scripts/trooper/unity_shipping.sh`: отдельный Unity derivative, реальная модель дробовика, независимый GLB readback и 119 deformation poses. Оригинал и v2 не перезаписываются.
3. Последовательно `unity/tools.sh prepare`, `test-edit`, `test-play`, `build`. Unity 6000.3.23f1, glTFast 6.20.0, URP 17.3.0. В Player нет runtime GLB import, animation curves или Editor API.
4. Запустить Mac Development Player с `-trooperReview -trooperEvidence <absolute-directory> -screen-width 1920 -screen-height 1080 -screen-fullscreen 0` либо 3840×2160. Дать окну focus. Synthetic Gamepad API проверяет путь native input/session/presentation; это не реальные контроллеры.
5. Для короткого отдельного измерения: `-diagnostic -probeCameras 4 -probeEvidence <absolute-directory>` с теми же resolution flags. Перед запуском закрыть Editor/Blender и завершить CPU jobs. PNG снимается после 3 секунд warmup + 10 секунд измерения. Показатель FPS на functional PNG не является performance-протоколом.

## Контракты и ограничения

- Generic Animator/manual Playables, `applyRootMotion=false`, session clock, отдельный visual child; motor/камера/hit volumes не читают skinned bones. Death root остаётся внутри presentation.
- У body и first-person forearms общий skeleton/семь clips. Камера скрывает своё тело и чужие viewmodel layers. Материалы/текстуры и clip assets общие; pose/skeleton независимы. Цвет оружия задаётся MaterialPropertyBlock.
- Clips idle/walk/run/aim зациклены, fire/hit/death — one-shot. Editor сохраняет копии и проверяет каждый binding. Pause, Repeat, death/respawn и immediate same-clock reset проверяются по реальным transforms.
- Body/arms производны от существующего trooper; shotgun — существующий world asset, без старых robot hands. В first-person derivative используется анатомическое отсечение выше локтя; порог по skin weights сам по себе разрезал смешанные cloth/armor фрагменты. Срез остаётся ниже кадра; оставшиеся skin weights и tracks сохранены.
- Contact audit сравнивает взвешенные вершины и треугольники кисти с реальным shotgun mesh после независимого GLB импорта. Signed nearest-face distances нельзя трактовать как глубину проникновения: источник состоит из пересекающихся деталей со смешанным winding. Для пересечений применяется triangle BVH; близость несущих пальцев измеряется отдельно. Это не художественная оценка хвата или self-collision всего тела.
- Исходный cylinder fixture PASS в `../trooper-animation-pipeline` — отдельное историческое доказательство восстановления v2, не доказательство хвата реального оружия.
- Нет Humanoid retarget, corrective shapes, новых моделей персонажа или стороннего mocap. Нет teams/bots/solo/8-player/network/replay, main merge, archive или browser deploy.

## Shipping asset readback

`asset-import-readback.json`: оба GLB, 57 bones, все семь clips, сохранённые skin weights. Body 29 844 700 bytes; arms derivative с отсечением выше локтя 6 369 504 bytes; исходный v2 около 84 MiB. Точные hashes/bytes/recipe — `asset-shipping-audit.json` и manifest.

`asset-exported-contact-audit.json`: 44 проверки (body/arms × обе руки × 11 clip/phase состояний), 0 triangle BVH overlaps. Для левой опоры максимальные ближайшие расстояния index/middle/ring — 1,881/2,359/3,701 мм; справа thumb/ring — 1,411/2,989 мм, middle — 5,017 мм. Правый указательный не считается несущим пальцем; левая опора — открытая ладонь, не цилиндрический обхват. Эти расстояния не являются универсальным критерием естественности позы.

`asset-deformation.json`: 119 поз, все 7 clips ниже неизменного 2× threshold; максимум 1,945392× в hit. В bake исправлены сгиб большого пальца справа и распределение поворота между левыми shoulder/upper-arm. Положение кисти вычисляется после её итогового поворота; fitter и bake используют один authored pre-IK baseline. Исходная source mesh и weights не правились.

## Native Player и тесты

EditMode **31/31**, PlayMode **28/28**, Mac Development build **PASS**. Точные UTC timestamps и log hashes — `automated-results.json`. После тестов добавлен только guard диагностического прогрева: он ждёт focus. Финальная сборка с guard прошла оба foreground frame-прогона; игровые scripts и shipping assets не менялись.

`player-review-summary.json`: **36 состояний FHD + 36 состояний 4K**. Все PNG имеют точные output размеры; каждый JSON focused/muted. Capture охватывает idle/walk/run/aim/fire/hit/death, terminal death, respawn, pause/same-clock и Repeat при 2/3/4 местах. В 3-seat layout четвёртая ячейка сохраняет счёт. Пары pause/paused-clock имеют одинаковые session clock и фактические bone positions.

В FHD setup duration 5 минут, в 4K — 4 минуты (изменено при активации setup окна); это не сравнение игрового темпа. В обоих прогонах проверялись те же короткие состояния и один presentation profile. Уникальные ресурсы одинаковы при 2/3/4 местах: **7 skinned meshes, 16 materials, 27 textures**; skin instances — 14/21/28. Counts относятся к активной normal-state scene, включая окружение/оружие, без corpse.

Через обычный native UI отдельно проверены setup, Space keyboard/mouse join, диагностический старт, Escape pause, Repeat, возврат в меню и Exit. Synthetic Gamepads не заявлены физическими устройствами. Player logs не содержат exceptions/errors.

## Короткие frame diagnostics

Apple M2 Pro / Mac14,10, macOS 14.6.1, четыре stationary animated troopers, 3 s warmup + 10 s measurement, Editor/Blender и CPU jobs завершены. Оба итоговых прогона полностью foreground, output FHD/4K; raw reports и PNG — `perf-fhd`/`perf-4k`. Первые прогоны с потерянным focus исключены и оставлены только в ignored `.local/trooper-rejected-probes`.

| Output | frame p50 | p95 | p99 | worst |
|---|---:|---:|---:|---:|
| perf-fhd | 8.325 ms | 9.162 ms | 9.338 ms | 9.443 ms |
| perf-4k | 8.336 ms | 9.292 ms | 16.718 ms | 17.789 ms |

Runtime texture-size diagnostic — 134 491 932 bytes; это оценка `Profiler.GetRuntimeMemorySizeLong`, не измеренная VRAM. Current allocated-memory, CPU/GPU timings и profile hash сохранены в JSON. Старого baseline Player в исходном worktree нет; количественное сравнение с rigid robot не заявляется.

Короткий stationary sample не закрывает длительные foreground 60 FPS, реальный бой, reference hardware/quality/internal scale, физические gamepads/TV или художественную приёмку. В 4K хвост отдельных кадров превышает 16,67 ms. Этап 2 roadmap остаётся незавершённым.
