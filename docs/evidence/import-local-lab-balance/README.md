# Локальный баланс → Default v2

Источник: сохранённый профиль «Default · копия», ревизия 4, 08.10.2026. Snapshot/hash перенесён целиком как Default v2; первая запись каталога сохранена без изменений. Исходная Lab-история не изменялась.

Четыре последних изменения движения относительно начала локальной копии: скорость, ускорение, торможение, гравитация; скорость прыжка нормализована до 7. Все отличия от предыдущей встроенной записи:

| Путь | Default v1 | Default v2 |
| --- | --- | --- |
| player.movement.maximumGroundSpeed | 9 | 6 |
| player.movement.groundAcceleration | 45 | 40 |
| player.movement.groundDeceleration | 32 | 30 |
| player.movement.gravity | 24 | 22 |
| player.movement.jumpSpeed | 8.5 | 7 |
| input.gamepadReturnDelay | 0.35 | 0.55 |
| damageBoost.initialDelaySeconds | 15 | 30 |
| combat.killcamSeconds | 3 | 5 |
| rocket.startingAmmo | 20 | 15 |
| death.corpseSeconds | 8 | 15 |
| rocket.botMinimumDistance | 7 | 6.5 |
| match.durationMinutes | 5 | 4 |
| match.targetPoints | 3000 | 2000 |
| cutter.width | 0.03 | 0.05 |
| cutter.jitter | 0.08 | 0.1 |
| cutter.frequency | 24 | 25 |
| cutter.arcWidth | 0.004 | 0.01 |
| corpse.rifleSpeed | 3.75 | 5.75 |
| corpse.cutterSpeed | 0.375 | 2.375 |
| blood.markSize | 0.18 | 0.2 |

Полный snapshot/hash и source provenance находятся в source.json. Статическая проверка: append-only каталог, точная исходная идентичность, уникальные пути, диапазоны и шаги значений — PASS. Полный EditMode: 399/399 PASS без пропусков. Полный PlayMode: первоначально 225/227 PASS; два ожидания старого баланса скорректированы без изменения runtime/каталога. Оба полных affected fixtures повторены: 15/15 PASS без пропусков; покрыты все 227 исходных сценариев. Packaged movement scenario и оба natural terminal bot matches прошли.

Контракты сохранены: duration-plus увеличивает текущую длительность ровно на минуту и передаёт её следующему матчу; после профильной задержки view возвращается в aim, pause замораживает clock, Repeat очищает corpse/view state. Добавлена проверка отсутствия раннего respawn. Остальные неизменённые fixtures повторно не запускались.

Результаты и длительности: verification.json; XML/log сохранены в gzip, включая первоначальные ошибки и исправленную компиляционную ошибку теста.

Пока выполнялась QA, локальная копия обновилась до revision 8 / registry 14. Все общие значения баланса совпадают с импортированной revision 4; новый input.gamepadTapAimThresholdSeconds=.22 отсутствует в текущем main / registry 13 и не переносится как часть этой замороженной ревизии. Исходная revision 4 и последующая локальная история сохранены; задача локальную историю не записывала.

Сборка Player, обновление установленного клиента и публикация не выполнялись. Человеческая/device/visual/audio приёмка остаётся открытой.
