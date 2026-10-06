# robot-animation-review Specification

## Purpose

Capability предоставляет отдельный безопасный browser-просмотрщик, чтобы оценивать полный набор анимаций робота со стороны без смешения с матчевой симуляцией или физическим вводом.

## Requirements

### Requirement: Управляемый muted animation-review sandbox
Browser SHALL предоставлять отдельный animation-review sandbox с роботом, оружием, камерой со стороны и явными controls для idle, walk, run, strafe, jump, aim, fire, hit и destruction. Sandbox MUST начинаться с выключенным звуковым выводом и MUST явно обозначать, что это renderer-only просмотр, а не матч, replay или проверка physical mouse-look. Выбор состояния и полный автоматический прогон MUST показывать плавное смешивание поз, а не мгновенную замену кадра.

#### Scenario: Просмотр отдельного состояния
- **WHEN** reviewer выбирает supported animation state
- **THEN** sandbox показывает соответствующее движение робота из neutral pose без создания или изменения authoritative match state

#### Scenario: LOD review
- **WHEN** reviewer переключает LOD0 и LOD1
- **THEN** sandbox сохраняет выбранное animation state и показывает role-defining hierarchy и silhouette для выбранного LOD

#### Scenario: Полный автоматический прогон
- **WHEN** reviewer запускает полный animation review
- **THEN** sandbox последовательно показывает каждый supported state через наблюдаемые плавные переходы и остаётся отделённым от gameplay input и simulation clock
