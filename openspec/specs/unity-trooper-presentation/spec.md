# unity-trooper-presentation Specification

## Purpose
Показывать существующего skinned trooper v2 и его first-person руки в native Unity матче с независимой от игровых правил анимацией.

## Requirements

### Requirement: Existing character and hands
Native Player SHALL использовать существующего trooper v2 и производные его рук с существующим shotgun, сохранять артикуляцию кистей и лицензионное происхождение. Старый rigid compatible-rig SHALL NOT использоваться.

#### Scenario: Local views
- **WHEN** начинается матч с 2, 3 или 4 local seats
- **THEN** каждый живой игрок видит свои руки и оружие, другие камеры видят полное skinned тело; чужие first-person руки скрыты, при трёх местах четвёртая ячейка сохраняет счёт

### Requirement: Presentation follows gameplay
Анимация SHALL читать движение и события fire/hit/death/respawn, использовать существующие idle/walk/run/aim/fire/hit/death clips и SHALL NOT управлять motor, уроном, боезапасом либо life state. Death motion SHALL оставаться внутри неколлизионного visual corpse.

#### Scenario: Pause and repeat
- **WHEN** матч приостановлен либо повторён
- **THEN** анимационное время заморожено либо очищено вместе с presentation, старые трупы и fire/hit состояния не протекают в новый матч

#### Scenario: Death and respawn
- **WHEN** gameplay сообщает смерть и затем respawn
- **THEN** руки скрыты во время killcam, corpse проигрывает death без перемещения motor, после respawn руки и живое тело восстановлены

### Requirement: Reviewable resource and grip contract
Поставка SHALL содержать воспроизводимый shipping derivative, provenance и attribution, разделяемые immutable meshes/materials/textures и клипы, профильные настройки с едиными metadata. Хват SHALL проверяться с фактическим игровым оружием и несколькими фазами fire; proxy cylinders не заменяют эту проверку.

#### Scenario: Build verification
- **WHEN** собирается Mac Player
- **THEN** build содержит skinned тела, руки, все клипы и attribution; muted evidence показывает animated состояния и хват при 2/3/4 seats, измерения ресурсов и frame time помечены diagnostic до reference hardware acceptance
