# Orbital refined — F1

Выбран пользователем обновлённый макет №2 Orbital refined, включая руки. Предыдущий Vector отклонён как слишком угловатый и недостаточно детализированный. Референс хранится в shared planning change `add-vector-shotgun-and-armored-hands/assets/f1-02-orbital-refined.png`.

`scripts/trooper/build_vector.py` сохраняет стабильные asset/muzzle/material ключи прежнего pipeline, но создаёт Orbital v2: spline-contour domed ceramic shells, круглые многослойные muzzle collars, утопленные сервисные узлы, вентиляцию, маркировку и новые анатомичные перчатки с мелкими накладками, швами и гибкими манжетами. Все размеры являются authored geometry; новые runtime tuning параметры не добавлены.

- `equipment-model.png`, `hands-and-weapon.png`, `hand-detail.png`: рендеры настоящего экспортированного GLB, не AI-макеты.
- `asset-audit.json`: независимое чтение экспортированных GLB, topology/material/clip/muzzle contracts и SHA-256.
- `player/`: muted native Player diagnostic `-vectorReview`, реальные accepted Fire actions, отдельные idle/fire/smoke/settled и two-seat/world состояния.
- `editmode.xml`, `playmode.xml`: результаты автоматических проверок.

Профили `unity-proving-ground-v1@1` и `unity-trooper-presentation-v1@1`, фиксированные presentation seeds. Gameplay, камера, collision, баланс и исходные persisted animation clips не меняются.

Техническая проверка и изображения не заменяют художественную приёмку пользователя, физические устройства и performance на согласованном эталонном оборудовании.

## Проверено

EditMode 109/109, PlayMode 94/94, macOS Development build passed. Первый EditMode запуск до Prepare нашёл устаревшую ссылку prefab; Prepare пересоздал производные. Первый PlayMode запуск обнаружил переименование `vector-armored-hands`; имя восстановлено без изменения теста. Финальный прогон полностью зелёный. Семь persisted clips не изменены.

Native diagnostic: `combat-bowl-v1@8`, пять состояний завершены без logged exception, audio muted. FPS 1 на кадрах отражает фоновый screenshot diagnostic и не является performance benchmark. Новая геометрия оружия — 74 338 треугольников LOD0 / 40 885 LOD1, embedded first-person arms+weapon — 136 794; целевой hardware benchmark и artistic acceptance отдельно.
