# Orbital F2 — реальная модель и native Player

F2 перерабатывает отвергнутую пользователем поставку `b85b865` в прежнем выбранном направлении Orbital refined №2. Макет находится в shared change `add-vector-shotgun-and-armored-hands/assets/f1-02-orbital-refined.png`. F1, его Blender-исходники и Player сохранены отдельно.

## Изменение авторинга

Постоянно видимая кисть теперь использует исходную анатомическую геометрию, UV, тканевые/кожаные детали и веса ART_LOLL из immutable baseline `49878a337e98f999787b530b9a071ce6698f55cb`. Рукава отделены по skin weights. Поверх этой основы построены тонкие панели пальцев/пясти и охватывающие предплечье оболочки Orbital с раздельными керамикой, шасси, окантовкой и швами. CC BY attribution сохранена. Промежуточная процедурная перчатка оказалась беднее исходной по детализации и не поставляется.

У оружия переработаны контуры оболочек, открытые участки шасси, охлаждающий канал и задняя часть. Вогнутые контуры имеют корректную триангуляцию и небольшую кривизну; прежние радиальные поверхности давали складки на границе. Stable manifest names и `vector-muzzle-0/1`, `vector-armored-hands`, `identity-vector-status` сохранены.

`OrbitalGripPose` добавляет опорный хват после оценки исходных семи клипов; сами persisted Unity clips не изменены. Коррекция учитывает преобразование осей Blender/Unity. Размещение, хват и локальная подсветка читают `unity-trooper-presentation-v1@2` с metadata/ranges. Подсветка имеет маску только своего viewmodel. Gameplay и camera/input state не изменены.

## Первичные визуальные данные

- `equipment-model.png` — рендер экспортированного standalone GLB в Blender.
- `hands-and-weapon.png`, `hand-detail.png` — экспортированные GLB в позе aim с той же authored additive коррекцией опорного хвата.
- `player/01-idle.png`, `02-fire.png`, `03-smoke.png`, `04-settled.png`, `06-motion.png` — реальный native Development Player, muted; выстрел и движение проходят через session actions.
- `player/05-two-seats-world.png` — два viewport и world weapon.
- `asset-audit.json`, `export-contract.json` — hashes, геометрия, встроенные текстуры, 57 joints и семь clips; LOD1 сохраняет отдельный сокращённый экспорт.

Изображения выше не являются AI-макетами. Проверены фактические формы, контакт опорной руки, выстрел и два viewport. Это кандидат для художественной оценки: равенство уровню или точное повторение макета не заявляется. FPS в синтетических captures с паузами, фоновыми окнами и параллельным Editor не является performance benchmark. Физическая игра/контроллеры и художественная приёмка отдельны.

## Воспроизводимость

Blender 5.2.1 LTS: `scripts/trooper/build_vector.py`, затем `scripts/trooper/review_orbital.py`. `python3 scripts/trooper/audit_orbital_contract.py` проверяет GLB contracts и manifest hashes. Source dimensions описывают контент в метрах; они не являются runtime balance.

Unity: `unity/tools.sh test-edit`, `unity/tools.sh test-play`, `unity/tools.sh build`. Результаты XML лежат рядом. Immutable Player и упакованные `.blend` находятся в отдельном каталоге поставки, записанном в shared `acceptance.md` и `delivery.json`.

Проверки F2: EditMode **109/109**, PlayMode **101/101**, macOS Development build **PASS**. Native capture содержит шесть состояний, muted; журнал проверен без исключений. Исходные семь persisted `.anim` не изменены относительно baseline этой ветки.
