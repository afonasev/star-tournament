# Orbital F3 — сплошное соединение корпуса

По feedback пользователя закрыт разрыв между рукояткой и ствольным блоком, включая поднятый участок над подствольной опорой. Добавлены цельная шейка receiver, сопряжённые керамические накладки и закрытое подствольное шасси. Руки, материалы перчаток, поза и animation clips F2 сохраняются. Gameplay, muzzle mounts и profile parameters не менялись.

`equipment-side.png` — проверочный боковой рендер реального экспортированного GLB; `equipment-model.png` — общий ракурс. `hands-and-weapon.png` и `hand-detail.png` — фактическая модель в позе aim. Это не AI-макеты. `export-contract.json` проверяет LOD, embedded images, 57 joints, семь clips и hashes manifest; `hand-preservation.json` — сравнение геометрии/UV/skin с F2.

Авторинг: Blender `scripts/trooper/build_vector.py`; рендеры: `scripts/trooper/review_orbital.py`; контракт: `python3 scripts/trooper/audit_orbital_contract.py`. Новый Blender source сохраняется отдельно в `.local/orbital-f3-source`, предыдущие F1/F2 candidate и source не перезаписываются.

Проверки и путь immutable local macOS Player фиксируются в canonical change `add-vector-shotgun-and-armored-hands`, `acceptance.md` и `delivery.json`. Художественная/физическая приёмка и performance benchmark отдельны от автоматических проверок.

При проверке исправлена прежняя синтаксическая ошибка Unity YAML в `TagManager.asset`: пустые значения слоёв записаны явно как пустые строки, состав и порядок слоёв сохранены.

Автоматические проверки: EditMode 109/109, PlayMode 106/106. После исправления формата пустых слоёв повторены EditMode 109/109 и TrooperPresentationTests 7/7; новый build без ошибки чтения TagManager. BuildMac формирует сцену из текущих upstream defaults (lighting `orbital-league-ring-v1@8`, map `combat-bowl-v1@14`); его generated scene IDs и shader prefilter caches не включены в правку оборудования.
