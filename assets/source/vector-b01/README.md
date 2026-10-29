# B01 — сохранённый неактивный вариант

Точные body и FPS GLB из `78fc2d7851faac7b11a588b9f584ec88f178d84c`, до возврата ART_LOLL в игру. SHA-256 и editable source paths находятся в manifest.json. Каталог расположен вне Unity Assets и не входит в runtime.

Исходники остаются в Git: `scripts/vector_fighter/vector-body-b01.blend` (корпус), `scripts/vector_fighter/f23-military-color.blend` (шлем), сохранённые скрипты экспорта и переноса предплечий. Исторические импортные проверки B01 сохранены рядом как текст.

Чтобы вернуть именно эту версию, явно выбрать её, скопировать оба GLB обратно в Unity Art, обновить hashes manifest/mounts и выполнить TrooperPreparation.RefreshExistingScene, affected import/native QA. Не копировать только GLB без согласованных prefab/material ссылок. Для редактирования реконструкции использовать source и exporter на закреплённой ревизии из manifest.

У retained derivative content остаются исходные обязательства ART_LOLL / CC BY 4.0; см. ATTRIBUTION.md.
