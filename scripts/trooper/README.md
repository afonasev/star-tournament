# Sci-Fi Soldier — независимый animation pipeline

Это **локальный кандидат**, не утверждённый игрок. Исходный ART_LOLL GLB не имел skeleton, skinning или animation. Прежний `trooper-compatible-rig.glb` не используется: он разрезан на жёсткие части и не содержит skin.

## Запуск

Из корня этого worktree:

```bash
TROOPER_PYTHON=/path/to/python-with-pillow scripts/trooper/run.sh
```

Нужны локальные Blender 5.2.1 LTS (проверенная версия) и Python с Pillow 12.3.0. `TROOPER_BLENDER=/path/to/blender` позволяет указать Blender явно. Pipeline не обращается к сетевым сервисам и не устанавливает зависимости. Проверенный Python этой машины: `/Users/eaafonasev/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3`.

До запуска положить `trooper-2k.glb` и исходный `SOURCE.md` в `.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/`. GLB должен иметь SHA-256 `53add01552d98244b75039c33319293c53e2eecc046b11890d6fa247fd13ef53`; другая ревизия требует нового аудита и landmarks. Pipeline перезаписывает только свои derivatives и evidence. Оригинал не меняется.

Шаги можно выполнять отдельно:

0. `geometry_checks.py`: пять проверок геометрического расчёта контакта треугольник/цилиндр.
1. `inspect_source.py`: импорт нетронутого оригинала, bounds, front/side/back PNG.
2. `build.py`: нормализация, weld, humanoid bones, weights, bake 7 actions, GLB, `.blend`, PNG.
3. `audit.py`: самостоятельный разбор binary GLB и свежий импорт; serialized weights, hierarchy, inverse bind matrices, clip names/durations, quaternion/loop проверки; 17 deformation samples на клип и readback PNG.
4. `review_rig.py`: actual rest skeleton рядом с моделью.
5. `review_hands.py`: свежий импорт GLB, изолированные крупные планы обеих рук (корпус скрыт только в QA) и 14 проверок хвата в aim/fire.
6. `contact_sheets.py`: обзор всех состояний, фаз walk/run/death, combat и кистей.
7. `finalize.py`: attribution, сохранённые source metadata и checksums.

Blender запускается с `--python-exit-code 1`: исключение действительно завершает pipeline ошибкой. `PASS_TECHNICAL` не является художественной приёмкой, тестом Unity Avatar или Khronos glTF Validator.

## Локальные результаты

Каталог `.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2/`:

- `trooper-rig.blend` — editable meshes, armature, vertex groups и семь Actions; QA stage входит только в `.blend`.
- `trooper-skinned-animated.glb` — один skin, 57 joints, шесть исходных meshes/materials, семь встроенных clips; QA ground/cameras/lights в GLB не экспортируются.
- `humanoid-map.json` — bone hierarchy, rest heads/tails и координатный контракт.
- `clips.json` — durations, loops, root-motion semantics.
- `source-audit.json`, `export-audit.json`, `grip-audit.json`, `grip-recipe.json`, `provenance.json`, `ATTRIBUTION.md`, `SHA256SUMS.json`.

Предыдущий `animation-pipeline-v1/` сохранён отдельно вместе с его QA. Текущие scripts/evidence воспроизводят v2.

В Git сохраняются scripts, planning, source/export audits, provenance и обзорные PNG в `docs/evidence/trooper-animation-pipeline/`. Тяжёлые GLB/Blender и individual PNG остаются локальными. При передаче на другую машину нужно перенести библиотеку вместе с SOURCE/ATTRIBUTION и checksum manifest; один Git checkout её не содержит.

Открыв `.blend`, выберите `TrooperHumanoid`, затем Action Editor и нужный Action. В сохранённой сцене выбран `idle`; все NLA tracks приглушены, чтобы клипы не складывались. `.blend` содержит linear skinning — именно тот вариант, который использует GLB. Для редактирования весов используйте vertex groups и Weight Paint; новые production параметры здесь не заводятся.

## Геометрия и skin contract

Исходник: 20 097 вершин, 30 757 треугольников, шесть material meshes. После weld совпадающих геометрических вершин — 16 691 вершина, **220 disconnected islands**, 2 167 boundary edges и 0 edges с более чем двумя faces. Это модульная открытая поверхность костюма; нулевой non-manifold count не означает watertight topology или отсутствие самопересечений.

Силуэт симметричен и пригоден для базовой humanoid позировки. Низкая плотность cloth topology в паху, локтях и коленях ограничивает сильное сгибание; для крупного плана нужен artist pass, возможно локальная retopology. Скрипт не генерирует новую геометрию персонажа и не выполняет скрытую автоматическую retopology.

Rest pose сохраняет исходную A-позу. Координаты Blender: метры, Z вверх, -Y вперёд; GLB: Y вверх, +Z вперёд. Общая высота до верхней детали — 1,80 м; origin на уровне подошв. Это offline normalization, а не изменение игровой капсулы. Armature: Root → Hips → Spine → Chest → UpperChest → Neck → Head; плечевые, локтевые, кистевые, тазобедренные, коленные, стопные и toe bones по сторонам. `humanoid-map.json` содержит точные значения.

Ткань использует плавные anatomical envelope weights; таз и перчатки дополнительно решаются по связности поверхности (harmonic weights). Границы пальцев выделяются по отдельным ветвям исходной геометрии, основание большого пальца использует мягкие screened-Laplacian constraints. Шлем, жилет, рюкзак и мелкие shell islands закреплены целиком либо имеют единый blend весов на island. Это намеренно воспроизводимый начальный skinning; он не выдаётся за ручную работу профессионального character rigger. Переходы таза и коленей сглажены, attachments колена используют согласованную смесь костей. Добавлены 30 костей пальцев (по три на каждый палец) и четыре кости распределения скручивания предплечий. GLB ограничен четырьмя влияниями на вершину; audit проверяет наличие ненулевых весов у всех новых костей.

24 исходных изображения имеют размеры 1K/2K. Оценка декодированного RGBA8 уровня 0 — **360 MiB**, без mipmaps, дополнительных копий и GPU overhead. Это не измерение Unity/браузерной VRAM. GLB около 84 MiB слишком тяжёл для принятия без отдельной работы над texture reuse/compression и LOD. Оптимизация не выполняется молча до утверждения кандидата.

## Клипы

| Clip | Длительность | Семантика |
|---|---:|---|
| idle | 2,4 с | loop, спокойная стойка |
| walk | 1,2 с | loop, in-place, обе фазы шага |
| run | 0,8 с | loop, in-place, более сильный подъём ног и работа рук |
| aim | 1,6 с | loop, двухручная поза с артикулированными пальцами и вытянутым правым указательным |
| fire | 0,4 с | one-shot, отдача рук и корпуса, движение правого указательного пальца |
| hit | 0,6 с | one-shot, отклонение корпуса и возврат |
| death | 1,6 с | one-shot, authored падение с correction по фактической поверхности пола |

Все движения заданы локально и baked при 30 fps; внешние mocap/animation packs не используются. Совместимость семи клипов с этим экспортированным skin подтверждается round-trip audit, а не сходством bone names. Loop boundaries проверяются по serialized translation/rotation/scale. Скорость игрового перемещения не задаётся; подбор скорости и stride в Unity остаётся отдельной работой. `death` содержит движение root, остальные клипы — in-place.

## Проверка хвата

`anatomy.py` задаёт измеренные landmarks исходной перчатки и фиксированный контрольный цилиндр диаметром около 24 мм. Локальный bounded fit подбирает curl пальцев по реальным весам skin; радиус цилиндра при подборе не меняется. Левый хват использует пять пальцев, правый — четыре с отдельным движением указательного. Рецепт и углы сохранены в `grip-recipe.json`.

`review_hands.py` проверяет независимо импортированный GLB: минимальный зазор всей поверхности треугольников до конечного цилиндра, затем близость каждого несущего пальца. Проверяются aim и шесть фаз fire для обеих рук, включая фазу нажатия. Допуски: проникновение не глубже 0,05 мм и зазор пальца не больше 3 мм. Это воспроизводимый тест локального хвата. Два локальных цилиндра не являются единым оружием: взаимное положение рук и контакт с утверждённой моделью оружия требуют отдельной проверки. Цилиндры присутствуют только в PNG и не экспортируются в GLB.

## Реальные ограничения и следующие gates

- **Художественная приёмка остаётся открытой.** Походка и падение процедурно заданы; статические PNG и механическая проверка не доказывают естественность движения. В v2 устранён прежний run warning; диагностический порог растяжения 2x не является универсальным стандартом качества.
- Finger rig и локальный хват реализованы. Проверка с реальным утверждённым оружием, first-person viewmodel и художественная доработка боевых поз не выполнены.
- Нет corrective shape keys, muscle/cloth simulation или retargeted mocap. Toe bones зарезервированы в hierarchy, но toe articulation не используется клипами.
- Unity import, Humanoid Avatar/T-pose mapping и Animator transitions не проверены. Имена семантически совместимы с humanoid mapping, но автоматическую Unity-совместимость не заявляем. Перед retarget необходимо настроить Avatar на reference T-pose, проверить стороны, таз, кисти и стопы; импорт GLB потребует отдельно выбранного Unity importer либо утверждённого FBX derivative.
- Нет игровых collider proxies, LOD, texture optimization, physical playtest или performance acceptance. Ни один renderer/runtime/manifest/public asset не меняется.
- Исходная лицензия CC BY 4.0 сохранена в GLB extras и SOURCE.md. При первоначальном аудите live Sketchfab page ответила 403; повторная проверка авторской страницы ограничена. Условия CC BY прочитаны на официальном сайте. Attribution и отметку об адаптации нужно сохранить при распространении; см. `ATTRIBUTION.md`.

Текущее поручение позволяет готовить кандидата, но не утверждает его качество для production или замену `light-sport-robot`.

## Native integration update 2026-09-19

Новое поручение разрешило интеграцию v2 в native Unity и производные first-person руки. Candidate-only запрет выше — исторический статус подготовки; текущий контракт см. `docs/GAME_SPEC.md` и `docs/tasks/completed/21-unity-trooper-integration.md`. Это не художественная или performance-приёмка. Устойчивая копия исходника и v2: `/Users/eaafonasev/Documents/Codex/asset-vault/star-tournament/trooper-v2`.

Native shipping воспроизводится после восстановления v2 командой `scripts/trooper/unity_shipping.sh` (тот же `TROOPER_BLENDER`). Отдельные этапы: `unity_grip_bake.py` сохраняет исходные lower-body/death clips и адаптирует верх тела к существующему shotgun; `unity_fit_contact.py` подбирает пальцы/кисти на реальных поверхностях; повторный bake с `--fitted` применяет рецепт; `unity_build_shipping.py` выпускает body и forearms-only GLB с общей системой материалов и max1024 textures. Это производные существующего v2, не новый персонаж. `unity_audit_shipping.py` независимо импортирует оба GLB, проверяет 57 костей/7 clips и поверхности кистей; `unity_deformation.py` повторяет 119 поз с прежним порогом 2×.

Unity prepare сохраняет Generic animation clips и prefabs в `Assets/StarTournament/Trooper`. Runtime не импортирует GLB и не создаёт animation curves. Собственный same-rig pipeline не требует Humanoid Avatar; retarget не проверялся. Shipping attribution включён в StreamingAssets. Числа offline asset recipe — измерения и статическая авторская поза; игровые presentation параметры находятся в `unity-trooper-presentation-v1@1` Balance Lab.
